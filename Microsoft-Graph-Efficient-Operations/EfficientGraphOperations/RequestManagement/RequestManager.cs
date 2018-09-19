using Microsoft.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EfficientRequestHandling.RequestManagement
{
    public delegate void ResponseCallback(HttpResponseMessage message);
    public class RequestManager : IDisposable
    {
        #region Constructors
        public RequestManager(GraphServiceClient client, int concurrencyLevel = 16, int batchSize = 20)
        {
            this.graphClient = client;
            if (batchSize > 20 || batchSize < 1)
            {
                throw new ArgumentException($"{nameof(batchSize)} has to be between 1 and 20. Value passed: {batchSize} is incorrect.");
            }
            this.batchSize = batchSize;
            if (concurrencyLevel > 20 || concurrencyLevel < 1)
            {
                throw new ArgumentException($"{nameof(concurrencyLevel)} has to be between 1 and 20. Value passed: {concurrencyLevel} is incorrect.");
            }
            // maximum tasks we want to submit at any given time. keep this close to connectionLimit to prevent timeouts from tasks waiting for an available connection.
            this.maxRunningSendTasks = concurrencyLevel;

            // max size of the inbound request collection is used to prevent runaway growth of the collection. we allow it to gr
            // queue size is for individual original requests, so multiply by batchSize
            //this.queuedInboundRequests = new BlockingCollection<RequestItem>(maxRunningSendTasks * batchSize * 3);

            //NOTE: having bounded capacity creates dead locks when response handlers for collections try to add more requests for next page
            //they get stuck and the running tasks in the Request Manager never get completed
            this.queuedInboundRequests = new BlockingCollection<RequestItem>();
            // we want this collection to be unbound to avoid blocking on retries
            this.internalRetryRequests = new BlockingCollection<RequestItem>();

            this.Start();
        }
        #endregion
        #region Private fields
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private CancellationToken ct;
        private readonly IGraphServiceClient graphClient;

        private readonly BlockingCollection<RequestItem> queuedInboundRequests;
        private readonly BlockingCollection<RequestItem> internalRetryRequests;
        private readonly int maxRunningSendTasks;
        private readonly int batchSize;
        #endregion

        #region Public
        public IGraphServiceClient GraphClient { get => this.graphClient; }
        public void QueueRequest(HttpRequestMessage request, ResponseCallback callback)
        {
            try
            {
                this.queuedInboundRequests.Add(new RequestItem(request, callback));
            }
            catch (InvalidOperationException)
            {
                if (!this.cts.IsCancellationRequested)
                {
                    throw;
                }
            }
        }

        public void RegisterForCancellation(Action callback)
        {
            this.cts.Token.Register(callback);
        }
        public CancellationToken GetCancellationToken()
        {
            return this.cts.Token;
        }

        #endregion
        private void Start()
        {
            if (this.consumerTask != null)
            {
                return;
            }
            this.ct = this.cts.Token;
            this.consumerTask = Task.Factory.StartNew(
                () => this.ConsumeRequests(this.ct),
                this.ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }
        private Task consumerTask;
        private void ConsumeRequests(CancellationToken ct)
        {
            var runningTasks = new List<Task>(maxRunningSendTasks);
            var itemsForBatch = new List<RequestItem>(batchSize);
            //var timeToWait = 3000;
            var timeToWait = 2000;
            bool forceSendBatch = false;
            while (!this.queuedInboundRequests.IsCompleted || itemsForBatch.Count > 0 || runningTasks.Count > 0 || this.internalRetryRequests.Count > 0)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }
                try
                {
                    // first, try to take something from retry queue - they get priority since they can delay send due to throttling. do not block on this
                    if (this.internalRetryRequests.TryTake(out RequestItem item))
                    {
                        itemsForBatch.Add(item);
                    }
                    // if no retries, add an inbound request. block on this one a bit to give external callers a chance to produce something
                    else if (this.queuedInboundRequests.TryTake(out item, timeToWait, ct))
                    {
                        itemsForBatch.Add(item);
                    }
                    // if nothing was added, force the current batch to go out
                    else
                    {
                        forceSendBatch = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    throw;
                }
                // check if we should send something, if not, loop again without sending
                if (!(itemsForBatch.Count == batchSize || forceSendBatch))
                {
                    continue;
                }
                forceSendBatch = false;
                // we should send something
                if (runningTasks.Count == maxRunningSendTasks)
                {
                    RemoveAllCompletedTasks(runningTasks, true); // we have full list of tasks, so need to remove at least one to make room for a new one
                }
                else
                {
                    RemoveAllCompletedTasks(runningTasks, false); // we want to remove whatever we can without blocking. this is important for the loop termination conditions
                }
                SendCurrentBatch(itemsForBatch, runningTasks);
            }
        }

        private void SendCurrentBatch(List<RequestItem> itemsForBatch, List<Task> runningTasks)
        {
            if (itemsForBatch.Count < 1)
            {
                return;
            }
            var batchRequest = BatchFactory.MakeBatchRequest(itemsForBatch, this.graphClient.BaseUrl, out TimeSpan delaySend);
            if (delaySend > TimeSpan.Zero)
            {
                Logger.WriteLine($"Delaying a request by {delaySend.TotalSeconds} seconds.");
                Logger.WriteLine($"Inbound requests in queue: {this.queuedInboundRequests.Count}. Retry requests in queue: {this.internalRetryRequests.Count}");
            }
            var callBackTask = GraphNetworkHelpers.SendWithRetriesAsync(batchRequest, this.GraphClient.AuthenticationProvider, this.ct, delaySend)
                .ContinueWith(
                    OnResponseAvailable,
                    itemsForBatch.ToDictionary(r => r.Id, r => r),  //make a copy so we can access it in the callback to unpack the batch
                    TaskContinuationOptions.RunContinuationsAsynchronously
            );
            itemsForBatch.Clear();
            runningTasks.Add(callBackTask);
        }

        private void OnResponseAvailable(Task<HttpResponseMessage> responseTask, object originalItems)
        {
            try
            {
                HttpResponseMessage response = null;
                try
                {
                     response = responseTask.Result;
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex => ex is TaskCanceledException && this.ct.IsCancellationRequested);
                }
                if (response == null)
                {
                    return;
                }
                // batch responses should always be successful, if not throw an exception
                response.EnsureSuccessStatusCode();
                var retries = BatchFactory.UnpackBatchResponseAndCallback(response, (IDictionary<int, RequestItem>)originalItems);
                foreach (var retry in retries)
                {
                    this.internalRetryRequests.Add(retry);
                }
            }
            catch (Exception)
            {
                this.cts.Cancel(true);
                throw;
            }
        }
        private void RemoveAllCompletedTasks(List<Task> tasks, bool blockToRemoveAtLeastOne)
        {
            if (tasks.Count == 0)
            {
                return;
            }
            // remove whatever we can without blocking
            var removedCount = tasks.RemoveAll(t =>
            {
                if (t.IsCompleted)
                {
                    t.Wait();   // we want to throw any exceptions here - this will not block
                    return true;
                }
                return false;
            });
            // force removal of one task
            if (blockToRemoveAtLeastOne && removedCount < 1)
            {
                // we want this to block the consumer's thread so the consumer doesn't have to compete for thread pool. that's why we do not await WhenAny
                var completedTask = Task.WhenAny(tasks).Result;
                // this should throw any exceptions during the task's execution
                completedTask.Wait(this.ct);
                // remove task from list
                tasks.RemoveAt(tasks.IndexOf(completedTask));
            }
        }
        public void Dispose()
        {
            this.CancelRequestExecution();
            //this.WaitUntilDone();
        }
        private void WaitUntilDone()
        {
            if (this.consumerTask != null)
            {
                this.queuedInboundRequests.CompleteAdding();
                this.consumerTask.Wait();
            }
            this.cts.Cancel();
        }
        private void CancelRequestExecution()
        {
            if (this.consumerTask != null)
            {
                this.queuedInboundRequests.CompleteAdding();
                this.cts.Cancel();
                try
                {
                    this.consumerTask.Wait();
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

    }
    internal class RequestItem
    {
        public RequestItem(HttpRequestMessage request, ResponseCallback callback)
        {
            this.Request = request;
            this.Callback = callback;
            this.Id = Interlocked.Increment(ref globalId);
            this.ExecutionDelay = TimeSpan.Zero;
        }
        private static int globalId = 0;
        public int Id { get; private set; }
        public HttpRequestMessage Request { get; private set; }
        public TimeSpan ExecutionDelay
        {
            get
            {
                var newDelay = requestedDelay - (DateTime.Now - delayStart);
                return newDelay > TimeSpan.Zero ? newDelay : TimeSpan.Zero;
            }
            set
            {
                requestedDelay = value;
                delayStart = DateTime.Now;
            }
        }
        private DateTime delayStart;
        private TimeSpan requestedDelay;
        public void CallbackWithResponse(HttpResponseMessage message)
        {
            if (this.Callback is null)
            {
                return;
            }
            this.Callback(message);
        }
        private ResponseCallback Callback { get; set; }
    }
}
