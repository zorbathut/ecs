
using Dec;

namespace Ghi.Test
{
    using NUnit.Framework;
    using System.Linq;

    [TestFixture]
    public class Systems : Base
    {
        [Dec.StaticReferences]
        public static class Decs
        {
            static Decs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static ProcessDec TestProcess;
        }

        public static class NullSystem
        {
            public static int Executions = 0;
            public static void Execute() { ++Executions; }
        }

	    [Test]
	    public void Null([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <SystemDec decName=""TestSystem"">
                        <type>NullSystem</type>
                    </SystemDec>

                    <ProcessDec decName=""TestProcess"">
                        <order>
                            <li>TestSystem</li>
                        </order>
                    </ProcessDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            ProcessEnvMode(env, envMode, env =>
            {
                NullSystem.Executions = 0;
                env.Process(Decs.TestProcess);
                Assert.AreEqual(1, NullSystem.Executions);
            });
        }

        class ComponentA : Dec.IRecordable
        {
            public void Record(Recorder recorder)
            {

            }
        }

        class ComponentB : Dec.IRecordable
        {
            public void Record(Recorder recorder)
            {

            }
        }

        class ComponentC : Dec.IRecordable
        {
            public void Record(Recorder recorder)
            {

            }
        }

        class ComponentD : Dec.IRecordable
        {
            public void Record(Recorder recorder)
            {

            }
        }

        class ProcessA
        {
            public static void Execute(ComponentA ca) { }
        }

        class ProcessB
        {
            public static void Execute(ComponentB ca) { }
        }

        class ProcessC
        {
            public static void Execute(ComponentC ca) { }
        }

        class ProcessD
        {
            public static void Execute(ComponentD ca) { }
        }

        [Dec.StaticReferences]
        public static class LotsOfComponentsDecs
        {
            static LotsOfComponentsDecs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec EntityA;
            public static EntityDec EntityB;
            public static EntityDec EntityC;
            public static EntityDec EntityD;

            public static ProcessDec Everything;
        }

        [Test]
        public void LotsOfComponents([Values] EnvironmentMode envMode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(LotsOfComponentsDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""ComponentA"">
                        <type>ComponentA</type>
                    </ComponentDec>

                    <ComponentDec decName=""ComponentB"">
                        <type>ComponentB</type>
                    </ComponentDec>

                    <ComponentDec decName=""ComponentC"">
                        <type>ComponentC</type>
                    </ComponentDec>

                    <ComponentDec decName=""ComponentD"">
                        <type>ComponentD</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityA"">
                        <components>
                            <li>ComponentA</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""EntityB"">
                        <components>
                            <li>ComponentB</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""EntityC"">
                        <components>
                            <li>ComponentC</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""EntityD"">
                        <components>
                            <li>ComponentD</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""ProcessA"">
                        <type>ProcessA</type>
                    </SystemDec>

                    <SystemDec decName=""ProcessB"">
                        <type>ProcessB</type>
                    </SystemDec>

                    <SystemDec decName=""ProcessC"">
                        <type>ProcessC</type>
                    </SystemDec>

                    <SystemDec decName=""ProcessD"">
                        <type>ProcessD</type>
                    </SystemDec>

                    <ProcessDec decName=""Everything"">
                        <order>
                            <li>ProcessA</li>
                            <li>ProcessB</li>
                            <li>ProcessC</li>
                            <li>ProcessD</li>
                        </order>
                    </ProcessDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var ea = env.Add(LotsOfComponentsDecs.EntityA);
            var eb = env.Add(LotsOfComponentsDecs.EntityB);
            var ec = env.Add(LotsOfComponentsDecs.EntityC);
            var ed = env.Add(LotsOfComponentsDecs.EntityD);

            ea.Component<ComponentA>();
            eb.Component<ComponentB>();
            ec.Component<ComponentC>();
            ed.Component<ComponentD>();

            ProcessEnvMode(env, envMode, env =>
            {
                env.Process(LotsOfComponentsDecs.Everything);
            });
        }
    }
}
