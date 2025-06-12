using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    public class CarpoolAlgorithm<TProducerInput>
    {
        //private static readonly TimeSpan WAIT_LOOP_TIME = TimeSpan.FromMilliseconds(100);
        //private readonly SemaphoreSlim _workSharingMutex = new SemaphoreSlim(1, 1);
        //private readonly SemaphoreSlim _workDone = new SemaphoreSlim(0, 1000);
        private int _waitingThreads = -1;

        public delegate ValueTask PerformWorkAndDispatchDelegate(TProducerInput producerInput, CancellationToken cancelToken, IRealTimeProvider realTime);
        public delegate ValueTask<RetrieveResult<TReaderResult>> CheckForCompletedWorkDelegate<TReaderInput, TReaderResult>(TReaderInput taskInput, CancellationToken cancelToken, IRealTimeProvider realTime);

        private readonly PerformWorkAndDispatchDelegate _performWorkFunc;

        public CarpoolAlgorithm(PerformWorkAndDispatchDelegate workDelegate)
        {
            _performWorkFunc = workDelegate.AssertNonNull(nameof(workDelegate));
        }

        private enum SlotWorkState
        {
            WorkNotStarted = 0,
            WorkStarted = 1,
            WorkCompleted = 2,
            WorkSkipped = 3
        }

        private class SlotReservation
        {
            public SlotWorkState State = SlotWorkState.WorkNotStarted;
        }

        private SlotReservation _currentSlot = new SlotReservation();

        private SemaphoreSlim _slotReserveMutex = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Threadless work-sharing algorithm. When you call this method, it will either start an async task to do some work,
        /// or wait for an existing task if one is already running. Then it will repeat until one work item reads an item
        /// which satisfies this readOutputDelegate. That result will then be returned. Otherwise, the method will loop until cancellation.
        /// </summary>
        /// <param name="producerInput">Input to the producer (work) function.</param>
        /// <param name="readerInput">Input to the reader (no-work) function.</param>
        /// <param name="readOutputDelegate">The implementation of the reader function.</param>
        /// <param name="cancelToken">The cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An async task which will produce a mailbox reply message</returns>
        public async ValueTask<RetrieveResult<TReaderResult>> WorkOnePhase<TReaderInput, TReaderResult>(
            TProducerInput producerInput,
            TReaderInput readerInput,
            CheckForCompletedWorkDelegate<TReaderInput, TReaderResult> readOutputDelegate,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // Super paranoid correct implementation

            while (true)
            {
                SlotReservation thisThreadSlot;
                bool thisThreadIsProducer = false;
                // Step 1: Reserve a waiting spot on the thread list
                await _slotReserveMutex.WaitAsync(cancelToken).ConfigureAwait(false);
                try
                {
                    thisThreadSlot = _currentSlot;
                    if (thisThreadSlot.State == SlotWorkState.WorkNotStarted)
                    {
                        thisThreadSlot.State = SlotWorkState.WorkStarted;
                        thisThreadIsProducer = true;
                    }
                }
                finally
                {
                    _slotReserveMutex.Release();
                }

                // Step 2: If we are the first thread, do work
                if (thisThreadIsProducer)
                {
                    try
                    {
                        // See if we even need to run the producer
                        RetrieveResult<TReaderResult> rr = await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);
                        if (rr.Success)
                        {
                            // If not, just return
                            thisThreadSlot.State = SlotWorkState.WorkSkipped;
                            return rr;
                        }
                        else
                        {
                            await _performWorkFunc(producerInput, cancelToken, realTime).ConfigureAwait(false);
                            thisThreadSlot.State = SlotWorkState.WorkCompleted;
                            return await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        // Signal all waiters on this carpool that we are done
                        await _slotReserveMutex.WaitAsync(cancelToken).ConfigureAwait(false);
                        try
                        {
                            // Switch slots
                            _currentSlot = new SlotReservation();

                            // And that's it. We don't need to wait for anything.
                        }
                        finally
                        {
                            _slotReserveMutex.Release();
                        }
                    }
                }
                // Step 3: If we are not the first thread, wait for work to finish. Then check result
                else
                {
                    // Tentative check
                    RetrieveResult<TReaderResult> rr = await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);
                    if (rr.Success)
                    {
                        return rr;
                    }

                    // Wait for producer to finish
                    while (thisThreadSlot.State == SlotWorkState.WorkStarted)
                    {
                        await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);
                    }

                    rr = await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);

                    // Either the producer ran fully but didn't give us any output...
                    if (thisThreadSlot.State == SlotWorkState.WorkCompleted)
                    {
                        return rr;
                    }

                    // Or the producer didn't run at all, but somehow we still got an output (is this possible?)
                    if (rr.Success)
                    {
                        return rr;
                    }
                }
            }


#if FASTER_IMPLEMENTATION
            bool isProducer;
            await _workSharingMutex.WaitAsync(cancelToken).ConfigureAwait(false);
            try
            {
                isProducer = Interlocked.Increment(ref _waitingThreads) == 0;
            }
            finally
            {
                _workSharingMutex.Release();
            }

            if (isProducer)
            {
                try
                {
                    await _performWorkFunc(producerInput, cancelToken, realTime).ConfigureAwait(false);

                    // If we are the producer thread, and the result is non-null, it means we just read a message from the
                    // post office, and it was intended for us. We can break the loop!
                    return await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        // OPT: If there are a lot of waiting threads, we can potentially get starved out of this mutex
                        // which will prevent us from processing the actual result of the work item
                        // Ideally we should be able to replace this with something that blocks all new threads from coming in
                        // and then releases them all at once. But I haven't found a working design that does that.
                        await _workSharingMutex.WaitAsync(cancelToken).ConfigureAwait(false);
                        try
                        {
                            int waitingThreads = _waitingThreads;
                            if (waitingThreads > 0)
                            {
                                _workDone.Release(waitingThreads);
                            }

                            SpinWait.SpinUntil(NoWorkRunning);

                            while (_workDone.CurrentCount > 0)
                            {
                                _workDone.Wait();
                            }
                        }
                        finally
                        {
                            _workSharingMutex.Release();
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _waitingThreads);
                    }
                }
            }
            else
            {
                try
                {
                    bool loop = true;
                    do
                    {
                        // First, check if a previous producer has already got the message for us
                        RetrieveResult<TReaderResult> rr = await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);
                        if (rr != null && rr.Success)
                        {
                            return rr;
                        }

                        //_serviceLogger.Log("Non-producer thread is waiting for replies to " + desiredReplyToId);
                        loop = !(await _workDone.WaitAsync(WAIT_LOOP_TIME, cancelToken).ConfigureAwait(false));
                        //loop = !_workDone.Wait(WAIT_LOOP_TIME, cancelToken);

                        if (loop && realTime.IsForDebug)
                        {
                            // consume virtual time if we need to
                            await realTime.WaitAsync(TimeSpan.FromMilliseconds(100), cancelToken).ConfigureAwait(false);
                        }
                    } while (loop);
                }
                finally
                {
                    Interlocked.Decrement(ref _waitingThreads);
                }

                // Final read check
                return await readOutputDelegate(readerInput, cancelToken, realTime).ConfigureAwait(false);
            }
#endif
        }

        private bool NoWorkRunning()
        {
            return _waitingThreads == 0;
        }
    }
}
