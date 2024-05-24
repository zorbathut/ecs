namespace Ghi;

using System;

internal static class Assert
{
    public static void IsNull(object val)
    {
        if (val != null)
        {
            Dbg.Err($"Value is not null: {val}");
        }
    }

    public static void IsNotNull(object val)
    {
        if (val == null)
        {
            Dbg.Err("Value is null");
        }
    }

    public static void IsTrue(bool val, string msg = null)
    {
        if (!val)
        {
            Dbg.Err(msg ?? "Value is false");
        }
    }

    public static void IsEmpty<T>(System.Collections.Generic.ICollection<T> collection)
    {
        if (collection.Count != 0)
        {
            Dbg.Err($"Collection is not empty: {collection}");
        }
    }

    public static void AreSame(object expected, object actual)
    {
        if (expected != actual)
        {
            Dbg.Err($"Values do not match: expected {expected}, actual {actual}");
        }
    }

    public static void AreEqual<T>(T expected, T actual)
    {
        // both of these could inherit from T, so let's just verify no inheritance games are going on here
        // at least if they end up different
        if (expected.GetType() != actual.GetType())
        {
            Dbg.Err($"Types do not match: expected {expected.GetType()}, actual {actual.GetType()}");
            return;
        }

        // arrays should really have Equals overridden
        if (expected is Array)
        {
            // can't use SequenceEqual because we don't have typing info
            var expectedArray = (Array)(object)expected;
            var actualArray = (Array)(object)actual;

            if (expectedArray.Length != actualArray.Length)
            {
                Dbg.Err($"Array lengths do not match: expected {expectedArray.Length}, actual {actualArray.Length}");
                return;
            }

            for (int i = 0; i < expectedArray.Length; i++)
            {
                if (!expectedArray.GetValue(i).Equals(actualArray.GetValue(i)))
                {
                    Dbg.Err($"Values do not match at index {i}: expected {expectedArray.GetValue(i)}, actual {actualArray.GetValue(i)}");
                    return;
                }
            }

            return;
        }

        if (!expected.Equals(actual))
        {
            Dbg.Err($"Values do not match: expected {expected}, actual {actual}");
        }
    }
}
