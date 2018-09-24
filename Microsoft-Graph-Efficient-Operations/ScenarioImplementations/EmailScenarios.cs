using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using EfficientRequestHandling.RequestBuilders;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using EfficientRequestHandling;

namespace ScenarioImplementations
{
    public static class EmailScenarios
    {
        /// <summary>
        /// Gets full mailbox content for each user in the tenant.
        /// </summary>
        /// <see cref="GroupScenarios.GetAllGroupsWithMembers(RequestManager)GetAllGroupsWithMembers">The approach taken here is the same as in the scenario for downloading all groups with members.</see>
        /// <param name="requestManager"></param>
        /// <returns></returns>
        public static IEnumerable<User> GetAllUsersWithCompleteMailboxes(RequestManager requestManager)
        {
            // Step 1: start downloading user objects using partitioning. We need to get users first, so we can build requests for Messages
            
            // This will return users as they become available from concurrent response handlers
            IEnumerable<User> users;
            using (var builder = GraphRequestBuilder<User>.GetBuilder<UserCollectionResponseHandler>(requestManager, out users))
            {
                // initialize a stream for each partition (e.g. split users by UPN)
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("userPrincipalName"))
                {
                    builder.Users.Request().Top(999).Filter(filter).GetAsync().Wait();
                }
            }
            // Step 2: as groups come in, create a request to download members
            IEnumerable<User> usersWithMailboxes;
            using (var builder = UserNestedCollectionsRequestBuilder.GetBuilder(requestManager, out usersWithMailboxes))
            {
                foreach (var user in users)
                {
                    // initiate the group request
                    builder.Messages(user).Request().Top(999).GetAsync().Wait();
                }
            }
            return usersWithMailboxes;
        }

        public static IEnumerable<Message> GetEmailsForSingleUser(RequestManager requestManager, string userId)
        {
            IEnumerable<Message> messages;
            using (var builder = GraphRequestBuilder<Message>.GetBuilder<MessageCollectionPartitioningResponseHandler>(requestManager, out messages))
            {
                // initialize request to download the entire mailbox
                builder.Users[userId].Messages.Request().GetAsync().Wait();
            }
            return messages;
        }
    }
}
