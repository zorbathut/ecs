using Dec;

namespace Ghi.Test
{
    using NUnit.Framework;
    using System;
    using System.Reflection;

    public class SimpleComponent : IRecordable
    {
        public int number;

        public void Record(Dec.Recorder recorder)
        {
            recorder.Record(ref number, "number");
        }
    }

    public class StringComponent : IRecordable
    {
        public string str;

        public void Record(Dec.Recorder recorder)
        {
            recorder.Record(ref str, "str");
        }
    }

    [TestFixture]
    public class Base
    {
        [SetUp] [TearDown]
        public void Clean()
        {
            // we turn on error handling so that Clear can work even if we're in the wrong mode
            handlingErrors = true;

            Dec.Database.Clear();

            handlingWarnings = false;
            handledWarning = false;

            handlingErrors = false;
            handledError = false;

            Dec.Config.UsingNamespaces = new string[] { "Ghi", "Ghi.Test", TestContext.CurrentContext.Test.ClassName };
        }

        private bool handlingWarnings = false;
        private bool handledWarning = false;

        private bool handlingErrors = false;
        private bool handledError = false;

        public enum EnvironmentMode
        {
            Standard,
            ReadWrite,
            Cloned,
        }

        [OneTimeSetUp]
        public void PrepHooks()
        {
            Dec.Config.WarningHandler = str => {
                System.Diagnostics.Debug.Print(str);
                Console.WriteLine(str);

                if (handlingWarnings)
                {
                    handledWarning = true;
                }
                else
                {
                    // Throw if we're not handling it - this way we get test failures
                    throw new ArgumentException(str);
                }
            };

            Dec.Config.ErrorHandler = str => {
                System.Diagnostics.Debug.Print(str);
                Console.WriteLine(str);

                if (handlingErrors)
                {
                    // If we're handling it, don't throw - this way we can validate that fallback behavior is working right
                    handledError = true;
                }
                else
                {
                    // Throw if we're not handling it - this way we get test failures and can validate that exception-passing behavior is working right
                    throw new ArgumentException(str);
                }
            };

            Dec.Config.ExceptionHandler = e => {
                Dec.Config.ErrorHandler(e.ToString());
            };

            Dec.Config.UsingNamespaces = new string[] { "Ghi", "Ghi.Test" };
        }

        public static void UpdateTestParameters(Dec.Config.UnitTestParameters parameters)
        {
            typeof(Dec.Config).GetField("TestParameters", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, parameters);
        }

        protected void ExpectWarnings(Action action)
        {
            Assert.IsFalse(handlingWarnings);
            handlingWarnings = true;
            handledWarning = false;

            action();

            Assert.IsTrue(handlingWarnings);
            Assert.IsTrue(handledWarning);
            handlingWarnings = false;
            handledWarning = false;
        }

        protected void ExpectErrors(Action action)
        {
            Assert.IsFalse(handlingErrors);
            handlingErrors = true;
            handledError = false;

            action();

            Assert.IsTrue(handlingErrors);
            Assert.IsTrue(handledError);
            handlingErrors = false;
            handledError = false;
        }

        protected void ExpectException(Action action)
        {
            bool excepted = false;
            try
            {
                action();
            }
            catch
            {
                excepted = true;
            }

            Assert.IsTrue(excepted);
        }

        public void ProcessEnvMode(Ghi.Environment env, EnvironmentMode mode, Action<Ghi.Environment> test)
        {
            switch (mode)
            {
                case EnvironmentMode.Standard:
                    test(env);
                    break;

                case EnvironmentMode.ReadWrite:
                {
                    var envText = Dec.Recorder.Write(env);
                    var envDupe = Dec.Recorder.Read<Ghi.Environment>(envText);
                    using (var scope = new Ghi.Environment.Scope(envDupe))
                    {
                        test(envDupe);
                    }

                    break;
                }

                case EnvironmentMode.Cloned:
                {
                    var envDupe = Dec.Recorder.Clone(env);
                    using (var scope = new Ghi.Environment.Scope(envDupe))
                    {
                        test(envDupe);
                    }

                    break;
                }
            }
        }
    }
}
