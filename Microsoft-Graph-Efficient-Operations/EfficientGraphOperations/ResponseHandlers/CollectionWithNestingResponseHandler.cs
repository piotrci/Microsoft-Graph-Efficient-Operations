using EfficientRequestHandling.RequestBuilders;
using EfficientRequestHandling.RequestManagement;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace EfficientRequestHandling.ResponseHandlers
{
    abstract class CollectionWithNestingResponseHandler<TParentEntity, UNestedEntity> : BaseResponseHandler<TParentEntity>
    {
        private readonly ResultAggregator<UNestedEntity> nestedResults;
        private readonly BaseResponseHandler<UNestedEntity> nestedCollectionRequestHandler;
        protected readonly TParentEntity parentItem;

        public CollectionWithNestingResponseHandler(TParentEntity parent, Type nestedCollectionHandlerType, RequestManager rm, ResultAggregator<TParentEntity> ra) : base(rm, ra)
        {
            this.parentItem = parent;
            this.nestedResults = new ResultAggregator<UNestedEntity>(rm.GetCancellationToken());
            this.nestedCollectionRequestHandler = BaseResponseHandler<UNestedEntity>.GetHandler(nestedCollectionHandlerType, rm, this.nestedResults);
            this.nestedResults.NoMoreAdding();
            this.nestedResults.ResultsComplete += this.OnResultsComplete;
        }
        private void EmptyHandler(object sender, EventArgs ea) { }
        protected abstract ICollectionPage<UNestedEntity> SetNestedCollectionOnParentAndReturn();
        private void OnResultsComplete(object sender, IEnumerable<UNestedEntity> results)
        {
            try
            {
                var nestedCollection = this.SetNestedCollectionOnParentAndReturn();
                foreach (var item in results)
                {
                    nestedCollection.Add(item);
                }
                this.ReturnResponse(parentItem);
            }
            finally
            {
                this.Unregister();
            }
        }

        public override void InitializeRequest(HttpRequestMessage request)
        {
            this.nestedCollectionRequestHandler.InitializeRequest(request);
        }

        protected override void ProcessResponse(HttpResponseMessage rawResponse)
        {
            // this handler does not process its own responses, it uses the child handler it created in the constructor.
            throw new NotImplementedException();
        }
    }
}
