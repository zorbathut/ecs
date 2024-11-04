using NUnit.Framework;
using System.Linq;

namespace Ghi.Test
{
    [TestFixture]
    public class EnvironmentTest : Base
    {
	    [Test]
	    public void Singleton([Values] EnvironmentMode envMode)
	    {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitStaticRefs = new System.Type[] { } });
            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ComponentDec decName=""SingComponent"">
                        <type>SimpleComponent</type>
                        <singleton>true</singleton>
                    </ComponentDec>

                    <ComponentDec decName=""EntComponent"">
                        <type>StringComponent</type>
                    </ComponentDec>
                </Decs>
            ");
            parser.Finish();

            Environment.Init();
            var env = new Environment();
            using var envActive = new Environment.Scope(env);

            ProcessEnvMode(env, envMode, env =>
            {
                var simp = env.Singleton<SimpleComponent>();
                Assert.IsNotNull(simp);

                StringComponent str = null;
                ExpectErrors(() => str = env.Singleton<StringComponent>());
                Assert.IsNull(str);
            });
        }
    }
}
