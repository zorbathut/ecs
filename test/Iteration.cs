
namespace Ghi.Test
{
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class Iteration : Base
    {
        [Dec.StaticReferences]
        public static class Decs
        {
            static Decs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static ProcessDec TestProcess;
            public static EntityDec EntityModel;
        }

        public static class IterationSystem
        {
            public static int Executions = 0;
            public static void Execute(SimpleComponent simple) { ++Executions; simple.number = Executions; }
        }

	    [Test]
	    public void Basic([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""Component"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>Component</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""TestSystem"">
                        <type>IterationSystem</type>
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

            env.Add(Decs.EntityModel);
            env.Add(Decs.EntityModel);

            ProcessEnvMode(env, envMode, env =>
            {
                IterationSystem.Executions = 0;
                env.Process(Decs.TestProcess);
                Assert.AreEqual(2, IterationSystem.Executions);

                Entity[] entities = env.List.OrderBy(e => e.Component<SimpleComponent>().number).ToArray();
                Assert.AreEqual(1, entities[0].Component<SimpleComponent>().number);
                Assert.AreEqual(2, entities[1].Component<SimpleComponent>().number);
            });
        }

        public static class IterationAddSystem
        {
            public static void Execute(Entity simple) { Environment.Current.Value.Add(Decs.EntityModel); }
        }

        [Test]
	    public void Addition([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""Component"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>Component</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""TestSystem"">
                        <type>IterationAddSystem</type>
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

            env.Add(Decs.EntityModel);
            env.Add(Decs.EntityModel);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.AreEqual(2, env.List.Count());
                env.Process(Decs.TestProcess);
                Assert.AreEqual(4, env.List.Count());
            });
        }

        public static class IterationRemoveSystem
        {
            public static void Execute(Entity simple) { Environment.Current.Value.Remove(simple); }
        }

        [Test]
	    public void Removal([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""Component"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>Component</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""TestSystem"">
                        <type>IterationRemoveSystem</type>
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

            env.Add(Decs.EntityModel);
            env.Add(Decs.EntityModel);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.AreEqual(2, env.List.Count());
                env.Process(Decs.TestProcess);
                Assert.AreEqual(0, env.List.Count());
            });
        }

        // IterationIndex test
        // ----
        // This tests for a specific rather bizarre indexing issue involving using the wrong index. I doubt this exact bug will happen again, but, hey, extra validation.

        [Dec.StaticReferences]
        public static class IterationIndexDecs
        {
            static IterationIndexDecs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec IterationIndexEntityA;
            public static EntityDec IterationIndexEntityB;
            public static ProcessDec IterationIndexProcess;
        }

        public static class IterationIndexSystemA
        {
            public static List<Entity> Touched = new List<Entity>();
            public static void Execute(Entity entity, SimpleComponent sc) { Touched.Add(entity); }
        }

        public static class IterationIndexSystemB
        {
            public static List<Entity> Touched = new List<Entity>();
            public static void Execute(Entity entity, StringComponent sc) { Touched.Add(entity); }
        }

        [Test]
	    public void IterationIndex([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(IterationIndexDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""ComponentA"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <ComponentDec decName=""ComponentB"">
                        <type>StringComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""IterationIndexEntityA"">
                        <components>
                            <li>ComponentA</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""IterationIndexEntityB"">
                        <components>
                            <li>ComponentB</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""IterationIndexSystemA"">
                        <type>IterationIndexSystemA</type>
                    </SystemDec>

                    <SystemDec decName=""IterationIndexSystemB"">
                        <type>IterationIndexSystemB</type>
                    </SystemDec>

                    <ProcessDec decName=""IterationIndexProcess"">
                        <order>
                            <li>IterationIndexSystemA</li>
                            <li>IterationIndexSystemB</li>
                        </order>
                    </ProcessDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            env.Add(IterationIndexDecs.IterationIndexEntityA);
            env.Add(IterationIndexDecs.IterationIndexEntityB);

            IterationIndexSystemA.Touched.Clear();
            IterationIndexSystemB.Touched.Clear();

            ProcessEnvMode(env, envMode, env =>
            {
                env.Process(IterationIndexDecs.IterationIndexProcess);

                Assert.AreEqual(1, IterationIndexSystemA.Touched.Count);
                Assert.AreEqual(1, IterationIndexSystemB.Touched.Count);
                Assert.AreEqual(2, Enumerable.Union(IterationIndexSystemA.Touched, IterationIndexSystemB.Touched).Count());
            });
        }
    }
}
