using System;

namespace Ghi
{
    public class ComponentDec : Dec.Dec
    {
        public Type type = null;
        public bool singleton = false;
        public bool cow = false;

        [Dec.Index]
        public int index;

        internal Type GetComputedType()
        {
            if (cow)
            {
                return typeof(Cow<>).MakeGenericType(type);
            }
            else
            {
                return type;
            }
        }

        public override void ConfigErrors(Action<string> reporter)
        {
            base.ConfigErrors(reporter);

            if (type == null)
            {
                reporter("No defined type");
                return;
            }

            if (type.IsValueType && singleton)
            {
                reporter("Singleton components cannot currently be structs or other value types");
            }
        }
    }
}
