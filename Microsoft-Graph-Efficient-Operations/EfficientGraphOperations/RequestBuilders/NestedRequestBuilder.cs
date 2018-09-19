using EfficientRequestHandling.RequestManagement;
using System;

namespace EfficientRequestHandling.RequestBuilders
{
    public abstract class NestedRequestBuilder<TParentEntity> : IDisposable
    {
        internal NestedRequestBuilder(RequestManager rm, ResultAggregator<TParentEntity> ra)
        {
            this.requestManager = rm;
            this.resultAggregator = ra;
        }
        protected readonly RequestManager requestManager;
        protected readonly ResultAggregator<TParentEntity> resultAggregator;
        
        public void NoMoreRequests()
        {
            this.resultAggregator.NoMoreAdding();
        }
        public void Dispose()
        {
            this.NoMoreRequests();
        }
    }
}
