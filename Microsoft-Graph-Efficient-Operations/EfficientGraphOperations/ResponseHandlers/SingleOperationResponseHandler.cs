using EfficientRequestHandling.RequestBuilders;
using EfficientRequestHandling.RequestManagement;
using Microsoft.Graph;
using System.Net.Http;
using System.Threading;

namespace EfficientRequestHandling.ResponseHandlers
{
    public class SingleOperationResponseHandler<TEntity> : BaseResponseHandler<OperationResult<TEntity>>
    {
        public SingleOperationResponseHandler(RequestManager rm, ResultAggregator<OperationResult<TEntity>> ra) : base(rm, ra)
        {
        }
        private static int successResponseCount;
        private static int errorResponseCount;
        private void LogProgress(bool isSuccess)
        {
            int currentCount = isSuccess ? Interlocked.Increment(ref successResponseCount) : Interlocked.Increment(ref errorResponseCount);
            if (currentCount % 100 == 0)
            {
                Logger.WriteLine($"Progress for write operations - successful: {successResponseCount}; error:{errorResponseCount}");
            }
        }
        protected override void ProcessResponse(HttpResponseMessage rawResponse)
        {
            try
            {
                TEntity item = default(TEntity);
                ErrorResponse error = default(ErrorResponse);
                var requestUri = rawResponse.RequestMessage.RequestUri;
                if (rawResponse.IsSuccessStatusCode)
                {
                    item = this.requestManager.GraphClient.HttpProvider.Serializer.DeserializeObject<TEntity>(rawResponse.Content.ReadAsStreamAsync().Result);
                    LogProgress(true);
                }
                else
                {
                    error = this.requestManager.GraphClient.HttpProvider.Serializer.DeserializeObject<ErrorResponse>(rawResponse.Content.ReadAsStreamAsync().Result);
                    LogProgress(false);
                }
                this.ReturnResponse(new OperationResult<TEntity>(item, error, requestUri));
            }
            finally
            {
                this.Unregister();
            }
        }
    }
}
