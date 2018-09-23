using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using EfficientRequestHandling.RequestBuilders;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using EfficientRequestHandling;

namespace ScenarioImplementations
{
    public static class LicenseManagementScenarios
    {
        /// <summary>
        /// Adds a specific license to selected users.
        /// </summary>
        /// <param name="requestManager"></param>
        /// <param name="users">A collection of users for whom to add the license. Note that we only really need the Id value of each user, so this method could be refactored.</param>
        /// <param name="skuPartNumber">The name of the product, e.g. "ENTERPRISEPACK" for Office365 E3. We resolve this later to a SKU id (guid)</param>
        /// <returns></returns>
        public static IEnumerable<OperationResult<User>> AssignLicensesToUsers(RequestManager requestManager, IEnumerable<User> users, string skuPartNumber)
        {
            // Resolve the product name to the guid id
            var licenseId = GetLicenseId(requestManager.GraphClient, skuPartNumber);
            // Build the input parameter
            var licenseToAssign = new AssignedLicense { SkuId = licenseId, DisabledPlans = new Guid[0] };
            // Execute interesting code
            return ModifyLicensesForUsers(requestManager, users, new AssignedLicense[] { licenseToAssign }, new Guid[0]);
        }
        public static IEnumerable<OperationResult<User>> RemoveLicensesFromUsers(RequestManager requestManager, IEnumerable<User> users, string skuPartNumber)
        {
            var licenseId = GetLicenseId(requestManager.GraphClient, skuPartNumber);
            return ModifyLicensesForUsers(requestManager, users, new AssignedLicense[0], new Guid[] { licenseId });
        }
        /// <summary>
        /// Executes license modification requests on users. Uses parallelization and batching of requests to maximize throughput.
        /// </summary>
        /// <param name="requestManager"></param>
        /// <param name="users"></param>
        /// <param name="licensesToAdd"></param>
        /// <param name="licensesToRemove"></param>
        /// <returns></returns>
        private static IEnumerable<OperationResult<User>> ModifyLicensesForUsers(RequestManager requestManager, IEnumerable<User> users, IEnumerable<AssignedLicense> licensesToAdd, IEnumerable<Guid> licensesToRemove)
        {

            // This collection will contain the result of each individual request. Note that batches are handled internally by RequestManager, so you only deal with individual requests that were created here.
            // Operations can fail (e.g. you cannot remove a license the user does not have) - the OperationResult type let's you check if the operation failed and inspect the error response returned by the service.
            IEnumerable<OperationResult<User>> results;

            // This builder allows us to create single operations (i.e. ones that don't require collection handling).
            using (var builder = GraphRequestBuilder<User>.GetBuilderForSingleOperation(requestManager, out results))
            {
                foreach (var user in users)
                {
                    // Standard Graph SDK syntax. Again, the request is not actually executed here.
                    // ReturnNoContent() is a helper method that adds a header instructing Graph not to return the full user object after it is modified. This allows us to reduce the size 
                    // of the response to further speed things up.
                    builder.Users[user.Id].AssignLicense(licensesToAdd, licensesToRemove).Request().ReturnNoContent().PostAsync().Wait();
                }
            }
            return results;
        }
        private static Guid GetLicenseId(IGraphServiceClient client, string skuPartNumber)
        {
            // simplified - we assume an org would always return all of its subscribed skus in one response
            var allProducts = client.SubscribedSkus.Request().GetAsync().Result;
            var product = allProducts.First(p => p.SkuPartNumber.Equals(skuPartNumber, StringComparison.OrdinalIgnoreCase));
            return product.SkuId.Value;
        }
        /// <summary>
        /// Demonstrates the basic approach to license management operations: we process one user request at a time, without any optimizations
        /// </summary>
        /// <param name="client"></param>
        /// <param name="users">A collection of users for whom to add the license. Note that we only really need the Id value of each user, so this method could be refactored.</param>
        /// <param name="skuPartNumber">The name of the product, e.g. "ENTERPRISEPACK" for Office365 E3. We resolve this later to a SKU id (guid)</param>
        /// <returns></returns>
        public static IEnumerable<User> AssignLicensesToUsersBasic(GraphServiceClient client, string skuPartNumber, IEnumerable<User> users)
        {
            var licenseId = GetLicenseId(client, skuPartNumber);
            var licensesToAssign = new[] { new AssignedLicense { SkuId = licenseId, DisabledPlans = new Guid[0] } };
            var results = new List<User>();

            foreach (var user in users)
            {
                // Executes the request synchronously 
                results.Add(client.Users[user.Id].AssignLicense(licensesToAssign, new Guid[0]).Request().ReturnNoContent().PostAsync().Result);
            }
            return results;
        }
    }
}
