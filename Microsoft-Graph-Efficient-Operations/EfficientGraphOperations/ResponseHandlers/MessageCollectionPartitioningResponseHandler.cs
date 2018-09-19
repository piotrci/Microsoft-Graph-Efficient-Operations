﻿using EfficientGraphOperations.RequestBuilders;
using EfficientGraphOperations.RequestManagement;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EfficientGraphOperations.ResponseHandlers
{
    /// <summary>
    /// Unlike in other collection handlers, we ignore @odata.nextLink and do not "page" through the collection, because we can generate our own based on the partitioning scheme 
    /// using skip and top query params.
    /// This is due to the fact that /messages paging uses skip/top in lieu of skiTokens
    /// 
    /// PROBLEM: Exchange internally skips certain types of items, and that is why nextLink may contain a skip value larger than original + top items requested. This causes a problem with the approach
    /// proposed here, as the partitions we create will overlap and result in the same message being returned multiple times.
    /// </summary>
    /// <param name="rawResponse"></param>
    public class MessageCollectionPartitioningResponseHandler : BaseResponseHandler<Message>
    {
        #region Abstract class implementation
        public MessageCollectionPartitioningResponseHandler(RequestManager rm, ResultAggregator<Message> ra) : base(rm, ra) { }
        public override void InitializeRequest(HttpRequestMessage request)
        {
            ThrowIfSkipOrTopInRequest(request);
            this.originalRequest = request;
            for (int i = 0; i < partitionCount; i++)
            {
                InitializeNewPartitionRequest(GetNewStartingPoint());
            }
        }
        private HttpRequestMessage originalRequest;
        
        protected override void ProcessResponse(HttpResponseMessage rawResponse)
        {
            rawResponse.EnsureSuccessStatusCode();
            var collectionResponse = this.requestManager.GraphClient.HttpProvider.Serializer.DeserializeObject<UserMessagesCollectionResponse>(rawResponse.Content.ReadAsStreamAsync().Result);
            // add items to result aggregator
            foreach (var item in collectionResponse.Value)
            {
                this.ReturnResponse(item);
            }
            // parse the original request uri to extract the $skip value
            var oldStartingPoint = GetSkipValue(rawResponse.RequestMessage.RequestUri);
            int newStaringPoint = 0;
            // check if the number of items returned matches pageSize. if yes, we need to fire another request, if not we should not add more but only remove
            if (collectionResponse.Value.Count < pageSize)
            {
                RemoveOldStartingPoint(oldStartingPoint);
            }
            else
            {
                newStaringPoint = GetNewStartingPoint(oldStartingPoint);
            }
            if (activePartitions.Count == 0)
            {
                // we are done, there is nothing more outstanding, this handler needs to go away
                this.Unregister();
                return;
            }
            if (newStaringPoint > 0)
            {
                // queue up another request
                InitializeNewPartitionRequest(newStaringPoint);
            }
            else
            {
                // do nothing, we are waiting for other partitions to complete
            }
        }
        #endregion

        private const int partitionCount = 16;
        private const int pageSize = 100;
        private readonly SortedSet<int> activePartitions = new SortedSet<int>();


        private static readonly Regex rgSkipValue = new Regex(@"[?].*?[$]skip=(?<value>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        private static int GetSkipValue(Uri uri)
        {
            int value = -1;
            if (uri.Query != null)
            {
                var match = rgSkipValue.Match(uri.Query);
                if (match.Success)
                {
                    value = int.Parse(match.Groups["value"].Value);
                }
            }
            if (value < 0)
            {
                throw new InvalidOperationException($"Original request Uri does not contain a valid $skip parameter. Expected a non zero value generated by this handler. Request Uri was: {uri}");
            }
            return value;
        }
        private void InitializeNewPartitionRequest(int startingPoint)
        {
            var partitionRequest = CreatePartitionRequest(this.originalRequest, startingPoint);
            requestManager.QueueRequest(partitionRequest, this.ProcessResponse);
        }

        private int GetNewStartingPoint(int oldPointToRemove = -1)
        {
            lock (activePartitions)
            {
                if (activePartitions.Count == 0)
                {
                    activePartitions.Add(0);
                    return 0;
                }
                var start = activePartitions.Max + pageSize;
                if (oldPointToRemove > -1)
                {
                    activePartitions.Remove(oldPointToRemove);
                }
                if (!activePartitions.Add(start))
                {
                    throw new InvalidOperationException($"Adding partition from item {start} failed because it exists in {nameof(activePartitions)}. This is unexpected and means there is a bug.");
                }
                return start;
            }
        }
        private void RemoveOldStartingPoint(int oldPointToRemove)
        {
            lock (activePartitions)
            {
                activePartitions.Remove(oldPointToRemove);
            }
        }

        private static HttpRequestMessage CreatePartitionRequest(HttpRequestMessage original, int start)
        {
            var partitionRequest = GraphNetworkHelpers.CloneHttpRequestMessageAsync(original).Result;
            var builder = new UriBuilder(partitionRequest.RequestUri);
            string queryToAppend = $"$skip={start}&$top={pageSize}";

            if (builder.Query != null && builder.Query.Length > 1)
                builder.Query = builder.Query.Substring(1) + "&" + queryToAppend;
            else
                builder.Query = queryToAppend;
            partitionRequest.RequestUri = builder.Uri;
            return partitionRequest;
        }

        private void ThrowIfSkipOrTopInRequest(HttpRequestMessage request)
        {
            var queryParams = request.RequestUri.Query;
            if (queryParams.Contains("$skip=", StringComparison.OrdinalIgnoreCase) || queryParams.Contains("$top=", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The http request contains $skip or $token query params. This is not supported since this handler uses those params to partition requests. The original request Uri is: {request.RequestUri.ToString()}");
            }
        }


    }
}
