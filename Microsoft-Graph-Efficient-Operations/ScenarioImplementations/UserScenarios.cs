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
        /// <summary>
        /// Get all users in the organization, as fast as possible.
        /// </summary>
        /// <param name="requestManager">You can modify batchSize and concurrencyLevel in requestManager to change how requests are sent.
        /// Currently, it turns out that setting batchSize to 1 (essentially disabling batching), combined with a high concurrencyLevel (say 16), 
        /// results in best performance for GET operations
        /// </param>
        /// <remarks>
        /// Graph permissions required:
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/user_list
        /// </remarks>
        /// <returns></returns>
        public static IEnumerable<User> GetAllUsers(RequestManager requestManager)
        {
            // This collection will be populated with results as they become available. RequestManager executes requests in the background and asynchronously
            // adds them to this collection. The users are added to the collection immediately after they are returned and can be consumed via enumeration.
            // The underlying collection will block enumeration until the RequestManager is done processing all queued results
            IEnumerable<User> users;

            // The builder allows us to construct requests using standard Graph SDK syntax. However, instead of sending the requests it queues them with the RequestManager
            // for later execution in the background. The manager may batch multiple requests together for better performance.
            using (var builder = GraphRequestBuilder<User>.GetBuilder<UserCollectionResponseHandler>(requestManager, out users))
            {
                // We use filters to partition the large user collection into individual "streams". Graph supports efficient, indexed, queries on userPrinicpal name, so we use that property.
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("userPrincipalName"))
                {
                    // This is standard syntax of the Graph SDK. We "pretend" to send the request and wait for it, but in reality the GraphRequestBuilder class does not execute the request here.
                    // Instead, it queues it up with the RequestManager for background execution. The reason we call GetAsync().Wait() is to force the code internal to the Graph SDK
                    // to fully execute and build the final request.
                    // The Top() expression is used to maximize the size of each result page. 999 is the maximum size for the User collection.
                    builder.Users.Request().Top(999).Filter(filter).GetAsync().Wait();

                    // Note that with normal SDK usage the above call would only give us the first page of results.
                    // However, this particular builder is specialized to handling collections. The internal response handler will receive the first page and automatically
                    // queue up another request (internally) until all pages are retrieved.
                }
            }
            return users;
        }
        /// <summary>
        /// This demonstrates the traditional, "simple but slow" method of enumerating a collection. It can be used for comparison with the optimized approach.
        /// </summary>
        /// <param name="client"></param>
        /// /// <remarks>
        /// Graph permissions required:
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/user_list
        /// </remarks>
        /// <returns></returns>
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
