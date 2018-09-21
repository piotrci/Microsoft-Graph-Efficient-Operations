using EfficientRequestHandling;
using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using ScenarioImplementations;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DemoApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // configure more open connections to graph, so we can leverage concurrency.
            ServicePointManager.DefaultConnectionLimit = 16;
            ServicePointManager.ReusePort = true;

            try
            {
                // set up authentication based on the config specified in AuthSettings.cs (you should have a local git-ignoder AuthSettingsLocal.cs file where you initialize the values
                var authProvider = AuthSettings.isUserAuthentication ? (MyAuthenticationProvider)new UserAuthenticationProvider() : (MyAuthenticationProvider)new AppOnlyAuthenticationProvider();
                GraphServiceClient client = GetAuthenticatedClient(authProvider);

                // set up logging
                EfficientRequestHandling.Logger.SetLogger(new OutputLogger(Console.OpenStandardOutput(), System.IO.File.Open("log.txt", FileMode.Create, FileAccess.Write, FileShare.Read)));

                // go to a specific scenario
                goto getAllUsers;

#pragma warning disable CS0164
#pragma warning disable CS0162
                #region User scenarios
                getAllUsers: ExecuteScenarioWithPerfMeasurement(client,
                  $"Downloading all users using parallelization.",
                  (requestManager) =>
                  {
                      var users = UserScenarios.GetAllUsers(requestManager).ToArray();
                      return $"Finished downloading users: {users.Length}.";
                  }, batchSize: 1);         // using batchSize=1 since this is more optimal with the paritioning approach used by this scenario.

                return;

                getAllUsersBasic: ExecuteScenarioWithPerfMeasurement(client,
                  $"Downloading all users using non-optimized paging pattern.",
                  (requestManager) =>
                  {
                      var users = UserScenarios.GetAllUsersBasic(client).ToArray();
                      return $"Finished downloading users: {users.Length}.";
                  });

                return;
                #endregion

                #region Group scenarios
                getAllGroupsWithMembers: ExecuteScenarioWithPerfMeasurement(client,
                 $"Geting all groups with members.",
                 (requestManager) =>
                 {
                     var groups = GroupScenarios.GetAllGroupsWithMembers(requestManager).ToArray();
                     return $"Finished downloading groups with members: {groups.Length}.";
                 }, batchSize: 1);         // using batchSize=1 since this is more optimal with the paritioning approach used by this scenario.

                return;
                #endregion

                #region Device scenarios

                getDeviceReport: ExecuteScenarioWithPerfMeasurement(client,
                 $"Downloading all device state to generate a report.",
                 (requestManager) =>
                 {
                     var report = DeviceScenarios.GetDeviceReport(requestManager);
                     return $"Finished generating device report:\n{report}";
                 }, batchSize: 1);

                return;

                #endregion

                #region Email scenarios
                getAllEmailsForOneUser: ExecuteScenarioWithPerfMeasurement(client,
                  $"Geting all emails for a single user.",
                  (requestManager) =>
                  {
                      var emails = EmailScenarios.GetEmailsForSingleUser(requestManager, client.Me.Request().GetAsync().Result.Id).ToArray();
                      return $"Finished downloading emails for one user: {emails.Length}.";
                  });

                return;

                getAllEmailsForAllUsers: ExecuteScenarioWithPerfMeasurement(client,
                  $"Geting all emails for all users.",
                  (requestManager) =>
                  {
                      var emails = EmailScenarios.GetAllUsersWithCompleteMailboxes(requestManager).ToArray();
                      return $"Finished downloading emails for all users: {emails.Length}.";
                  });

                return;
                #endregion

                #region Delta query scenarios
                deltaQueryOptimized: ExecuteScenarioWithPerfMeasurement(client,
                $"Executing efficient delta query cycle on user collection",
                (requestManager) =>
                {
                    DeltaQueryScenarios.UsersDeltaQueryEfficient(requestManager);
                    return $"Delta cycle complete.";
                });

                deltaQueryBasic: ExecuteScenarioWithPerfMeasurement(client,
                $"Executing traditional delta query cycle on user collection",
                (requestManager) =>
                {
                    DeltaQueryScenarios.UserDeltaQueryTraditional(client);
                    return $"Delta cycle complete.";
                });

                return;
                #endregion

                #region License management scenarios
                assignLicenses:
                var usersTemp = UserScenarios.GetAllUsersBasic(client).Take(1000).ToArray();
                ExecuteScenarioWithPerfMeasurement(client,
                    $"Assigning licenses.",
                   (requestManager) =>
                   {
                       var results = LicenseManagementScenarios.AssignLicensesToUsers(requestManager, usersTemp, "POWER_BI_STANDARD").ToArray();
                       var errors = results.Where(r => r.IsSuccessful == false).GroupBy(r => r.ErrorDetails.Error.Message).ToArray();
                       return $"Processed {results.Length}.";
                   });

                return;

                removeAllLicenses: ExecuteScenarioWithPerfMeasurement(client,
                   $"Removing licenses.",
                   (requestManager) =>
                   {
                       var users = UserScenarios.GetAllUsers(requestManager);
                       var results = LicenseManagementScenarios.RemoveLicensesFromUsers(requestManager, users, "POWER_BI_STANDARD").ToArray();
                       var errors = results.Where(r => r.IsSuccessful == false).GroupBy(r => r.ErrorDetails.Error.Message).ToArray();
                       return $"Processed {results.Length}.";
                   });

                return;

                assignLicensesBasic: ExecuteScenarioWithPerfMeasurement(client,
                  $"Assigning licenses the basic way.",
                  (requestManager) =>
                  {
                      var users = UserScenarios.GetAllUsers(requestManager).Take(1000).ToArray();
                      var results = LicenseManagementScenarios.AssignLicensesToUsersBasicApproach(client, "POWER_BI_STANDARD",
                          users).ToArray();
                      return $"Processed {results.Length}.";
                  });
                return;
                #endregion

                #region Test data setup
                createUsers: int usersToCreate = 5195;
                ExecuteScenarioWithPerfMeasurement(client,
                 $"Creating random users: {usersToCreate}",
                 (requestManager) =>
                 {
                     var createdUsers = TestDataSetup.CreateRandomUsers(requestManager, usersToCreate, "petersgraphtest.onmicrosoft.com").ToArray();
                     return $"User creation complete. Created: {createdUsers.Length}";
                 }, concurrencyLimit: 16);
                return;

                createGroups:
                User[] usersToAdd = null;
                ExecuteScenarioWithPerfMeasurement(client,
                  $"Downloading all users with optimizations.",
                  (requestManager) =>
                  {
                      usersToAdd = UserScenarios.GetAllUsers(requestManager).ToArray();
                      return $"Downloaded {usersToAdd.Length}.";
                  }, batchSize: 1);

                int groupsToCreate = 100;
                int memberCount = 100;

                ExecuteScenarioWithPerfMeasurement(client,
                    $"Creating random groups: {groupsToCreate}",
                    (requestManager) =>
                    {
                        var createdGroups = TestDataSetup.CreateGroupsWithRandomMembersOptimized(requestManager, usersToAdd, groupsToCreate, memberCount).ToArray();
                        return $"User creation complete. Created: {createdGroups.Length}";
                    }, concurrencyLimit: 2);

                return;
                #endregion
#pragma warning restore CS0164
#pragma warning restore CS0162
            }
            finally
            {
                EfficientRequestHandling.Logger.FlushAndCloseLogs();
            }
        }

        private static void ExecuteScenarioWithPerfMeasurement(GraphServiceClient client, string openingTitle, Func<RequestManager, string> executeAndSummarize, int batchSize = 20, int concurrencyLimit = 16)
        {
            using (var rm = new RequestManager(client, concurrencyLimit, batchSize))
            {
                EfficientRequestHandling.Logger.WriteLine($"Starting: {openingTitle}");
                var stopWatch = Stopwatch.StartNew();
                string summary = String.Empty;
                try
                {
                    summary = executeAndSummarize(rm);
                }
                catch (Exception ex)
                {
                    summary = ex.ToString();
                    throw;
                }
                finally
                {
                    EfficientRequestHandling.Logger.WriteLine($"Finished. Time elapsed: {stopWatch.Elapsed.ToString("c")}. Summary: {summary}.");
                }
            }
        }
        private static readonly string microsoftGraphV1 = @"https://graph.microsoft.com/v1.0";

        private static GraphServiceClient GetAuthenticatedClient(MyAuthenticationProvider provider)
        {
            GraphServiceClient client;

            client = new GraphServiceClient(
                microsoftGraphV1,
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        var token = await provider.GetAccessTokenAsync();
                        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
                    }));
            return client;
        }
    }
}
