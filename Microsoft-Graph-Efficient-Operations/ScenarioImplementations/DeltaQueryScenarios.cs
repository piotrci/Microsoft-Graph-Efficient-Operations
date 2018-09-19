using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graph;

namespace ScenarioImplementations
{
    public class DeltaQueryScenarios
    {
        /// <summary>
        /// This is a "traditional" way of doing delta query. We download the currest state of the resource (Users collection) and then use delta links to pick up changes.
        /// The problem with this approach is that it cannot be optimized as we need to page the initial state sequentially. If there are a lot of users, this will take a while.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static IEnumerable<User> UserDeltaQueryTraditional(GraphServiceClient client)
        {
            //Initialize the delta query
            IUserDeltaRequest request = client.Users.Delta().Request().Top(999);
            // Step 1: populate the current state of users by following the nextLink in the collection
            IUserDeltaCollectionPage currentPage = ExecuteDeltaCycle(request);
            // Step 2: Get the deltaLink from the last response
            // Note: the Microsoft.Graph SDK doesn't fully implement delta, so we have to reach into the AdditionalData value bag
            IUserDeltaRequest deltaRequest = GetNextDeltaRequest(currentPage, client);
            // Step 3: After changes are made to users, execute the deltaRequest to pick them up
            return WaitForDeltaChanges(deltaRequest);
        }

        private static IEnumerable<User> WaitForDeltaChanges(IUserDeltaRequest deltaRequest)
        {
            Console.WriteLine("Make some changes to users and hit any key to proceed after making the changes");
            Console.ReadKey();
            IUserDeltaCollectionPage deltaChanges;
            do
            {
                Console.WriteLine("Polling for changes");
                Task.Delay(TimeSpan.FromSeconds(1));
                deltaChanges = deltaRequest.GetAsync().Result;
            } while (deltaChanges.Count < 1);
            return deltaChanges;
        }

        public static IEnumerable<User> UsersDeltaQueryEfficient(RequestManager requestManager)
        {
            // Step 1: make a request to get the latest delta token, without returning any results. The goal is to obtain the current token, and later download the current resource state - efficiently
            string latestDeltaTokenUrl = $"{requestManager.GraphClient.BaseUrl}/users/delta?$deltaToken=latest";
            var page = new UserDeltaCollectionPage();
            page.InitializeNextPageRequest(requestManager.GraphClient, latestDeltaTokenUrl);
            var emptyPage = page.NextPageRequest.GetAsync().Result;
            var firstDeltaRequest = GetNextDeltaRequest(emptyPage, requestManager.GraphClient);

            // Step 2: download the current state of the User collection, efficiently
            var currentStateDict = UserScenarios.GetAllUsers(requestManager).ToDictionary(u => u.Id);

            // Step 3: pick up delta changes since before we downloaded the current state
            var changesSinceCurrentState = ExecuteDeltaCycle(firstDeltaRequest);

            // Step 4: merge changes into current state 
            foreach (var change in changesSinceCurrentState)
            {
                currentStateDict[change.Id] = change;
            }

            // Step 5: we now have current state, get the delta link for future changes
            var deltaRequest = GetNextDeltaRequest(changesSinceCurrentState, requestManager.GraphClient);
            return WaitForDeltaChanges(deltaRequest);
            
        }
        private static IUserDeltaRequest GetNextDeltaRequest(IUserDeltaCollectionPage currentPage, IGraphServiceClient client)
        {
            string deltaLink = GetDeltaLinkFromPage(currentPage);
            currentPage.InitializeNextPageRequest(client, deltaLink);
            return currentPage.NextPageRequest;
        }

        private static IUserDeltaCollectionPage ExecuteDeltaCycle(IUserDeltaRequest request)
        {
            IUserDeltaCollectionPage currentUserState = new UserDeltaCollectionPage();
            IUserDeltaCollectionPage currentPage;
            do
            {
                currentPage = request.GetAsync().Result;
                foreach (var user in currentPage)
                {
                    currentUserState.Add(user);
                }
                request = currentPage.NextPageRequest;
            } while (request != null);
            currentUserState.AdditionalData = currentPage.AdditionalData;
            return currentUserState;
        }

        private static string GetDeltaLinkFromPage(IUserDeltaCollectionPage currentPage)
        {
            if (!currentPage.AdditionalData.TryGetValue("@odata.deltaLink", out object value))
            {
                throw new InvalidOperationException("The last page response did not contain the deltaLink.");
            }

            return (string)value;
        }
    }
}
