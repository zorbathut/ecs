
namespace Ghi;

[Dec.CloneStructPiecewise]   // copies value's ref and revision, just like we want
public struct Cow<T> : Dec.IRecordable where T : class, new()
{
    private T value;
    private long revision;

    // I'm not really happy with this revision behavior
    // the problem is that if we're loading through recorder, we may not have an active environment
    // which means we need to fake a revision somehow
    // so we choose one that can't possibly be valid (hello, 2^64 environments georg, how are you doing)
    // but it's still messy that we're forcing unnecessary copies on the first RW
    // and we can't even treat -1 as "safe" because we might get copied before it gets updated
    // I guess it's probably fine because the entire point of the COW system is that the copies should be fast
    // but still, it's a little ugly.
    public Cow()
    {
        value = new T();
        revision = Environment.Current.Value?.UniqueId ?? -1;
    }

    public Cow(T value)
    {
        this.value = value;
        revision = Environment.Current.Value?.UniqueId ?? -1;
    }

    public long GetRevision()
    {
        return revision;
    }

    public T GetRW()
    {
        var env = Environment.Current.Value;
        if (env == null)
        {
            Dbg.Err("Attempted to get a RW version of a COW structure without a working environment!");
            return value;
        }

        if (env.UniqueId == revision)
        {
            // we're good! we have the only unique version of this
            return value;
        }

        // gotta replace it ;.;
        // in theory it's possible we have the only ref remaining to it, but it's hard to detect that so I'm not
        value = Dec.Recorder.Clone(value);
        revision = env.UniqueId;
        return value;
    }

    public T GetRO()
    {
        // it hasn't changed, and we won't change it, so everything is fine
        // yay
        return value;
    }

    public void Record(Dec.Recorder recorder)
    {
        recorder.RecordAsThis(ref value);
        // revision is irrelevant, it's just a marker for the COW system
    }
}
