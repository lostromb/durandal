using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.NetFramework
{
    internal class Assertions
    {
        public static void ExceptionThrown<E>(Action action) where E : Exception
        {
            try
            {
                action();
                Assert.Fail("Should have thrown an exception of type " + typeof(E).Name);
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(E));
            }
        }

        public static async Task ExceptionThrown<E>(Func<Task> action) where E : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
                Assert.Fail("Should have thrown an exception of type " + typeof(E).Name);
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(E));
            }
        }
    }
}
