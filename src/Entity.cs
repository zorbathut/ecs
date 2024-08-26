
namespace Ghi
{
    using System;
    using System.Linq;

    [System.Diagnostics.DebuggerTypeProxy(typeof(DebugView))]
    public struct Entity : Dec.IRecordable, IEquatable<Entity>
    {
        internal int id;
        internal int hashCode; // put this here for better alignment
        internal long gen; // 32-bit gives us 2.1 years, and someone is gonna want to run a server longer than that

        private Environment.EntityDeferred deferred;

        public Entity()
        {
            this.id = 0;
            this.gen = 0;
            this.deferred = null;
            this.hashCode = 0x7e117a1e; // random arbitrary hex word
        }
        internal Entity(Environment.EntityDeferred deferred, int hashCode)
        {
            this.id = 0;
            this.gen = 0;
            this.hashCode = hashCode;
            this.deferred = deferred;

            // this data structure gives me a headache
            this.deferred.tranche.entries.Add(this);
        }
        internal Entity(int id, long gen, int hashCode)
        {
            this.id = id;
            this.gen = gen;
            this.hashCode = hashCode;
            this.deferred = null;
        }

        // nicely explicit for people who like that sort of thing
        public static Entity Invalid
        {
            get => new Entity();
        }

        internal void Resolve()
        {
            if (deferred != null && deferred.replacement.gen != 0)
            {
                id = deferred.replacement.id;
                gen = deferred.replacement.gen;
                deferred = null;
            }
        }

