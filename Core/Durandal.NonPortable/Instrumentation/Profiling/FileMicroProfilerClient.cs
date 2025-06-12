using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation.Profiling
{
    /// <summary>
    /// Microprofiler client which writes data to a file stream, with the naming convention like "Program_2021-01-09T09-07-08.microprofile".
    /// </summary>
    public class FileMicroProfilerClient : StreamMicroProfilerClient
    {
        private FileMicroProfilerClient(Stream stream, string fileName) : base(stream)
        {
            OutputFileName = new FileInfo(fileName);
        }

        public FileInfo OutputFileName { get; private set; }

        public static async Task<FileMicroProfilerClient> CreateAsync(string binaryName)
        {
            string timeStamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH-mm-ss");
            string newFileName = binaryName + "_" + timeStamp + ".microprofile";
            int counter = 2;
            while (System.IO.File.Exists(newFileName))
            {
                newFileName = binaryName + "_" + timeStamp + "_" + counter + ".microprofile";
                counter++;
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            }

#pragma warning disable CA2000 // Dispose objects before losing scope. Ownership is transferred and exceptions are handled properly
            Stream stream = new FileStream(
                newFileName,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                WRITE_BUFFER_SIZE * 8,
                true);

            try
            {
                FileMicroProfilerClient returnVal = new FileMicroProfilerClient(stream, newFileName);
                stream = null;
                return returnVal;
            }
            finally
            {
                stream?.Dispose();
            }
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        public override void SendProfilingData(byte[] data, int offset, int count)
        {
            // Augment the incoming message with the managed thread ID
            BinaryHelpers.Int32ToByteArrayLittleEndian(Thread.CurrentThread.ManagedThreadId, data, offset + 12);
            base.SendProfilingData(data, offset, count);
        }
    }
}
