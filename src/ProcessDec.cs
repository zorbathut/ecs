namespace Ghi
{
    using System;
    using System.Linq;

    [Dec.SetupDependsOn(typeof(SystemDec))]
    public class ProcessDec : Dec.Dec
    {
        public SystemDec[] order;

        public override void ConfigErrors(Action<string> reporter)
        {
            base.ConfigErrors(reporter);

            if (order == null)
            {
                reporter("No defined order");
            }

            if (order.Any(s => s?.method == null))
            {
                reporter("Order contains null or invalid systems; cleaning");
                order = order.Where(s => s?.method == null).ToArray();
            }
        }
    }
}
