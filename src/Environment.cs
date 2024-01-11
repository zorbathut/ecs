
using Dec;

namespace Ghi
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data.Common;

    using System.Linq;

    public class Environment : Dec.IRecordable
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
            public List<Entity> entries;
            public IList[] components;

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
                dec.process = (tranches, singletons) =>
                {
                    var method = dec.method;
                    var parameters = method.GetParameters();

                    {
                        // accumulate our entire match DB
                        var parameterDirectMatches = parameters
                            .Select(param => allComponents
                                .Select((c, i) => ( c, i ))
                                .Where(c => param.ParameterType.IsAssignableFrom(c.c.type))
                                .ToArray())
                            .ToArray();

                        // first, see if this can be mapped to all-singleton
                        if (parameterDirectMatches.All(matches => matches.Length == 1 && matches[0].c.singleton))
                        {
                            // it can!
                            // somewhat surprised tbqh
                            object[] args = new object[parameters.Length];
                            for (int i = 0; i < parameters.Length; ++i)
                            {
                                args[i] = singletons[
                                    allSingletons.FirstIndexOf(singleton =>
                                        singleton == parameterDirectMatches[i][0].c)];
                            }

                            // done; if we're unambiguously all singleton, then we can't possibly have non-singleton items, can we!
                            try
                            {
                                method.Invoke(null, args);
                            }
                            catch (Exception e)
                            {
                                Dbg.Ex(e);
                            }

                            return;
                        }
                        else if (parameterDirectMatches.Any(matches => matches.All(m => !m.c.singleton)))
                        {
                            // definitely can't be a singleton match, so we're OK with it
                        }
                        else if (parameterDirectMatches.Any(matches => matches.Length > 1))
                        {
                            var ambiguity = string.Join("; ", parameters.Zip(parameterDirectMatches, (param, matches) => ( param, matches ))
                                .Where(x => x.matches.Length > 1)
                                .Select(x => $"{x.param.ParameterType} matches [{string.Join(", ", x.matches.Select(m => m.c.type.ToString()))}]"));

                            Dbg.Err($"{dec}: Ambiguity in singleton scan! {ambiguity}");
                        }
                    }

                    bool found = false;

                    // for each tranche . . .
                    for (int i = 0; i < allEntities.Length; ++i)
                    {
                        // see if we can find an unambiguous mapping, including all singletons and every one of our component types
                        var availableComponents = allSingletons.Concat(allEntities[i].components).ToArray();
                        var parameterTrancheMatches = parameters
                            .Select(param => availableComponents
                                .Select((c, i) => (c.type, i))
                                .Concat(Enumerable.Repeat((typeof(Entity), -1), 1))
                                .Where(c => param.ParameterType.IsAssignableFrom(c.Item1))
                                .Select(c => c.Item2)
                                .ToArray())
                            .ToArray();

                        if (parameterTrancheMatches.All(matches => matches.Length == 1))
                        {
                            // we have a match!
                            found = true;

                            // our singletons are first, followed by possible components, so we need to map these up as appropriate
                            object[] args = new object[parameters.Length];

                            // as we do this, we build a remap array for us to rapidly remap things
                            List<(int from, int to)> remapsWorking = new List<(int from, int to)>();
                            for (int j = 0; j < parameters.Length; ++j)
                            {
                                if (parameterTrancheMatches[j][0] == -1)
                                {
                                    // this is Entity
                                    remapsWorking.Add((-1, j));
                                }
                                else
                                if (parameterTrancheMatches[j][0] < singletons.Length)
                                {
                                    // this is a singleton
                                    args[j] = singletons[parameterTrancheMatches[j][0]];
                                }
                                else
                                {
                                    // this is a component
                                    remapsWorking.Add(( parameterTrancheMatches[j][0] - singletons.Length, j ));
                                }
                            }

                            // compile this down for efficiency
                            var remaps = remapsWorking.ToArray();

                            // do it
                            for (int index = 0; index < tranches[i].entries.Count; ++index)
                            {
                                // remap the components
                                for (int j = 0; j < remaps.Length; ++j)
                                {
                                    int from = remaps[j].from;
                                    if (from == -1)
                                    {
                                        args[remaps[j].to] = tranches[i].entries[index];
                                    }
                                    else
                                    {
                                        args[remaps[j].to] = tranches[i].components[remaps[j].from][index];
                                    }
                                }

                                // invoke the method
                                try
                                {
                                    method.Invoke(null, args);
                                }
                                catch (Exception e)
                                {
                                    Dbg.Ex(e);
                                }
                            }
                        }
                        else if (parameterTrancheMatches.Any(matches => matches.Length > 1))
                        {
                            var ambiguity = string.Join("; ", parameters.Zip(parameterTrancheMatches, (param, matches) => ( param, matches ))
                                .Where(x => x.matches.Length > 1)
                                .Select(x => $"{x.param.ParameterType} matches [{string.Join(", ", x.matches.Select(m => m.ToString()))}]"));

                            Dbg.Err($"{dec}: Ambiguity in entity scan! {ambiguity}");
                        }
                    }

                    if (!found)
                    {
                        // this can really be refined more
                        Dbg.Err($"No entity type matches when attempting to run system {dec}!");
                    }
                };
            }
        }

        public Environment()
        {
            // I'm not worried about singleton inheritance yet
            var singletonTypes = Dec.Database<ComponentDec>.List.Where(cd => cd.singleton).OrderBy(cd => cd.DecName).ToArray();
            singletonLookup = singletonTypes.Select((cd, i) => (cd.type, i)).ToDictionary(x => x.type, x => x.i);

            singletons = new object[singletonTypes.Length];
            foreach ((var dec, var i) in singletonTypes.Select((cd, i) => (cd, i)))
            {
                singletons[i] = Activator.CreateInstance(dec.type);
            }

            // create tranches
            tranches = new Tranche[Dec.Database<EntityDec>.List.Length];
            foreach ((var index, var entity) in Dec.Database<EntityDec>.List.OrderBy(ed => ed.DecName).Select((ed, i) => (i, ed)))
            {
                tranches[index].entries = new List<Entity>();
                tranches[index].components = new IList[entity.components.Count];
                for (int i = 0; i < entity.components.Count; ++i)
                {
                    tranches[index].components[i] = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entity.components[i].type));
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
                        if (dec.components[i].type.IsAssignableFrom(providedComponents[j].GetType()))
                        {
                            if (match == -1)
                            {
                                match = j;
                            }
                            else
                            {
                                Dbg.Err($"Ambiguity in component match for {dec.DecName}; {dec.components[i].type} matches both {providedComponents[match].GetType()} and {providedComponents[j].GetType()}");
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
                    components[i] = Activator.CreateInstance(dec.components[i].type);
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
                    tranche.components = new List<object>[dec.components.Count];

                    // create a new set of components
                    for (int i = 0; i < dec.components.Count; ++i)
                    {
                        // currently not worrying about minmaxing efficiency here
                        tranche.components[i] = new List<object>();
                        tranche.components[i].Add(resultComponents[i]);
                    }

                    // do this late because it's a struct
                    entityDeferred.tranche = tranche;

                    phaseEndActions.Add(() =>
                    {
                        var currentComponents = new object[dec.components.Count];
                        // copy components back from the tranche
                        for (int i = 0; i < dec.components.Count; ++i)
                        {
                            currentComponents[i] = tranche.components[i][0];
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
                tranche.components[i].Add(components[i]);
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

            // we want to keep each tranche contiguous

            var tranche = tranches[lookup.dec.index];
            if (lookup.index == tranche.entries.Count - 1)
            {
                // if we're removing from the end, we just remove it; easy!
                tranche.entries.RemoveAt(lookup.index);

                // also, the same for every list
                for (int i = 0; i < tranche.components.Length; ++i)
                {
                    tranche.components[i].RemoveAt(lookup.index);
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
                    tranche.components[i][lookup.index] = tranche.components[i][endEntry];
                    tranche.components[i].RemoveAt(endEntry);
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
            status = Status.Processing;

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

            recorder.Record(ref tranches, "tranches");
            recorder.Record(ref entityLookup, "entityLookup");
            recorder.Record(ref entityFreeList, "entityFreeList");
            recorder.Record(ref singletons, "singletons");
            recorder.Record(ref singletonLookup, "singletonLookup");
        }
    }
}
