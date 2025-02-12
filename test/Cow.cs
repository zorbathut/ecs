
namespace Ghi.Test;

using NUnit.Framework;

[TestFixture]
public class Cow : Base
{
    [Dec.StaticReferences]
    public static class Decs
    {
        static Decs() { Dec.StaticReferencesAttribute.Initialized(); }

        public static EntityDec StructEntity;
        public static EntityDec ClassEntity;
        public static EntityDec CowEntity;

        public static ProcessDec CowInitProcess;
        public static ProcessDec CowROProcess;
        public static ProcessDec CowRWProcess;
    }

    public struct IntStructComponent : Dec.IRecordable
    {
        public int value;

        public void Record(Dec.Recorder recorder)
        {
            recorder.Record(ref value, nameof(value));
        }
    }

    public class IntClassComponent : Dec.IRecordable
    {
        public int value;

        public void Record(Dec.Recorder recorder)
        {
            recorder.Record(ref value, nameof(value));
        }
    }

    public static class CowInitSystem
    {
        public static void Execute(ref Cow<IntClassComponent> intClassComponent)
        {
            intClassComponent.Set(new IntClassComponent());
        }
    }

    public static class CowROSystem
    {
        public static IntClassComponent holder;
        public static void Execute(ref Cow<IntClassComponent> intClassComponent)
        {
            holder = intClassComponent.GetRO();
        }
    }

    public static class CowRWSystem
    {
        public static IntClassComponent holder;
        public static void Execute(ref Cow<IntClassComponent> intClassComponent)
        {
            holder = intClassComponent.GetRW();
        }
    }

    [SetUp]
    public void Setup()
    {
        UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
        var parser = new Dec.Parser();
        parser.AddString(Dec.Parser.FileType.Xml, @"
            <Decs>
                <ComponentDec decName=""IntStructComponent"">
                    <type>IntStructComponent</type>
                </ComponentDec>

                <ComponentDec decName=""IntClassComponent"">
                    <type>IntClassComponent</type>
                </ComponentDec>

                <ComponentDec decName=""IntCowComponent"">
                    <type>IntClassComponent</type>
                    <cow>true</cow>
                </ComponentDec>

                <EntityDec decName=""StructEntity"">
                    <components>
                        <li>IntStructComponent</li>
                    </components>
                </EntityDec>

                <EntityDec decName=""ClassEntity"">
                    <components>
                        <li>IntClassComponent</li>
                    </components>
                </EntityDec>

                <EntityDec decName=""CowEntity"">
                    <components>
                        <li>IntCowComponent</li>
                    </components>
                </EntityDec>

                <SystemDec decName=""CowInitSystem"">
                    <type>CowInitSystem</type>
                </SystemDec>

                <SystemDec decName=""CowROSystem"">
                    <type>CowROSystem</type>
                </SystemDec>

                <SystemDec decName=""CowRWSystem"">
                    <type>CowRWSystem</type>
                </SystemDec>

                <ProcessDec decName=""CowInitProcess"">
                    <order>
                        <li>CowInitSystem</li>
                    </order>
                </ProcessDec>

                <ProcessDec decName=""CowROProcess"">
                    <order>
                        <li>CowROSystem</li>
                    </order>
                </ProcessDec>

                <ProcessDec decName=""CowRWProcess"">
                    <order>
                        <li>CowRWSystem</li>
                    </order>
                </ProcessDec>
            </Decs>
        ");
        parser.Finish();
    }

    [Test]
    public void Manual()
    {
        Environment.Init();
        var env = new Environment();
        using var envActive = new Environment.Scope(env);

        Ghi.Cow<IntClassComponent> cow = new Ghi.Cow<IntClassComponent>();
        cow.Set(new IntClassComponent());
        IntClassComponent original = cow.GetRO();

        var envClone = Dec.Recorder.Clone(env);

        IntClassComponent postCloneRO = cow.GetRO();
        IntClassComponent postCloneRW = cow.GetRW();

        IntClassComponent postCloneAndRWRO = cow.GetRO();

        Assert.AreSame(original, postCloneRO);
        Assert.AreNotSame(original, postCloneRW);
        Assert.AreSame(postCloneRW, postCloneAndRWRO);
    }

    [Test]
    public void WithinEnv()
    {
        Environment.Init();
        var env = new Environment();

        Ghi.Entity entity;
        IntClassComponent cowOriginal;
        {
            using var envActive = new Environment.Scope(env);

            entity = env.Add(Decs.CowEntity);
            // this is ugly because .Component<> doesn't currently support COW; this obviously needs to be solved at some point

            env.Process(Decs.CowInitProcess);

            CowROSystem.holder = null;
            env.Process(Decs.CowROProcess);
            cowOriginal = CowROSystem.holder;

            CowRWSystem.holder = null;
            env.Process(Decs.CowRWProcess);
            Assert.AreSame(cowOriginal, CowRWSystem.holder);
        }

        var envClone = Dec.Recorder.Clone(env);

        {
            using var envActive = new Environment.Scope(envClone);

            CowROSystem.holder = null;
            envClone.Process(Decs.CowROProcess);
            Assert.AreSame(cowOriginal, CowROSystem.holder);

            CowRWSystem.holder = null;
            envClone.Process(Decs.CowRWProcess);
            var cowClone = CowRWSystem.holder;
            Assert.AreNotSame(cowOriginal, cowClone);

            CowROSystem.holder = null;
            envClone.Process(Decs.CowROProcess);
            Assert.AreSame(cowClone, CowROSystem.holder);
        }
    }
}
