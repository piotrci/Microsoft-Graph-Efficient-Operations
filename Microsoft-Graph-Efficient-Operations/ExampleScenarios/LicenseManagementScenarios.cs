using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using EfficientRequestHandling.RequestBuilders;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using EfficientRequestHandling;

namespace ExampleScenarios
{
    public static class LicenseManagementScenarios
    {
        public static IEnumerable<OperationResult<User>> AssignLicensesToUsers(RequestManager requestManager, IEnumerable<User> users, string skuPartNumber)
        {
            var licenseId = GetLicenseId(requestManager.GraphClient, skuPartNumber);
            var licenseToAssign = new AssignedLicense { SkuId = licenseId, DisabledPlans = new Guid[0] };
            return ModifyLicensesForUsers(requestManager, users, new AssignedLicense[] { licenseToAssign }, new Guid[0]);
        }
        public static IEnumerable<OperationResult<User>> RemoveLicensesFromUsers(RequestManager requestManager, IEnumerable<User> users, string skuPartNumber)
        {
            var licenseId = GetLicenseId(requestManager.GraphClient, skuPartNumber);
            return ModifyLicensesForUsers(requestManager, users, new AssignedLicense[0], new Guid[] { licenseId });
        }
        private static IEnumerable<OperationResult<User>> ModifyLicensesForUsers(RequestManager requestManager, IEnumerable<User> users, IEnumerable<AssignedLicense> licensesToAdd, IEnumerable<Guid> licensesToRemove)
        {

            // modify license on each user
            IEnumerable<OperationResult<User>> results;
            using (var builder = GraphRequestBuilder<User>.GetBuilderForSingleOperation(requestManager, out results))
            {
                foreach (var user in users)
                {
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
        public static IEnumerable<User> AssignLicensesToUsersBasicApproach(GraphServiceClient client, string skuPartNumber, IEnumerable<User> users)
        {
            var licenseId = GetLicenseId(client, skuPartNumber);
            var licensesToAssign = new[] { new AssignedLicense { SkuId = licenseId, DisabledPlans = new Guid[0] } };
            var results = new List<User>();

            foreach (var user in users)
            {
                results.Add(client.Users[user.Id].AssignLicense(licensesToAssign, new Guid[0]).Request().ReturnNoContent().PostAsync().Result);
            }
            return results;
        }
    }
}
