using System;

namespace Ghi
{
    internal static class Dbg
    {
        internal static void Inf(string format)
        {
            Dec.Config.InfoHandler(format);
        }

        internal static void Wrn(string format)
        {
            Dec.Config.WarningHandler(format);
        }

        internal static void Err(string format)
        {
            Dec.Config.ErrorHandler(format);
        }

        internal static void Ex(Exception e)
        {
            Dec.Config.ExceptionHandler(e);
        }
    }
}