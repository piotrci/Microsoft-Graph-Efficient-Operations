using EfficientGraphOperations.RequestManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EfficientGraphOperations.RequestManagement;
using EfficientGraphOperations.ResponseHandlers;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfficientGraphOperations.RequestBuilders
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
