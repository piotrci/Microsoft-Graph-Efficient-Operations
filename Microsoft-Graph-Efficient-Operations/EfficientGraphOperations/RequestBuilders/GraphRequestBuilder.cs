using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EfficientRequestHandling.RequestBuilders
{
    /// <summary>
    /// A dummy graph client allows us to use the SDK code to create requests without actually sending them to the service.
    /// The RequestCreated event allows us to plug in our own request handler and let the RequestManager class handle sending requests efficiently.
    /// </summary>
    public class GraphRequestBuilder<TResult> : GraphServiceClient, IDisposable
    {
        internal GraphRequestBuilder(ResultAggregator<TResult> ra, Func<BaseResponseHandler<TResult>> handleCreator) : base(dummyAuthProvider, new DummyHttpProvider())
        {
            ((DummyHttpProvider)this.HttpProvider).RequestCaptured += this.DummyHttpProvider_RequestCaptured;
            this.resultAggregator = ra;
            this.createHandler = handleCreator;
        }
        private readonly ResultAggregator<TResult> resultAggregator;
        private readonly Func<BaseResponseHandler<TResult>> createHandler;
        private void DummyHttpProvider_RequestCaptured(object sender, HttpRequestMessage capturedRequest)
        {
            var responseHandler = this.createHandler();
            responseHandler.InitializeRequest(capturedRequest);
        }
        public void NoMoreRequests()
        {
            this.resultAggregator.NoMoreAdding();
        }
        public void Dispose()
        {
            this.NoMoreRequests();
        }
        #region Intercepting requests
        private readonly static IAuthenticationProvider dummyAuthProvider = new DelegateAuthenticationProvider(
                    (requestMessage) =>
                    {
                        return Task.FromResult(0);
                    });
        private class DummyHttpProvider : IHttpProvider
        {
            public event EventHandler<HttpRequestMessage> RequestCaptured;
            async public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
            {
                //throw new NotImplementedException("implement type check in serialized to make sure that requestcs are for TResult on DummyGraphClient");
                this.RequestCaptured(this, await GraphNetworkHelpers.CloneHttpRequestMessageAsync(request));
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }
            
            #region Irrelevant interface stuff
            private static readonly ISerializer dummySerializer = new Serializer();
            public ISerializer Serializer => dummySerializer;
            public TimeSpan OverallTimeout { get => TimeSpan.FromMinutes(1); set => throw new NotImplementedException(); }
            public void Dispose() { }
            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) { throw new NotImplementedException(); }
            #endregion
        }
        #endregion

        #region Public Builder factories
        public static GraphRequestBuilder<TResult> GetBuilder<UResponseHandler>(RequestManager rm, out IEnumerable<TResult> results) where UResponseHandler : BaseResponseHandler<TResult>
        {
            var ra = new ResultAggregator<TResult>(rm.GetCancellationToken());
            results = ra;
            return new GraphRequestBuilder<TResult>(ra, () => BaseResponseHandler<TResult>.GetHandler(typeof(UResponseHandler), rm, ra));
        }
        public static GraphRequestBuilder<OperationResult<UEntity>> GetBuilderForSingleOperation<UEntity>(RequestManager rm, out IEnumerable<OperationResult<UEntity>> results)
        {
            var ra = new ResultAggregator<OperationResult<UEntity>>(rm.GetCancellationToken());
            results = ra;
            return new GraphRequestBuilder<OperationResult<UEntity>>(ra, () => new SingleOperationResponseHandler<UEntity>(rm, ra));
        }
        public static GraphRequestBuilder<User> GetBuilderForUserMailboxes(User u, RequestManager rm, out IEnumerable<User> results)
        {
            var ra = new ResultAggregator<User>(rm.GetCancellationToken());
            results = ra;
            return new GraphRequestBuilder<User>(ra, () => new UserMailboxResponseHandler(u, rm, ra));
        }
        #endregion
    }
}
