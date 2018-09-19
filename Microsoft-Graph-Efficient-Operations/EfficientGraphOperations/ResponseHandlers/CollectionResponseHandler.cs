using EfficientGraphOperations.RequestBuilders;
using EfficientGraphOperations.RequestManagement;
using Microsoft.Graph;
using System.Net.Http;

namespace EfficientGraphOperations.ResponseHandlers
{
    public abstract class CollectionResponseHandler<TResponseObject, UCollectionResponse> : BaseResponseHandler<TResponseObject>
    {
        protected abstract HttpRequestMessage GetNextPageRequest(UCollectionResponse collectionResponse);
        protected abstract ICollectionPage<TResponseObject> GetPageItems(UCollectionResponse collectionResponse);

        public CollectionResponseHandler(RequestManager rm, ResultAggregator<TResponseObject> ra) : base(rm, ra) { }

        protected override void ProcessResponse(HttpResponseMessage rawResponse)
        {
            if (this.CheckForErrors(rawResponse))
            {
                return;
            }
            var collectionResponse = this.requestManager.GraphClient.HttpProvider.Serializer.DeserializeObject<UCollectionResponse>(rawResponse.Content.ReadAsStreamAsync().Result);
            var page = this.GetPageItems(collectionResponse);
            //Logger.WriteLine($"Got collection page for {typeof(TResponseObject).Name} with items: {page.Count}");
            foreach (var item in page)
            {
                this.ReturnResponse(item);
            }
            var nextRequest = this.GetNextPageRequest(collectionResponse);
            
            if (nextRequest != null)
            {
                requestManager.QueueRequest(nextRequest, this.ProcessResponse);
            }
            else
            {
                // no more results in the collection
                this.Unregister();
            }
        }
    }
    
}
