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
        /// Today, Graph's expand cannot be used to download members, the approach taken here is to download empty group objects first, and then enumerate each groups members.
        /// We do this in parallel: as group objects are downloaded we immediately start downloading their members.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="maxGroups"></param>
        /// <returns></returns>
        public static IEnumerable<Group> GetAllGroupsWithMembers(RequestManager requestManager)
        {
            // Step 1: start downloading group objects using partitioning
            // This will return groups as they become available from concurrent response handlers
            IEnumerable<Group> groups;
            using (var builder = GraphRequestBuilder<Group>.GetBuilder<GroupCollectionResponseHandler>(requestManager, out groups))
            {
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("mailNickname"))
                {
                    // initiate the group request
                    builder.Groups.Request().Top(999).Filter(filter).GetAsync().Wait();
                }
            }
            // initialize a stream for each partition (e.g. split groups by group name)

            // Step 2: as groups come in, create a request to download members
            IEnumerable<Group> groupsWithMembers;
            using (var builder = GroupNestedCollectionsRequestBuilder.GetBuilder(requestManager, out groupsWithMembers))
            {
                foreach (var group in groups)
                {
                    // initiate the group request
                    builder.Members(group).Request().Top(999).GetAsync().Wait();
                }
            }
            return groupsWithMembers;
        }
        /// <summary>
        /// Gets all members in a single group. Does not use parallization because there is currently no way in Graph to create parallel streams of members for a container.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="groupId"></param>
        /// <param name="maxMembers"></param>
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
