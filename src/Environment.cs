
namespace Ghi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Emit;

    public class Environment : Dec.IRecordable, Dec.IPostCloneNew, Dec.IPostCloneOriginal
    {
        public static System.Threading.ThreadLocal<Environment> Current = new();
        public struct Scope : IDisposable
        {
            private Environment old;
            private Environment current;
            public Scope(Environment env)
            {
                old = Current.Value;
                Current.Value = env;
                current = env;
            }

            public void Dispose()
            {
                Assert.AreSame(Current.Value, current);
                Current.Value = old;
            }
        }

        internal struct Tranche : Dec.IRecordable
        {
            public List<Entity> entries;    // this length is canonical
            public Array[] components;  // these grow as needed, but often include padding

            public void Record(Dec.Recorder recorder)
            {
                recorder.Record(ref entries, "entries");
                recorder.Record(ref components, "components");
            }
        }
        private Tranche[] tranches;

        private struct EntityLookup : Dec.IRecordable
        {
            public Ghi.EntityDec dec;
            public int index;
            public int gen;

            public void Record(Dec.Recorder recorder)
            {
                recorder.Record(ref dec, "dec");
                recorder.Record(ref index, "index");
                recorder.Record(ref gen, "gen");
            }
        }
        private List<EntityLookup> entityLookup = new();
        private List<int> entityFreeList = new();

        private static List<Ghi.EntityDec> indexToEntityDec;

        private object[] singletons;
        private Dictionary<Type, int> singletonLookup = new();

        // if someone makes more than 64 bits of Environments then I salute you
        private static long s_LastUniqueId = 0;
        private long uniqueId = System.Threading.Interlocked.Increment(ref s_LastUniqueId);
        public long UniqueId
        {
            get
            {
                return uniqueId;
            }
        }

        // bump!
        // this allows our COW structures to recognize that they may now be shared
        // this effectively locks every existing COW class at its current state for eternity; if it changes it, it'll be by cloning it first
        public void PostCloneOriginal()
        {
            uniqueId = System.Threading.Interlocked.Increment(ref s_LastUniqueId);
        }
        public void PostCloneNew()
        {
            uniqueId = System.Threading.Interlocked.Increment(ref s_LastUniqueId);
        }

        // Status
        private enum Status
        {
            Idle,
            Processing,
        }
        private Status status = Status.Idle;

        // Phase-end deferral
        internal class EntityDeferred
        {
            public EntityDec dec;
            public Tranche tranche;

            public Entity replacement;

            public (EntityDec dec, Tranche tranche, int index) Get()
            {
                return (dec, tranche, 0);
            }
        }
        private List<Action> phaseEndActions = new();

        // Config
        internal static Func<Entity, string> EntityToString = null;

        public int Count
        {
            get
            {
                return tranches.Select(t => t.entries.Count).Sum();
            }
        }

        public IEnumerable<Entity> List
        {
            get
            {
                return tranches.SelectMany(t => t.entries);
            }
        }

        public static void Init()
        {
            var DbgEx = typeof(Dbg).GetMethod("Ex", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            indexToEntityDec = Dec.Database<Ghi.EntityDec>.List.OrderBy(dec => dec.DecName).ToList();
            foreach((var dec, int i) in indexToEntityDec.Select((dec, i) => (dec, i)))
            {
                dec.index = i;
            }

            // cache some data we can use
            var allEntities = Dec.Database<EntityDec>.List.OrderBy(dec => dec.index).ToArray();
            var allComponents = Dec.Database<ComponentDec>.List.OrderBy(cd => cd.DecName).ToArray();
            var allSingletons = Dec.Database<ComponentDec>.List.Where(cd => cd.singleton).OrderBy(cd => cd.DecName).ToArray();

            // set up SystemDec processes
            foreach (var dec in Dec.Database<SystemDec>.List)
            {
                var method = dec.method;
                var parameters = method.GetParameters().Select(param => param.ParameterType).ToArray();
                var parametersBare = parameters.Select(param => param.IsByRef ? param.GetElementType() : param).ToArray();

                {
                    // accumulate our entire match DB
                    var parameterDirectMatches = parametersBare
                        .Select(param => allComponents
                            .Select((c, i) => ( c, i ))
                            .Where(c => param.IsAssignableFrom(c.c.GetComputedType()))
                            .ToArray())
                        .ToArray();

                    // first, see if this can be mapped to all-singleton
                    if (parameterDirectMatches.All(matches => matches.Length == 1 && matches[0].c.singleton))
                    {
                        // it can!
                        // somewhat surprised tbqh

                        // build our artificial IL function
                        var dynamicMethod = new DynamicMethod($"ExecuteSystem{dec.DecName}",
                            typeof(void),
                            new Type[] { typeof(Tranche[]), typeof(object[]) },
                            true);
                        System.Reflection.Emit.ILGenerator il = dynamicMethod.GetILGenerator();

                        // needs to start and end with the same number of parameters, so let's just do this within the exception block
                        var ex = il.BeginExceptionBlock();

                        // read all the singletons
                        for (int i = 0; i < parameters.Length; ++i)
                        {
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Ldc_I4, allSingletons.FirstIndexOf(singleton => singleton == parameterDirectMatches[i][0].c));
                            il.Emit(OpCodes.Ldelem_Ref);
                        }
                        il.Emit(OpCodes.Call, method);

                        il.BeginCatchBlock(typeof(Exception));
                        // whoops something went wrong
                        il.Emit(OpCodes.Call, DbgEx);
                        il.EndExceptionBlock();

                        // and we're done with the singleton-only path!
                        il.Emit(OpCodes.Ret);

                        dec.process = (Action<Tranche[], object[]>)dynamicMethod.CreateDelegate(typeof(Action<Tranche[], object[]>));

                        // NEXT.
                        continue;
                    }
                    else if (parameterDirectMatches.Any(matches => matches.All(m => !m.c.singleton)))
                    {
                        // definitely can't be a singleton match, so we're OK with it
                    }
                    else if (parameterDirectMatches.Any(matches => matches.Length > 1))
                    {
                        var ambiguity = string.Join("; ", parameters.Zip(parameterDirectMatches, (param, matches) => ( param, matches ))
                            .Where(x => x.matches.Length > 1)
                            .Select(x => $"{x.param} matches [{string.Join(", ", x.matches.Select(m => m.c.GetComputedType().ToString()))}]"));

                        Dbg.Err($"{dec}: Ambiguity in singleton scan! {ambiguity}");
                    }
                }

                // for each tranche, we need to see if it applies . . .
                var trancheDat = new List<(int trancheId, (int from, int to)[] singletonRemap, (int from, int to)[] trancheRemap)>();

                for (int trancheId = 0; trancheId < allEntities.Length; ++trancheId)
                {
                    // see if we can find an unambiguous mapping, including all singletons and every one of our component types
                    var availableComponents = allSingletons.Concat(allEntities[trancheId].components).ToArray();
                    var parameterTrancheMatches = parametersBare
                        .Select(param => availableComponents
                            .Select((c, i) => (c.GetComputedType(), i))
                            .Concat(Enumerable.Repeat((typeof(Entity), -1), 1))
                            .Where(c => param.IsAssignableFrom(c.Item1))
                            .Select(c => c.Item2)
                            .ToArray())
                        .ToArray();

                    if (parameterTrancheMatches.All(matches => matches.Length == 1))
                    {
                        // our singletons are first, followed by possible components, so we need to map these up as appropriate
                        // as we do this, we build a remap array for us to rapidly remap things
                        List<(int from, int to)> singletonRemaps = new List<(int from, int to)>();
                        List<(int from, int to)> trancheRemaps = new List<(int from, int to)>();
                        for (int j = 0; j < parameters.Length; ++j)
                        {
                            if (parameterTrancheMatches[j][0] == -1)
                            {
                                // this is Entity
                                trancheRemaps.Add((-1, j));
                            }
                            else if (parameterTrancheMatches[j][0] < allSingletons.Length)
                            {
                                // this is a singleton
                                singletonRemaps.Add(( parameterTrancheMatches[j][0], j ));
                            }
                            else
                            {
                                // this is a component
                                trancheRemaps.Add(( parameterTrancheMatches[j][0] - allSingletons.Length, j ));
                            }
                        }

                        // compile this down for efficiency
                        var singletonRemapArray = singletonRemaps.OrderBy(remap => remap.from).ToArray();
                        var trancheRemapArray = trancheRemaps.OrderBy(remap => remap.from).ToArray();

                        trancheDat.Add((trancheId, singletonRemapArray, trancheRemapArray));
                    }
                    else if (parameterTrancheMatches.Any(matches => matches.Length > 1))
                    {
                        var ambiguity = string.Join("; ", parameters.Zip(parameterTrancheMatches, (param, matches) => ( param, matches ))
                            .Where(x => x.matches.Length > 1)
                            .Select(x => $"{x.param} matches [{string.Join(", ", x.matches.Select(m => m.ToString()))}]"));

                        Dbg.Err($"{dec}: Ambiguity in entity scan! {ambiguity}");
                    }
                }

                if (trancheDat.Count != 0)
                {
                    // verify that singletons match on each one
                    for (int i = 1; i < trancheDat.Count; ++i)
                    {
                        Assert.AreEqual(trancheDat[0].singletonRemap, trancheDat[i].singletonRemap);
                    }

                    // we put singletons in a single array so we can do it exactly once
                    var singletonLookup = trancheDat[0].singletonRemap;
                    var trancheLookups = trancheDat.Select(tdo => (tdo.trancheId, tdo.trancheRemap)).ToArray();

                    // build our artificial IL function
                    var dynamicMethod = new DynamicMethod($"ExecuteSystem{dec.DecName}",
                        typeof(void),
                        new Type[] { typeof(Tranche[]), typeof(object[]) },
                        true);
                    System.Reflection.Emit.ILGenerator il = dynamicMethod.GetILGenerator();

                    // yank singletons out and apply appropriate casting
                    // we're making an array based on our parameter order so we can fill it in later
                    Action<int>[] singletonLookups = new Action<int>[parameters.Length];
                    for (int i = 0; i < singletonLookup.Length; ++i)
                    {
                        il.Emit(OpCodes.Ldarg_1);

                        // this can be more optimized for size
                        il.Emit(OpCodes.Ldc_I4, singletonLookup[i].from);

                        // use a ref so we're not copying structs around
                        il.Emit(OpCodes.Ldelem_Ref);

                        var local = il.DeclareLocal(parameters[singletonLookup[i].to]);
                        il.Emit(OpCodes.Stloc, local);

                        singletonLookups[singletonLookup[i].to] = index =>
                        {
                            il.Emit(OpCodes.Ldloc, local);
                        };
                    }

                    // singletons should now be in an appropriate type, and local, which is probably the fastest solution
                    // but there might be better options!

                    // now loop through all the tranches, we'll generate IL for each one
                    for (int i = 0; i < trancheLookups.Length; ++i)
                    {
                        // we'll be using temp values that we want to eliminate after this, so we'll just use a scope for it
                        // whoops we can't do that
                        // well uh
                        // figure this out later
                        //il.BeginScope();

                        int trancheId = trancheLookups[i].trancheId;

                        // first set up the arrays
                        var trancheRemapArray = trancheLookups[i].trancheRemap;

                        // remap the components
                        Action<int>[] lookups = (Action<int>[])singletonLookups.Clone();
                        for (int j = 0; j < trancheRemapArray.Length; ++j)
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldc_I4, trancheId);
                            il.Emit(OpCodes.Ldelema, typeof(Tranche));

                            int from = trancheRemapArray[j].from;
                            if (from == -1)
                            {
                                // grab the entries field
                                il.Emit(OpCodes.Ldfld, typeof(Tranche).GetField("entries"));

                                // shove this into another local
                                var local = il.DeclareLocal(typeof(List<Entity>));
                                il.Emit(OpCodes.Stloc, local);

                                // and then we'll just use a lambda to grab it later
                                lookups[trancheRemapArray[j].to] = index =>
                                {
                                    il.Emit(OpCodes.Ldloc, local);
                                    il.Emit(OpCodes.Ldloc, index);
                                    il.Emit(OpCodes.Callvirt, typeof(List<Entity>).GetMethod("get_Item"));
                                };
                            }
                            else
                            {
                                // grab the appropriate components array
                                il.Emit(OpCodes.Ldfld, typeof(Tranche).GetField("components"));
                                il.Emit(OpCodes.Ldc_I4, from);
                                il.Emit(OpCodes.Ldelem_Ref);

                                // get the appropriate array type so we can avoid casts at runtime
                                // we pull this out of the entity type, not our parameter types; implicit casting on the function call is (probably?) cheaper than messing around with arrays
                                var itemType = allEntities[trancheId].components[from].GetComputedType();
                                var arrayType = itemType.MakeArrayType();
                                var local = il.DeclareLocal(arrayType);
                                il.Emit(OpCodes.Castclass, arrayType);
                                il.Emit(OpCodes.Stloc, local);

                                // and then we'll just use a lambda to grab it later
                                var parameter = parameters[trancheRemapArray[j].to];
                                lookups[trancheRemapArray[j].to] = index =>
                                {
                                    il.Emit(OpCodes.Ldloc, local);
                                    il.Emit(OpCodes.Ldloc, index);

                                    if (parameter.IsByRef)
                                    {
                                        il.Emit(OpCodes.Ldelema, itemType);
                                    }
                                    else
                                    {
                                        il.Emit(OpCodes.Ldelem, itemType);
                                    }
                                };
                            }
                        }

                        // Store the length of the entries array, this is our loop length

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4, trancheId);
                        il.Emit(OpCodes.Ldelema, typeof(Tranche));
                        il.Emit(OpCodes.Ldfld, typeof(Tranche).GetField("entries"));
                        il.Emit(OpCodes.Callvirt, typeof(List<Entity>).GetProperty("Count").GetGetMethod());
                        var entitylistlen = il.DeclareLocal(typeof(int));
                        il.Emit(OpCodes.Stloc, entitylistlen);

                        // working index
                        var index = il.DeclareLocal(typeof(int));
                        il.Emit(OpCodes.Ldc_I4_0);
                        il.Emit(OpCodes.Stloc, index);

                        // loop positions
                        var loopStart = il.DefineLabel();
                        var loopEnd = il.DefineLabel();

                        // start of the loop
                        il.MarkLabel(loopStart);

                        // Compare index with entitylistlen
                        il.Emit(OpCodes.Ldloc, index);
                        il.Emit(OpCodes.Ldloc, entitylistlen);

                        il.Emit(OpCodes.Bge, loopEnd); // If index >= entitylistlen, jump to loopEnd

                        // Get ready to make the actual call
                        // needs to start and end with the same number of parameters, so let's just do this within the exception block
                        var ex = il.BeginExceptionBlock();

                        // read all the parameters
                        for (int paramIndex = 0; paramIndex < lookups.Length; ++paramIndex)
                        {
                            lookups[paramIndex](index.LocalIndex);
                        }
                        il.Emit(OpCodes.Call, method);

                        il.BeginCatchBlock(typeof(Exception));
                        // whoops something went wrong
                        il.Emit(OpCodes.Call, DbgEx);
                        il.EndExceptionBlock();

                        // Increment the loop index
                        il.Emit(OpCodes.Ldloc, index);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Add);
                        il.Emit(OpCodes.Stloc, index);

                        // Jump back to the start of the loop
                        il.Emit(OpCodes.Br, loopStart);

                        // Mark the end of the loop
                        il.MarkLabel(loopEnd);
                    }

                    // we done!
                    il.Emit(OpCodes.Ret);

                    dec.process = (Action<Tranche[], object[]>)dynamicMethod.CreateDelegate(typeof(Action<Tranche[], object[]>));
                }
                else
                {
                    // this can really be refined more
                    Dbg.Err($"No entity type matches when attempting to run system {dec}!");
                }
            }
        }

        public Environment()
        {
            // I'm not worried about singleton inheritance yet
            var singletonTypes = Dec.Database<ComponentDec>.List.Where(cd => cd.singleton).OrderBy(cd => cd.DecName).ToArray();
            singletonLookup = singletonTypes.Select((cd, i) => (type: cd.GetComputedType(), i)).ToDictionary(x => x.type, x => x.i);

            singletons = new object[singletonTypes.Length];
            foreach ((var dec, var i) in singletonTypes.Select((cd, i) => (cd, i)))
            {
                singletons[i] = Activator.CreateInstance(dec.GetComputedType());
            }

            // create tranches
            tranches = new Tranche[Dec.Database<EntityDec>.List.Length];
            foreach ((var index, var entity) in Dec.Database<EntityDec>.List.OrderBy(ed => ed.DecName).Select((ed, i) => (i, ed)))
            {
                tranches[index].entries = new List<Entity>();
                tranches[index].components = new Array[entity.components.Count];
                for (int i = 0; i < entity.components.Count; ++i)
                {
                    // arbitrarily hardcoded starting size; should this be bigger? smaller? who can say! it is a mystery
                    // probably shouldn't actually matter tbqh
                    tranches[index].components[i] = Array.CreateInstance(entity.components[i].GetComputedType(), 16);
                }
            }

            // create status
            status = Status.Idle;
        }

        private object[] FillComponents(EntityDec dec, object[] providedComponents)
        {
            bool[] used = null;
            if (providedComponents != null)
            {
                used = new bool[providedComponents.Length];
            }

            // this is kind of inefficient but I'm just living with the perf hit for the time being
            var components = new object[dec.components.Count];
            for (int i = 0; i < dec.components.Count; ++i)
            {
                // see if we have a provided component
                int match = -1;

                if (providedComponents != null)
                {
                    for (int j = 0; j < providedComponents.Length; ++j)
                    {
                        if (dec.components[i].GetComputedType().IsAssignableFrom(providedComponents[j].GetType()))
                        {
                            if (match == -1)
                            {
                                match = j;
                            }
                            else
                            {
                                Dbg.Err($"Ambiguity in component match for {dec.DecName}; {dec.components[i].GetComputedType()} matches both {providedComponents[match].GetType()} and {providedComponents[j].GetType()}");
                            }
                        }
                    }
                }

                if (match != -1)
                {
                    components[i] = providedComponents[match];
                    used[match] = true;
                }
                else
                {
                    components[i] = Activator.CreateInstance(dec.components[i].GetComputedType());
                }
            }

            if (used != null)
            {
                for (int i = 0; i < used.Length; ++i)
                {
                    if (!used[i])
                    {
                        Dbg.Err($"Unused component {providedComponents[i].GetType()} provided for {dec.DecName}");
                    }
                }
            }

            return components;
        }

        public Entity Add(EntityDec dec, object[] providedComponents = null)
        {
            var resultComponents = FillComponents(dec, providedComponents);

            switch (status)
            {
                case Status.Idle:
                    return AddNow(dec, resultComponents);
                case Status.Processing:
                    var entityDeferred = new EntityDeferred();
                    entityDeferred.dec = dec;

                    var tranche = new Tranche();
                    tranche.entries = new List<Entity>();
                    tranche.components = new Array[dec.components.Count];

                    // create a new set of components
                    for (int i = 0; i < dec.components.Count; ++i)
                    {
                        // currently not worrying about minmaxing efficiency here
                        tranche.components[i] = Array.CreateInstance(dec.components[i].GetComputedType(), 1);
                        tranche.components[i].SetValue(resultComponents[i], 0);
                    }

                    // do this late because it's a struct
                    entityDeferred.tranche = tranche;

                    phaseEndActions.Add(() =>
                    {
                        var currentComponents = new object[dec.components.Count];
                        // copy components back from the tranche
                        for (int i = 0; i < dec.components.Count; ++i)
                        {
                            currentComponents[i] = tranche.components[i].GetValue(0);
                        }
                        entityDeferred.replacement = AddNow(dec, currentComponents);
                    });
                    return new Entity(entityDeferred);
                default:
                    Assert.IsTrue(false);
                    return default;
            }
        }

        private Entity AddNow(EntityDec dec, object[] components)
        {
            var tranche = tranches[dec.index];
            var trancheId = tranche.entries.Count();

            for (int i = 0; i < dec.components.Count; ++i)
            {
                // we want to add this to the end, but it's a static array so (1) that's not "the end", and (2) we may need to realloc
                if (tranche.components[i].Length <= trancheId)
                {
                    // we need to realloc :(
                    var newArray = Array.CreateInstance(dec.components[i].GetComputedType(), trancheId * 2);
                    Array.Copy(tranche.components[i], newArray, trancheId);
                    tranche.components[i] = newArray;
                }

                tranche.components[i].SetValue(components[i], trancheId);
            }

            // now allocate the actual entity ID
            int id;
            if (entityFreeList.Count > 0)
            {
                id = entityFreeList[entityFreeList.Count - 1];
                entityFreeList.RemoveAt(entityFreeList.Count - 1);
                entityLookup[id] = new EntityLookup() { dec = dec, index = trancheId, gen = entityLookup[id].gen + 1 };
            }
            else
            {
                id = entityLookup.Count;
                entityLookup.Add(new EntityLookup() { dec = dec, index = trancheId, gen = 1 });
            }

            var entity = new Entity(id, entityLookup[id].gen);
            tranche.entries.Add(entity);

            return entity;
        }

        public void Remove(Entity entity)
        {
            if (entity == default)
            {
                Dbg.Err("Attempted to remove default entity");
                return;
            }

            switch (status)
            {
                case Status.Idle:
                    RemoveNow(entity);
                    break;
                case Status.Processing:
                    phaseEndActions.Add(() =>
                    {
                        RemoveNow(entity);
                    });
                    break;
                default:
                    Assert.IsTrue(false);
                    break;
            }
        }

        private void RemoveNow(Entity entity)
        {
            (EntityLookup lookup, int id) = LookupFromEntity(entity);
            if (id == -1)
            {
                Dbg.Err($"Attempted to remove entity {entity} that doesn't exist");
                return;
            }

            // send appropriate messages
            entity.OnRemove();

            // we want to keep each tranche contiguous

            var tranche = tranches[lookup.dec.index];
            if (lookup.index == tranche.entries.Count - 1)
            {
                // if we're removing from the end, we just remove it; easy!
                tranche.entries.RemoveAt(lookup.index);

                // also, the same for every list
                // we kinda just reset it in order to ensure we "garbage-collect" stuff
                for (int i = 0; i < tranche.components.Length; ++i)
                {
                    tranche.components[i].SetValue(lookup.dec.components[i].GetComputedType().CreateDefault(), lookup.index);
                }
            }
            else
            {
                // if we're removing from the center, we move the end item into the removed item's place
                // three cheers for O(1)
                int endEntry = tranche.entries.Count - 1;
                tranche.entries[lookup.index] = tranche.entries[endEntry];
                tranche.entries.RemoveAt(endEntry);

                // also, the same for every list
                for (int i = 0; i < tranche.components.Length; ++i)
                {
                    tranche.components[i].SetValue(tranche.components[i].GetValue(endEntry), lookup.index);
                    tranche.components[i].SetValue(lookup.dec.components[i].GetComputedType().CreateDefault(), endEntry);
                }

                // now patch up the entity lookup table for the item we just swapped in
                var replacedId = tranche.entries[lookup.index].id;
                entityLookup[replacedId] = new EntityLookup() { dec = lookup.dec, index = lookup.index, gen = entityLookup[replacedId].gen };
            }

            // wipe the entity we deleted from the lookup table
            // important that we bump the generation to ensure we never repeat generations!
            entityLookup[id] = new EntityLookup() { dec = null, index = -1, gen = entityLookup[id].gen + 1 };
            entityFreeList.Add(id);
        }

        private (EntityLookup lookup, int id) LookupFromEntity(Entity entity)
        {
            if (entity.id < 0 || entity.id >= entityLookup.Count)
            {
                return (new EntityLookup(), -1);
            }

            // compare generations; if this isn't us, it's not real
            var lookup = entityLookup[entity.id];
            if (lookup.gen != entity.gen)
            {
                return (new EntityLookup(), -1);
            }

            return (lookup, entity.id);
        }

        internal (EntityDec dec, Tranche tranche, int index) Get(Entity entity)
        {
            var (lookup, id) = LookupFromEntity(entity);
            if (id == -1)
            {
                return (null, default, -1);
            }

            return (lookup.dec, tranches[lookup.dec.index], lookup.index);
        }

        public T Singleton<T>()
        {
            if (!singletonLookup.ContainsKey(typeof(T)))
            {
                Dbg.Err($"Attempted to access singleton {typeof(T)} that doesn't exist");
                return default;
            }

            return (T)singletons[singletonLookup[typeof(T)]];
        }

        public T SingletonRO<T>()
        {
            return Singleton<T>();
        }

        public T SingletonRW<T>()
        {
            return Singleton<T>();
        }

        public void SingletonSet<T>(T newSingleton)
        {
            if (!singletonLookup.ContainsKey(typeof(T)))
            {
                Dbg.Err($"Attempted to set singleton {typeof(T)} that doesn't exist");
                return;
            }

            singletons[singletonLookup[typeof(T)]] = newSingleton;
        }

        public void Process(ProcessDec process)
        {
            if (Current.Value != this && Current.Value != null)
            {
                Dbg.Wrn("Started Environment.Process with a different Environment active; this is probably a mistake");
            }
            using var scope = new Scope(this);

            if (status != Status.Idle)
            {
                Dbg.Err($"Trying to run process while the world is in {status} state; should be {Status.Idle} state");
            }

            if (process == null)
            {
                Dbg.Err("Process is null!");
                return;
            }

            if (process.order == null)
            {
                Dbg.Err("Process Order is null!");
                return;
            }

            foreach (var system in process.order)
            {
                status = Status.Processing;

                {
                    //using var p = Prof.Sample(name: system.DecName);

                    system.process(tranches, singletons);
                }

                status = Status.Idle;

                if (phaseEndActions.Count != 0)
                {
                    var actions = new List<Action>(phaseEndActions);
                    phaseEndActions.Clear();

                    foreach (var action in actions)
                    {
                        action();
                    }

                    Assert.IsEmpty(phaseEndActions);
                }
            }
        }

        public void Record(Dec.Recorder recorder)
        {
            // make sure we're not actively doing things
            Assert.AreEqual(status, Status.Idle);
            Assert.AreEqual(phaseEndActions.Count, 0);

            // so that our children can use cows
            using var scope = new Scope(this);

            recorder.Record(ref tranches, "tranches");
            recorder.Record(ref entityLookup, "entityLookup");
            recorder.Record(ref entityFreeList, "entityFreeList");
            recorder.Record(ref singletons, "singletons");
            recorder.Record(ref singletonLookup, "singletonLookup");
        }
    }
}