        public bool IsValid()
        {
            var env = Environment.Current.Value;
            if (env == null)
            {
                Dbg.Err($"Attempted to get entity while env is unavailable");
                return default;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            return dec != null;
        }

        public bool HasComponent(ComponentDec t)
        {
            var env = Environment.Current.Value;
            if (env == null)
            {
                Dbg.Err($"Attempted to get entity while env is unavailable");
                return false;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            if (dec == null)
            {
                return false;
            }

            return dec.HasComponent(t.GetComputedType());
        }

        public bool HasComponent<T>()
        {
            var env = Environment.Current.Value;
            if (env == null)
            {
                Dbg.Err($"Attempted to get entity while env is unavailable");
                return false;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            if (dec == null)
            {
                return false;
            }

            return dec.HasComponent(typeof(T));
        }

        public T Component<T>()
        {
            if (typeof(T).IsGenericType && typeof(T).BaseType == typeof(Cow<>))
            {
                // no this kinda just doesn't work right now sorry
                // (needs to return a ref, or do the COW analysis internally)
                Dbg.Err("Returning COW types from entities is not supported yet, sorry");
                return default;
            }

            var env = Environment.Current.Value;
            if (env == null)
            {
                Dbg.Err($"Attempted to get entity while env is unavailable");
                return default;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            if (dec == null)
            {
                Dbg.Err($"Attempted to get dead entity {this}");
                return default;
            }

            // I don't like that this boxes
            var result = dec.GetComponentFrom(typeof(T), tranche, index);
            if (result == null)
            {
                return default;
            }

            return (T)result;
        }

        public T ComponentRO<T>()
        {
            return Component<T>();
        }

        public T ComponentRW<T>()
        {
            return Component<T>();
        }

        public T TryComponent<T>()
        {
            if (typeof(T).IsGenericType && typeof(T).BaseType == typeof(Cow<>))
            {
                // no this kinda just doesn't work right now sorry
                // (needs to return a ref, or do the COW analysis internally)
                Dbg.Err("Returning COW types from entities is not supported yet, sorry");
                return default;
            }

            var env = Environment.Current.Value;
            if (env == null)
            {
                // yes this is still an error
                Dbg.Err($"Attempted to get entity while env is unavailable");
                return default;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            if (dec == null)
            {
                return default;
            }

            // I don't like that this boxes
            var result = dec.TryGetComponentFrom(typeof(T), tranche, index);
            if (result == null)
            {
                return default;
            }

            return (T)result;
        }

        public T TryComponentRO<T>()
        {
            return TryComponent<T>();
        }

        public T TryComponentRW<T>()
        {
            return TryComponent<T>();
        }

        internal void OnRemove()
        {
            var env = Environment.Current.Value;
            if (env == null)
            {
                Dbg.Err($"Internal error: Attempted to remove entity while env is unavailable");
                return;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            if (dec == null)
            {
                Dbg.Err($"Internal error: Attempted to remove entity that can't be found");
                return;
            }

            foreach (var c in dec.components)
            {
                var typ = c.GetComputedType();

                if (typeof(IOnRemove).IsAssignableFrom(typ))
                {
                    var comp = dec.GetComponentFrom(typ, tranche, index);
                    ((IOnRemove)comp).OnRemove(this);
                }

                if (typ.IsGenericType && typ.BaseType == typeof(Cow<>) && typeof(IOnRemove).IsAssignableFrom(typ.GetGenericArguments()[0]))
                {
                    Dbg.Err("COW'ed IOnRemove is not supported yet, sorry");
                }
            }
        }

        public override string ToString()
        {
            string suffix = Environment.EntityToString != null ? (":" + Environment.EntityToString(this)) : "";
            switch (GetStatus())
            {
                case Status.Null:
                    return "[Entity:Null]";
                case Status.EnvUnavailable:
                    return $"[Entity:EnvUnavailable{suffix}]";
                case Status.Deferred:
                    return $"[Entity:{GetEntityDec().DecName}:Deferred:{deferred.GetHashCode()}{suffix}]";
                case Status.Deleted:
                    return $"[Entity:Deleted{suffix}]";
                case Status.Active:
                    return $"[Entity:{GetEntityDec().DecName}:{id}:{gen}{suffix}]";
                default:
                    Dbg.Err("Internal error");
                    return "[Entity:InternalError{suffix}]";
            }
        }

        public EntityIdentifier GetEntityIdentifier()
        {
            Resolve();
            Assert.IsTrue(deferred == null);

            return new EntityIdentifier(id, gen);
        }

        public EntityDec GetEntityDec()
        {
            var env = Environment.Current.Value;
            if (env == null)
            {
                // yes this is still an error
                Dbg.Err($"Attempted to get entity while env is unavailable");
                return default;
            }

            Resolve();

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            return dec;
        }

        public static bool operator==(Entity a, Entity b)
        {
            return a.Equals(b);
        }

        public static bool operator!=(Entity a, Entity b)
        {
            return !a.Equals(b);
        }

        public bool Equals(Entity other)
        {
            if (deferred != null || other.deferred != null)
            {
                return deferred == other.deferred;
            }

            return id == other.id && gen == other.gen;
        }

        public override bool Equals(object obj)
        {
            if (obj is Entity)
            {
                return Equals((Entity)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public void Record(Dec.Recorder recorder)
        {
            Resolve();
            Assert.IsTrue(deferred == null);

            recorder.Record(ref id, "id");
            recorder.Record(ref gen, "gen");
            recorder.Record(ref hashCode, "hashCode");
        }

        internal enum Status
        {
            Null,
            EnvUnavailable,
            Deferred,
            Active,
            Deleted,
        }
        internal Status GetStatus()
        {
            if (id == 0 && gen == 0 && deferred == null)
            {
                return Status.Null;
            }

            var env = Environment.Current.Value;
            if (env == null)
            {
                return Status.EnvUnavailable;
            }

            Resolve();

            if (id == 0 && gen == 0)
            {
                return Status.Null;
            }

            if (deferred != null)
            {
                return Status.Deferred;
            }

            (var dec, var tranche, var index) = deferred?.Get() ?? env.Get(this);
            if (dec == null)
            {
                return Status.Deleted;
            }

            return Status.Active;
        }

        internal class DebugView
        {
            private Entity entity;

            public int id;
            public long gen;
            public DebugView(Entity entity)
            {
                this.entity = entity;

                this.id = entity.id;
                this.gen = entity.gen;
            }

            public Entity.Status Status
            {
                get
                {
                    return entity.GetStatus();
                }
            }

            public object[] Components
            {
                get
                {
                    var env = Environment.Current.Value;
                    if (env == null)
                    {
                        return null;
                    }

                    entity.Resolve();

                    (var dec, var tranche, var index) = entity.deferred?.Get() ?? env.Get(entity);
                    if (dec == null)
                    {
                        return null;
                    }

                    return dec.components.Select(c => dec.GetComponentFrom(c.GetComputedType(), tranche, index)).ToArray();
                }
            }
        }
    }

    [System.Diagnostics.DebuggerTypeProxy(typeof(EntityComponent<>.DebugView))]
    public struct EntityComponent<T> : Dec.IRecordable
    {
        private Entity entity;

        public EntityComponent()
        {
            entity = new Entity();
        }

        internal EntityComponent(Entity entity)
        {
            this.entity = entity;
        }

        public static EntityComponent<T> From(Entity entity)
        {
            return new EntityComponent<T>(entity);
        }

        public bool IsValid()
        {
            // this does IsValid() also
            return entity.HasComponent<T>();
        }

        public T Get()
        {
            return entity.Component<T>();
        }

        public T GetRO()
        {
            return Get();
        }

        public T GetRW()
        {
            return Get();
        }

        public T TryGet()
        {
            return entity.TryComponent<T>();
        }

        public T TryGetRO()
        {
            return TryGet();
        }

        public T TryGetRW()
        {
            return TryGet();
        }

        public static bool operator==(EntityComponent<T> a, EntityComponent<T> b)
        {
            return a.entity == b.entity;
        }

        public static bool operator!=(EntityComponent<T> a, EntityComponent<T> b)
        {
            return a.entity != b.entity;
        }

        public override bool Equals(object obj)
        {
            if (obj is EntityComponent<T> o)
            {
                return entity == o.entity;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return entity.GetHashCode();
        }

        public override string ToString()
        {
            return entity.ToString();
        }

        public void Record(Dec.Recorder recorder)
        {
            recorder.RecordAsThis(ref entity);
        }

        internal class DebugView
        {
            private EntityComponent<T> component;

            public DebugView(EntityComponent<T> component)
            {
                this.component = component;
            }

            public Entity.Status Status
            {
                get
                {
                    return component.entity.GetStatus();
                }
            }

            public object Component
            {
                get
                {
                    return component.TryGetRO();
                }
            }
        }
    }

    public struct EntityIdentifier
    {
        private int id;
        private long gen;

        public EntityIdentifier(int id, long gen)
        {
            this.id = id;
            this.gen = gen;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode() ^ gen.GetHashCode();
        }

        public override string ToString()
        {
            return $"[{id}:{gen}]";
        }
    }
}
