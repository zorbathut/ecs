using Dec;

namespace Ghi.Test
{
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class Lifetime : Base
    {
        [Dec.StaticReferences]
        public static class RemovalDecs
        {
            static RemovalDecs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec EntityModel;
        }

        public class SubclassBase : IRecordable
        {
            public virtual void Record(Dec.Recorder recorder) { }
        }

        [Test]
	    public void Removal([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(RemovalDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""StringComponent"">
                        <type>StringComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>StringComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var entityA = env.Add(RemovalDecs.EntityModel);
            Assert.IsNotNull(entityA.TryComponent<StringComponent>());
            Assert.IsNotNull(entityA.Component<StringComponent>());

            env.Remove(entityA);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.IsNull(entityA.TryComponent<StringComponent>());
                ExpectErrors(() => Assert.IsNull(entityA.Component<StringComponent>()));
            });

            var entityB = env.Add(RemovalDecs.EntityModel);
            Assert.IsNotNull(entityB.TryComponent<StringComponent>());
            Assert.IsNotNull(entityB.Component<StringComponent>());
            env.Remove(entityB);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.IsNull(entityA.TryComponent<StringComponent>());
                Assert.IsNull(entityB.TryComponent<StringComponent>());

                ExpectErrors(() => Assert.IsNull(entityA.Component<StringComponent>()));
                ExpectErrors(() => Assert.IsNull(entityB.Component<StringComponent>()));
            });

            var entityC = env.Add(RemovalDecs.EntityModel);
            var entityD = env.Add(RemovalDecs.EntityModel);
            var entityE = env.Add(RemovalDecs.EntityModel);
            var entityF = env.Add(RemovalDecs.EntityModel);

            entityC.Component<StringComponent>().str = "C";
            entityD.Component<StringComponent>().str = "D";
            entityE.Component<StringComponent>().str = "E";
            entityF.Component<StringComponent>().str = "F";

            Assert.AreEqual("C", entityC.Component<StringComponent>().str);
            Assert.AreEqual("D", entityD.Component<StringComponent>().str);
            Assert.AreEqual("E", entityE.Component<StringComponent>().str);
            Assert.AreEqual("F", entityF.Component<StringComponent>().str);

            env.Remove(entityD);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.AreEqual("C", entityC.Component<StringComponent>().str);
                Assert.IsNull(entityD.TryComponent<StringComponent>());
                Assert.AreEqual("E", entityE.Component<StringComponent>().str);
                Assert.AreEqual("F", entityF.Component<StringComponent>().str);

                Assert.AreEqual(env.List.Select(e => e.Component<StringComponent>().str).OrderBy(s => s).ToArray(), new string[] { "C", "E", "F" });
            });

            env.Remove(entityF);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.AreEqual("C", entityC.Component<StringComponent>().str);
                Assert.IsNull(entityD.TryComponent<StringComponent>());
                Assert.AreEqual("E", entityE.Component<StringComponent>().str);
                Assert.IsNull(entityF.TryComponent<StringComponent>());

                Assert.AreEqual(env.List.Select(e => e.Component<StringComponent>().str).OrderBy(s => s).ToArray(), new string[] { "C", "E" });
            });

        }

        [Test]
	    public void RemovalRefs([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(RemovalDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""StringComponent"">
                        <type>StringComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>StringComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var entityA = env.Add(RemovalDecs.EntityModel);
            var refA = EntityComponent<StringComponent>.From(entityA);
            Assert.IsNotNull(refA.TryGet());
            Assert.IsNotNull(refA.Get());

            env.Remove(entityA);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.IsNull(refA.TryGet());
                ExpectErrors(() => Assert.IsNull(refA.Get()));
            });
        }

        [Dec.StaticReferences]
        public static class LiveAdditionDecs
        {
            static LiveAdditionDecs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec EntityModel;
            public static ProcessDec Process;
        }

        public static class LiveAdditionCreator
        {
            public static void Execute()
            {
                var env = Environment.Current.Value;
                var entity = env.Add(LiveAdditionDecs.EntityModel);
                entity.Component<StringComponent>().str = "beefs";
            }
        }

        [Test]
	    public void LiveAddition([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(LiveAdditionDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""StringComponent"">
                        <type>StringComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>StringComponent</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""Creator"">
                        <type>LiveAdditionCreator</type>
                    </SystemDec>

                    <ProcessDec decName=""Process"">
                        <order>
                            <li>Creator</li>
                        </order>
                    </ProcessDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            env.Process(LiveAdditionDecs.Process);

            ProcessEnvMode(env, envMode, env =>
            {
                var entities = env.List.ToArray();
                Assert.AreEqual(1, entities.Length);
                Assert.IsTrue(entities.All(e => e.Component<StringComponent>().str == "beefs"));
            });
        }

        public class OnRemoveComp : Ghi.IOnRemove
        {
            public static int removed = 0;

            public void OnRemove(Entity entity)
            {
                removed++;
            }
        }

        [Test]
        public void OnRemove()
        {
            OnRemoveComp.removed = 0;

            UpdateTestParameters(new Dec.Config.UnitTestParameters { });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""OnRemoveComp"">
                        <type>OnRemoveComp</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>OnRemoveComp</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var ent = env.Add(Dec.Database<EntityDec>.Get("EntityModel"));
            Assert.AreEqual(0, OnRemoveComp.removed);
            env.Remove(ent);
            Assert.AreEqual(1, OnRemoveComp.removed);
        }

        public class RemoveRecorderComp : Ghi.IOnRemove
        {
            public int removed = 0;

            public void OnRemove(Entity entity)
            {
                removed++;
            }
        }

        public static class RecordRemovals
        {
            public static List<int> recorded = new();

            public static void Execute(Entity ent, RemoveRecorderComp rrc)
            {
                recorded.Add(rrc.removed);
            }
        }

        public static class RemoveThing
        {
            public static void Execute(Entity ent, RemoveRecorderComp rrc)
            {
                RecordRemovals.recorded.Add(rrc.removed);
                Environment.Current.Value.Remove(ent);
                RecordRemovals.recorded.Add(rrc.removed);
            }
        }

        [Test]
        public void OnSystemRemove()
        {
            OnRemoveComp.removed = 0;

            UpdateTestParameters(new Dec.Config.UnitTestParameters { });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""RemoveRecorderComp"">
                        <type>RemoveRecorderComp</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>RemoveRecorderComp</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""RecordRemovals"">
                        <type>RecordRemovals</type>
                    </SystemDec>

                    <SystemDec decName=""RemoveThing"">
                        <type>RemoveThing</type>
                    </SystemDec>

                    <ProcessDec decName=""SystemRemoveTest"">
                        <order>
                            <li>RecordRemovals</li>
                            <li>RemoveThing</li>
                            <li>RecordRemovals</li>
                        </order>
                    </ProcessDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var ent = env.Add(Dec.Database<EntityDec>.Get("EntityModel"));
            RecordRemovals.recorded.Clear();

            var removeRecorder = ent.Component<RemoveRecorderComp>(); // holding onto this so we can check to make sure it's incremented correctly
            Assert.AreEqual(0, removeRecorder.removed);
            env.Process(Dec.Database<ProcessDec>.Get("SystemRemoveTest"));

            Assert.AreEqual(new List<int>() { 0, 0, 0 }, RecordRemovals.recorded);
            Assert.AreEqual(1, removeRecorder.removed);
        }
    }
}
