using Dec;
using NUnit.Framework;
using System.Linq;

namespace Ghi.Test;

public class StructValue : Base
{
    [Dec.StaticReferences]
    public static class Decs
    {
        static Decs() { Dec.StaticReferencesAttribute.Initialized(); }

        public static EntityDec StructEntity;
        public static ProcessDec StructProcessValue;
    }

    public struct StructComponent : IRecordable
    {
        public int value;

        public void Record(Recorder recorder)
        {
            recorder.Record(ref value, nameof(value));
        }
    }

    public static class StructSystemValue
    {
        public static int hit = 0;

        public static void Execute(StructComponent structComponent)
        {
            Assert.AreEqual(8, structComponent.value);
            ++hit;
        }
    }

    [SetUp]
    public void Setup()
    {
        UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
        var parser = new Dec.Parser();
        parser.AddString(Dec.Parser.FileType.Xml, @"
            <Decs>
                <ComponentDec decName=""StructComponentDec"">
                    <type>StructComponent</type>
                </ComponentDec>

                <EntityDec decName=""StructEntity"">
                    <components>
                        <li>StructComponentDec</li>
                    </components>
                </EntityDec>

                <SystemDec decName=""StructSystemValue"">
                    <type>StructSystemValue</type>
                </SystemDec>

                <ProcessDec decName=""StructProcessValue"">
                    <order>
                        <li>StructSystemValue</li>
                    </order>
                </ProcessDec>
            </Decs>
        ");
        parser.Finish();
    }

    [Test]
    public void StructComponentValue([Values] EnvironmentMode envMode)
    {
        Environment.Init();
        var env = new Environment();
        using var envActive = new Environment.Scope(env);

        var se = env.Add(Decs.StructEntity, new object[] { new StructComponent() { value = 8 } });

        ProcessEnvMode(env, envMode, env =>
        {
            StructSystemValue.hit = 0;

            env.Process(Decs.StructProcessValue);

            Assert.AreEqual(1, StructSystemValue.hit);
        });
    }
}