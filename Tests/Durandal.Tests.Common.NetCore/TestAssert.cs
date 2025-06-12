using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common
{
    internal class TestAssert
    {
        public static void ExceptionThrown<T>(Action actionToRun) where T : Exception
        {
            try
            {
                actionToRun();
                Assert.Fail($"Expected an exception of type {typeof(T).Name}");
            }
            catch (Exception e)
            {
                if (e is T)
                {
                    return;
                }

                throw;
            }
        }

        public static async Task ExceptionThrown<T>(Func<Task> actionToRun) where T : Exception
        {
            try
            {
                await actionToRun().ConfigureAwait(false);
                Assert.Fail($"Expected an exception of type {typeof(T).Name}");
            }
            catch (Exception e)
            {
                if (e is T)
                {
                    return;
                }

                throw;
            }
        }
    }
}
