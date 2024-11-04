using System;
using System.Collections.Generic;

namespace Ghi
{
    internal static class Util
    {
        internal static int FirstIndexOf<T>(this IEnumerable<T> enumerable, Func<T, bool> func)
        {
            int index = 0;
            var enumerator = enumerable.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (func(enumerator.Current))
                {
                    return index;
                }

                ++index;
            }

            return -1;
        }

        internal static object CreateDefault(this Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            else
            {
                return null;
            }
        }
    }
}
