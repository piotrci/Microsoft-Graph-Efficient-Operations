using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using EfficientRequestHandling.RequestBuilders;
using Microsoft.Graph;
using System.Collections.Generic;
using EfficientRequestHandling;

namespace ScenarioImplementations
{
    public static class UserScenarios
    {
        public static IEnumerable<User> GetAllUsers(RequestManager requestManager)
        {
            // Step 1: start downloading user objects using partitioning
            // This will return users as they become available from concurrent response handlers
            IEnumerable<User> users;
            using (var builder = GraphRequestBuilder<User>.GetBuilder<UserCollectionResponseHandler>(requestManager, out users))
            {
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("userPrincipalName"))
                {
                    builder.Users.Request().Top(999).Filter(filter).GetAsync().Wait();
                }
            }
            return users;
        }
        public static IEnumerable<User> GetAllUsersBasic(GraphServiceClient client)
        {
            int currentCount = 0;
            var request = client.Users.Request().Top(999);
            while (request != null)
            {
                var response = request.GetAsync().Result;
                foreach (var item in response)
                {
                    if (++currentCount % 1000 == 0)
                    {
                        Logger.WriteLine($"Downloaded users: {currentCount}");
                    }
                    yield return item;
                }
                request = response.NextPageRequest;
            }
        }
    }
}
