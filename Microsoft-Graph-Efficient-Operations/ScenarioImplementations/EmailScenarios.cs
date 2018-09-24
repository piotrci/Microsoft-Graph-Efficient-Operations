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
        /// Gets full mailbox content for each user in the tenant. It uses a patters similar to the GetAllGroupsWithMembers scenario - see that one for a detailed description.
        /// </summary>
        /// <see cref="GroupScenarios.GetAllGroupsWithMembers(RequestManager)GetAllGroupsWithMembers">The approach taken here is the same as in the scenario for downloading all groups with members.</see>
        /// <seealso cref="UserScenarios.GetAllUsers(RequestManager)"/>
        /// <param name="requestManager"></param>
        /// <returns></returns>
        public static IEnumerable<User> GetAllUsersWithCompleteMailboxes(RequestManager requestManager)
        {
            // Step 1: start downloading user objects using partitioning. We need to get users first, so we can build requests for Messages
            // we use the same optimized approach for this as in the GetAllUsers scenario
            IEnumerable<User> users = UserScenarios.GetAllUsers(requestManager);

            // Step 2: as user objects become available, we queue requests for mailboxes

            // This collection will be populated with Users for whom we fetched complete mailboxes
            IEnumerable<User> usersWithMailboxes;

            // This builder supports creation of requests for collections embedded in the User object, such as Messages
            using (var builder = UserNestedCollectionsRequestBuilder.GetBuilder(requestManager, out usersWithMailboxes))
            {
                foreach (var user in users)
                {
                    // initiate the request to get messages.
                    // we use a value for Top smaller than the maximum (1000) as messages tend to be large and requests to Graph may time out.
                    builder.Messages(user).Request().Top(500).GetAsync().Wait();
                }
            }
            return usersWithMailboxes;
        }

        /// <summary>
        /// Gets a collection of all messages in the users mailbox.
        /// </summary>
        /// <see cref="GroupScenarios.GetAllMembersFromASingleGroup(RequestManager, string)">A similar approach taken here.</see>
        /// <param name="requestManager"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static IEnumerable<Message> GetEmailsForSingleUser(RequestManager requestManager, string userId)
        {
            IEnumerable<Message> messages;
            using (var builder = GraphRequestBuilder<Message>.GetBuilder<MessageCollectionPartitioningResponseHandler>(requestManager, out messages))
            {
                // initialize request to download the entire mailbox
                builder.Users[userId].Messages.Request().Top(500).GetAsync().Wait();
            }
            return messages;
        }
    }
}
