using Dec;
using NUnit.Framework;
using System.Linq;


namespace Ghi.Test
{
    [TestFixture]
    public class AddWithComponent : Base
    {
        [Dec.StaticReferences]
        public static class GenericEntityModel
        {
            static GenericEntityModel() { Dec.StaticReferencesAttribute.Initialized(); }

            public static EntityDec EntityModel;
        }

        public class IntHolder
        {
            public int value = 2;
        }

        [Test]
	    public void AddSingle()
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { typeof(GenericEntityModel) } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""IntHolder"">
                        <type>IntHolder</type>
                    </ComponentDec>

                    <EntityDec decName=""EntityModel"">
                        <components>
                            <li>IntHolder</li>
                        </components>
                    </EntityDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            var entity = env.Add(GenericEntityModel.EntityModel, new object[] { new IntHolder() { value = 42 }});
            Assert.AreEqual(42, entity.Component<IntHolder>().value);
        }
    }
}
