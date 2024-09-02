using System.Collections.Concurrent;

namespace Ghi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class EntityDec : Dec.Dec
    {
        public List<ComponentDec> components;

        // this needs to be deterministic, so right now we're not using Dec for it because Dec's isn't
        [NonSerialized] internal int index;

        internal ConcurrentDictionary<Type, Func<Environment.Tranche, int, object>> componentGetters = new();
        internal ConcurrentDictionary<Type, Func<Environment.Tranche, int, object>> tryComponentGetters = new();
        internal ConcurrentDictionary<Type, Action<Environment.Tranche, int, object>> componentSetters = new();
        internal object GetComponentFrom(Type type, Environment.Tranche tranche, int index)
        {
            if (!componentGetters.TryGetValue(type, out var getter))
            {
                (getter, var _, var _) = CreateGetters(type);
            }

            return getter(tranche, index);
        }

        internal object TryGetComponentFrom(Type type, Environment.Tranche tranche, int index)
        {
            if (!tryComponentGetters.TryGetValue(type, out var tryGetter))
            {
                (var _, tryGetter, var _) = CreateGetters(type);
            }

            return tryGetter(tranche, index);
        }

        internal bool HasComponent(Type type)
        {
            // should really cache this
            return components.Any(c => type.IsAssignableFrom(c.GetComputedType()));
        }

        internal void SetComponentOn(Type type, Environment.Tranche tranche, int index, object value)
        {
            if (!componentSetters.TryGetValue(type, out var setter))
            {
                (var _, var _, setter) = CreateGetters(type);
            }

            setter(tranche, index, value);
        }

        private (Func<Environment.Tranche, int, object> getter, Func<Environment.Tranche, int, object> tryGetter, Action<Environment.Tranche, int, object> setter) CreateGetters(Type type)
        {
            Func<Environment.Tranche, int, object> getter;
            Func<Environment.Tranche, int, object> tryGetter;
            Action<Environment.Tranche, int, object> setter;

            // look over our components and see if we have something that makes sense
            // note: if this is slow, this might be another good target for runtime codegen
            // especially so we can kill boxing
            var matches = components.Select((c, i) => (type: c.GetComputedType(), i: i)).Where(c => type.IsAssignableFrom(c.type)).ToArray();
            if (matches.Length == 1)
            {
                var cindex = matches[0].i;
                getter = (tranche, index) => tranche.components[cindex].GetValue(index);
                tryGetter = getter;
                setter = (tranche, index, value) => tranche.components[cindex].SetValue(value, index);
            }
            else if (matches.Length == 0)
            {
                getter = (tranche, index) =>
                {
                    Dbg.Err($"Cannot find match for component {type} in entity {this}");
                    return null;
                };
                tryGetter = (tranche, index) => null;
                setter = (tranche, index, value) =>
                {
                    Dbg.Err($"Cannot find match for component {type} in entity {this}");
                };
            }
            else
            {
                getter = (tranche, index) =>
                {
                    Dbg.Err($"Ambiguous component {type} in entity {this}; could be any of {string.Join(", ", matches.Select(m => m.type))}");
                    return null;
                };
                tryGetter = (tranche, index) => null;
                setter = (tranche, index, value) =>
                {
                    Dbg.Err($"Ambiguous component {type} in entity {this}; could be any of {string.Join(", ", matches.Select(m => m.type))}");
                };
            }

            componentGetters.TryAdd(type, getter);
            tryComponentGetters.TryAdd(type, tryGetter);
            componentSetters.TryAdd(type, setter);

            return (getter, tryGetter, setter);
        }

        public override void ConfigErrors(Action<string> reporter)
        {
            base.ConfigErrors(reporter);

            if (components == null || components.Count == 0)
            {
                reporter("No defined components");
            }

            if (components.Any(c => c == null))
            {
                reporter("Null component");
            }

            if (components.Any(c => c?.type == null))
            {
                reporter("Cleaning component list from invalid components");
                components.RemoveAll(c => c?.type == null);
            }
        }
    }
}
