using Dec;
using NUnit.Framework;

namespace Ghi.Test
{
    [TestFixture]
    public class Components : Base
    {
        [Dec.StaticReferences]
        public static class Decs
        {
            static Decs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec EntityModelA;
            public static EntityDec EntityModelB;
        }

        public class SubclassBase : IRecordable
        {
            public virtual void Record(Dec.Recorder recorder) { }
        }

        public class SubclassDerived : SubclassBase
        {
            public override void Record(Dec.Recorder recorder)
            {
                base.Record(recorder);
            }
        }

        public class SubclassDerivedAlternate : SubclassBase
        {
            public override void Record(Dec.Recorder recorder)
            {
                base.Record(recorder);
            }
        }

        [Test]
	    public void Subclass([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""ComponentA"">
                        <type>SubclassDerived</type>
                    </ComponentDec>

                    <ComponentDec decName=""ComponentB"">
                        <type>SubclassDerivedAlternate</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModelA"">
                        <components>
                            <li>ComponentA</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""EntityModelB"">
                        <components>
                            <li>ComponentA</li>
                            <li>ComponentB</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var entityA = env.Add(Decs.EntityModelA);
            var entityB = env.Add(Decs.EntityModelB);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.AreSame(entityA.Component<SubclassBase>(), entityA.Component<SubclassDerived>());
                ExpectErrors(() => entityB.Component<SubclassBase>());
            });
        }

        [Test]
        public void HasComponent([Values] EnvironmentMode envMode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(Decs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""ComponentA"">
                        <type>SubclassDerived</type>
                    </ComponentDec>

                    <ComponentDec decName=""ComponentB"">
                        <type>SubclassDerivedAlternate</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModelA"">
                        <components>
                            <li>ComponentA</li>
                        </components>
                    </EntityDec>

                    <EntityDec decName=""EntityModelB"">
                        <components>
                            <li>ComponentB</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var entityA = env.Add(Decs.EntityModelA);
            var entityB = env.Add(Decs.EntityModelB);

            ProcessEnvMode(env, envMode, env =>
            {
                Assert.IsTrue(entityA.HasComponent<SubclassDerived>());
                Assert.IsTrue(!entityA.HasComponent<SubclassDerivedAlternate>());

                Assert.IsTrue(!entityB.HasComponent<SubclassDerived>());
                Assert.IsTrue(entityB.HasComponent<SubclassDerivedAlternate>());
            });
        }
    }
}
