using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using EfficientRequestHandling.RequestBuilders;
using Microsoft.Graph;
using System.Collections.Generic;
using System;
using EfficientRequestHandling;

namespace ScenarioImplementations
{
    public static class GroupScenarios
    {
        /// <summary>
        /// Gets all groups in the tenant, with all their members.
        /// Today, Graph's $expand parameter cannot be used to download all members in a group, it just gets the top N members
        /// The approach taken here is to download group objects first (which do not contain members), and then get all members of each group.
        /// We can optimize this futher by parallelizing requests to get groups with requests to get members - this way we don't have to wait
        /// for all groups to download before we start fetching members.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Group> GetAllGroupsWithMembers(RequestManager requestManager)
        {
            // Step 1: start downloading group objects using partitioning

            // This collection will be gradually populated with group objects, as they are fetched in the background by RequestManager
            IEnumerable<Group> groups;
            using (var builder = GraphRequestBuilder<Group>.GetBuilder<GroupCollectionResponseHandler>(requestManager, out groups))
            {
                // We use filters to split the group collection into streams. Graph supports indexed queries on the mailNickname property so we can use that.
                // This is useful if the tenat contains many groups.
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("mailNickname"))
                {
                    // This is standard syntax of the Graph SDK. We "pretend" to send the request and wait for it, but in reality the GraphRequestBuilder class does not execute the request here.
                    // Instead, it queues it up with the RequestManager for background execution. The reason we call GetAsync().Wait() is to force the code internal to the Graph SDK
                    // to fully execute and build the final request.
                    // The Top() expression is used to maximize the size of each result page. 999 is the maximum size for the Group collection.
                    builder.Groups.Request().Top(999).Filter(filter).GetAsync().Wait();
                }
            }

            // Step 2: start downloading group members 
            // At this point, groups are already trickling in from the background thread managed by Requestmanager. 
            // As groups objects come in, we immediately create a request to download members

            // This collection will contain groups with fully populated Members property
            IEnumerable<Group> groupsWithMembers;

            // This builder supports creation of requests for group memberships.
            using (var builder = GroupNestedCollectionsRequestBuilder.GetBuilder(requestManager, out groupsWithMembers))
            {
                // For each group object that has been downloaded we can now create a request. Note that the "groups" collection will block the thread
                // and wait if there are more results that have not been fetched yet.
                foreach (var group in groups)
                {
                    // Again, we queue up more requests to execute in the background.
                    // The request is only for the first page, but the specialized builder knows how to handle responses and will queue up more requests
                    // until the full group membership list is fetched and added to the group object.
                    builder.Members(group).Request().Top(999).GetAsync().Wait();
                }
            }
            // Note that at this point we have not fully fetched all results. This collection can be enumerated and will block if more results are incoming.
            // E.g. you could say groupsWithMembers.ToArray() to wait for all results, or use foreach and gradually process results as they become available.
            // Any group objects that show up in this collection will already have a full list of members populated.
            return groupsWithMembers;
        }
        /// <summary>
        /// Gets all members in for a single group. It returns a flat collection of DirectoryObjects.
        /// <returns></returns>
        public static IEnumerable<DirectoryObject> GetAllMembersFromASingleGroup(RequestManager requestManager, string groupId)
        {
            IEnumerable<DirectoryObject> members;
            using (var builder = GraphRequestBuilder<DirectoryObject>.GetBuilder<GroupMembersCollectionResponseHandler>(requestManager, out members))
            {
                builder.Groups[groupId].Members.Request().Top(999).GetAsync().Wait();
            }
            return members;
        }
    }
}
