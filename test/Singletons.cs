
namespace Ghi.Test
{
    using NUnit.Framework;
    using System.Linq;

    [TestFixture]
    public class Singletons : Base
    {
        [Dec.StaticReferences]
        public static class Decs
        {
            static Decs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static ProcessDec TestProcess;
        }

        public static class SingletonSystem
        {
            public static int Executions = 0;
            public static void Execute(SimpleComponent simple) { simple.number = 15; ++Executions; }
        }

	    [Test]
	    public void Singleton([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""Singleton"">
                        <type>SimpleComponent</type>
                        <singleton>true</singleton>
                    </ComponentDec>

                    <SystemDec decName=""TestSystem"">
                        <type>SingletonSystem</type>
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
                SingletonSystem.Executions = 0;
                env.Process(Decs.TestProcess);
                Assert.AreEqual(1, SingletonSystem.Executions);
                Assert.AreEqual(15, env.Singleton<SimpleComponent>().number);
            });
        }

        public static class SingletonExceptionSystem
        {
            public static void Execute(SimpleComponent simple)
            {
                throw new System.InvalidOperationException();
            }
        }

        [Test]
        public void SingletonException([Values] EnvironmentMode envMode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""Singleton"">
                        <type>SimpleComponent</type>
                        <singleton>true</singleton>
                    </ComponentDec>

                    <SystemDec decName=""TestSystem"">
                        <type>SingletonExceptionSystem</type>
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
                ExpectErrors(() => env.Process(Decs.TestProcess));
            });
        }

        public class BasicCommon
        {

        }

        public class BasicA
        {

        }

        public class BasicB
        {

        }

        public static class MultiEntityWithSingletonSystem
        {
            public static void Execute(SimpleComponent singleton, BasicCommon common)
            {
                throw new System.InvalidOperationException();
            }
        }

        // we had a bug where a system that touched two tranches, and had a singleton, would cause errors
        // so this is checking for that
        [Test]
        public void MultiEntityWithSingleton()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""Singleton"">
                        <type>SimpleComponent</type>
                        <singleton>true</singleton>
                    </ComponentDec>

                    <ComponentDec decName=""BasicCommon"">
                        <type>BasicCommon</type>
                    </ComponentDec>

                    <ComponentDec decName=""BasicA"">
                        <type>BasicA</type>
                    </ComponentDec>

                    <ComponentDec decName=""BasicB"">
                        <type>BasicB</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityA"">
                        <components>
                            <li>BasicCommon</li>
                            <li>BasicA</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""EntityB"">
                        <components>
                            <li>BasicCommon</li>
                            <li>BasicB</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""TestSystem"">
                        <type>MultiEntityWithSingletonSystem</type>
                    </SystemDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();

            // we actually don't need anything but the init
        }
    }
}
