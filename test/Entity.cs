
namespace Ghi.Test
{
    using NUnit.Framework;
    using System.Linq;

    [TestFixture]
    public class EntityTest : Base
    {
        [Dec.StaticReferences]
        public static class EntityTemplateDecs
        {
            static EntityTemplateDecs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec EntityModel;
        }

        [Dec.StaticReferences]
        public static class EntityProcessTemplateDecs
        {
            static EntityProcessTemplateDecs() { Dec.StaticReferencesAttribute.Initialized(); }

            public static ProcessDec TestProcess;
        }

	    [Test]
	    public void Creation()
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
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
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            env.Add(EntityTemplateDecs.EntityModel);
            var ents = env.List.ToArray();

            Assert.AreEqual(1, ents.Length);
            Assert.IsTrue(ents[0].Component<SimpleComponent>() != null);
	    }

        public static class InactiveTestSystem
        {
            public static void Execute()
            {
                var entity = Environment.Current.Value.Add(EntityTemplateDecs.EntityModel);
                entity.Component<SimpleComponent>().number = 4;
            }
        }

        [Test]
        public void Inactive()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs), typeof(EntityProcessTemplateDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""EntityComponent"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>EntityComponent</li>
                        </components>
                    </EntityDec>

                    <SystemDec decName=""TestSystem"">
                        <type>InactiveTestSystem</type>
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

            env.Process(EntityProcessTemplateDecs.TestProcess);

            var ents = env.List.ToArray();

            Assert.AreEqual(1, ents.Length);
            Assert.AreEqual(4, ents[0].Component<SimpleComponent>().number);
        }

        [Test] [Ignore("Explicit components not currently implemented")]
        public void ExplicitComponent()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""EntityComponent"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>EntityComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var comp = new SimpleComponent();
            //var entity = new Entity(EntityTemplateDecs.EntityModel, comp);

            //Assert.AreSame(comp, entity.Component<SimpleComponent>());
        }

        [Test] [Ignore("Explicit components not currently implemented")]
        public void ExplicitComponentDupe()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""EntityComponent"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>EntityComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var comp = new SimpleComponent();
            //ExpectErrors(() => new Entity(EntityTemplateDecs.EntityModel, comp, comp));
        }

        [Test] [Ignore("Explicit components not currently implemented")]
        public void ExplicitComponentInvalid()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""EntityComponent"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>EntityComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var comp = new StringComponent();
            //ExpectErrors(() => new Entity(EntityTemplateDecs.EntityModel, comp));
        }

        [Test] [Ignore("Explicit components not currently implemented")]
        public void ExplicitComponentWrong()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""EntityComponent"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <ComponentDec decName=""NonEntityComponent"">
                        <type>StringComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>EntityComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var comp = new StringComponent();
            //ExpectErrors(() => new Entity(EntityTemplateDecs.EntityModel, comp));
        }

        public class DerivedComponent : SimpleComponent
        {

        }

        [Test] [Ignore("Explicit components not currently implemented")]
        public void ExplicitComponentDerived()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""EntityComponent"">
                        <type>SimpleComponent</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>EntityComponent</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var comp = new DerivedComponent();
            //var entity = env.Add(EntityTemplateDecs.EntityModel, comp);

            //Assert.AreSame(comp, entity.Component<SimpleComponent>());
        }

        [Test]
	    public void ToStringNonexistent()
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
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
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            env.Add(EntityTemplateDecs.EntityModel);
            var ents = env.List.ToArray();
            ents[0].ToString(); // we're just checking to make sure it doesn't crash, we actually don't care what it outputs
	    }

        [Test] [Ignore("ToString not currently implemented")]
	    public void ToStringExistent()
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(EntityTemplateDecs) } });
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
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            /*Environment.Startup(toString: e => "ToStringTest");
            Environment.Add(new Ghi.Entity(EntityTemplateDecs.EntityModel));
            var ents = Environment.List.ToArray();
            Assert.AreEqual("ToStringTest", ents[0].ToString());*/
	    }
    }
}
