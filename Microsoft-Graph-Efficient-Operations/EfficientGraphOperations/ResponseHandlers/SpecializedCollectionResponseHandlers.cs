using EfficientGraphOperations.RequestBuilders;
using EfficientGraphOperations.RequestManagement;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EfficientGraphOperations.ResponseHandlers
{
    public class GroupCollectionResponseHandler : CollectionResponseHandler<Group, GraphServiceGroupsCollectionResponse>
    {
        public GroupCollectionResponseHandler(RequestManager rm, ResultAggregator<Group> ra) : base(rm, ra) { }

        protected override HttpRequestMessage GetNextPageRequest(GraphServiceGroupsCollectionResponse collectionResponse)
        {
            // retrieve nextLink, if exists
            if (!collectionResponse.AdditionalData.TryGetValue("@odata.nextLink", out object nextLink))
            {
                return null;
            }
            collectionResponse.Value.InitializeNextPageRequest(this.requestManager.GraphClient, (string)nextLink);
            return collectionResponse.Value.NextPageRequest.GetHttpRequestMessage();
        }

        protected override ICollectionPage<Group> GetPageItems(GraphServiceGroupsCollectionResponse collectionResponse)
        {
            return collectionResponse.Value;
        }
    }
    public class GroupMembersCollectionResponseHandler : CollectionResponseHandler<DirectoryObject, GroupMemberOfCollectionWithReferencesResponse>
    {
        public GroupMembersCollectionResponseHandler(RequestManager rm, ResultAggregator<DirectoryObject> ra) : base(rm, ra) { }

        protected override HttpRequestMessage GetNextPageRequest(GroupMemberOfCollectionWithReferencesResponse collectionResponse)
        {
            // retrieve nextLink, if exists
            if (!collectionResponse.AdditionalData.TryGetValue("@odata.nextLink", out object nextLink))
            {
                return null;
            }
            collectionResponse.Value.InitializeNextPageRequest(this.requestManager.GraphClient, (string)nextLink);
            return collectionResponse.Value.NextPageRequest.GetHttpRequestMessage();
        }

        protected override ICollectionPage<DirectoryObject> GetPageItems(GroupMemberOfCollectionWithReferencesResponse collectionResponse)
        {
            return collectionResponse.Value;
        }
    }
    public class UserCollectionResponseHandler : CollectionResponseHandler<User, GraphServiceUsersCollectionResponse>
    {
        public UserCollectionResponseHandler(RequestManager rm, ResultAggregator<User> ra) : base(rm, ra) { }

        protected override HttpRequestMessage GetNextPageRequest(GraphServiceUsersCollectionResponse collectionResponse)
        {
            // retrieve nextLink, if exists
            if (!collectionResponse.AdditionalData.TryGetValue("@odata.nextLink", out object nextLink))
            {
                return null;
            }
            collectionResponse.Value.InitializeNextPageRequest(this.requestManager.GraphClient, (string)nextLink);
            return collectionResponse.Value.NextPageRequest.GetHttpRequestMessage();
        }

        protected override ICollectionPage<User> GetPageItems(GraphServiceUsersCollectionResponse collectionResponse)
        {
            return collectionResponse.Value;
        }
    }
    public class MessageCollectionResponseHandler : CollectionResponseHandler<Message, UserMessagesCollectionResponse>
    {
        public MessageCollectionResponseHandler(RequestManager rm, ResultAggregator<Message> ra) : base(rm, ra) { }

        protected override HttpRequestMessage GetNextPageRequest(UserMessagesCollectionResponse collectionResponse)
        {
            // retrieve nextLink, if exists
            if (!collectionResponse.AdditionalData.TryGetValue("@odata.nextLink", out object nextLink))
            {
                return null;
            }
            collectionResponse.Value.InitializeNextPageRequest(this.requestManager.GraphClient, (string)nextLink);
            return collectionResponse.Value.NextPageRequest.GetHttpRequestMessage();
        }

        protected override ICollectionPage<Message> GetPageItems(UserMessagesCollectionResponse collectionResponse)
        {
            return collectionResponse.Value;
        }
        /// <summary>
        /// For Messages we want to ignore 404 because that is returned for users who do not have a mailbox
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        protected override bool IgnoreHttpError(HttpStatusCode code)
        {
            return code == HttpStatusCode.NotFound;
        }
    }
}
