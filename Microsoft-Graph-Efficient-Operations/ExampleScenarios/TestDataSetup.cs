using EfficientGraphOperations.RequestManagement;
using EfficientGraphOperations.ResponseHandlers;
using EfficientGraphOperations.RequestBuilders;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EfficientGraphOperations;

namespace ExampleScenarios
{
    public static class TestDataSetup
    {
        public static IEnumerable<User> CreateRandomUsers(RequestManager requestManager, int countOfUsersToCreate, string domainName)
        {
            var users = GenerateRandomUsers(countOfUsersToCreate, domainName);

            IEnumerable<OperationResult<User>> results;
            using (var builder = GraphRequestBuilder<User>.GetBuilderForSingleOperation(requestManager, out results))
            {
                foreach (var user in users)
                {
                    builder.Users[user.Id].Request().ReturnNoContent().CreateAsync(user).Wait();
                }
            }
            results.SplitIntoCollections(r => r.IsSuccessful, out ICollection<OperationResult<User>> createdUsers, out ICollection<OperationResult<User>> errors);
            if (errors.Count > 0)
            {
                var ex = new InvalidOperationException($"{errors.Count} user creation operations failed. See data for details.");
                ex.Data.Add("errorDetails", errors);
                throw ex;
            }
            return createdUsers.Select(r => r.Item);
        }
        public static IEnumerable<Group> CreateGroupsWithRandomMembersOptimized(RequestManager requestManager, ICollection<User> usersToAdd, int groupCount, int memberCountForEachGroup)
        {
            if (memberCountForEachGroup > usersToAdd.Count)
            {
                throw new ArgumentException($"The user collection contains fewer users ({usersToAdd.Count}) than the requested group member count: {memberCountForEachGroup}");
            }
            var groups = GenerateRandomGroups(groupCount);

#if BUGFIXED
            // TBD: group creation does not work via $batch due to a bug
            IEnumerable<OperationResult<Group>> groupCreationResults;
            using (var builder = GraphRequestBuilder<Group>.GetBuilderForSingleOperation(requestManager, out groupCreationResults))
            {
                foreach (var group in groups)
                {
                    builder.Groups[group.Id].Request().ReturnNoContent().CreateAsync(group).Wait();
                }
            }
            // execute group creation and check for errors
            groupCreationResults.SplitIntoCollections(r => r.IsSuccessful, out ICollection<OperationResult<Group>> successfulOperations, out ICollection<OperationResult<Group>> errors);
            if (errors.Count > 0)
            {
                var ex = new InvalidOperationException($"{errors.Count} group creation operations failed. See data for details.");
                ex.Data.Add("errorDetails", errors);
                throw ex;
            }
            var createdGroups = successfulOperations.Select(r => r.Item);
#else
            var createdGroups = groups.Select(g => requestManager.GraphClient.Groups[g.Id].Request().CreateAsync(g).Result).ToArray();
#endif

            // for each created group, add members
            IEnumerable<OperationResult<DirectoryObject>> memberResults;
            var random = new Random();    // to randomly select users to add to a group
            using (var builder = GraphRequestBuilder<DirectoryObject>.GetBuilderForSingleOperation(requestManager, out memberResults))
            {
                foreach (var group in createdGroups)
                {
                    var memberPool = usersToAdd.ToList();

                    for (int i = 0; i < memberCountForEachGroup; i++)
                    {
                        int userIndex = random.Next(memberPool.Count);
                        var userToAdd = memberPool[userIndex];
                        memberPool.RemoveAt(userIndex);
                        builder.Groups[group.Id].Members.References.Request().AddAsync(userToAdd).Wait();
                    }
                }
            }
            // force completion and check for errors
            memberResults.SplitIntoCollections(r => r.IsSuccessful, out ICollection<OperationResult<DirectoryObject>> addedMembers, out ICollection<OperationResult<DirectoryObject>> memberErrors);
            if (memberErrors.Count > 0)
            {
                var ex = new InvalidOperationException($"{memberErrors.Count} member addition operations failed. See data for details.");
                ex.Data.Add("errorDetails", memberErrors);
                throw ex;
            }
            return createdGroups;
        }

        public static IEnumerable<Group> CreateGroupsWithRandomMembersBasic(GraphServiceClient client, ICollection<User> usersToAdd, int groupCount, int memberCountForEachGroup)
        {
            if (memberCountForEachGroup > usersToAdd.Count)
            {
                throw new ArgumentException($"The user collection contains fewer users ({usersToAdd.Count}) than the requested group member count: {memberCountForEachGroup}");
            }
            var groups = GenerateRandomGroups(groupCount);

            var createdGroups = groups.Select(g => client.Groups[g.Id].Request().CreateAsync(g).Result);

            // for each created group, add members
            var random = new Random();    // to randomly select users to add to a group
            foreach (var group in createdGroups)
            {
                var memberPool = usersToAdd.ToList();

                for (int i = 0; i < memberCountForEachGroup; i++)
                {
                    int userIndex = random.Next(memberPool.Count);
                    var userToAdd = memberPool[userIndex];
                    memberPool.RemoveAt(userIndex);
                    client.Groups[group.Id].Members.References.Request().AddAsync(userToAdd).Wait();
                }
            }
            return createdGroups;
        }

        public static IEnumerable<OperationResult<User>> DeleteAllUsers(RequestManager requestManager)
        {
            //throw new NotImplementedException("You do not want to execute this by accident!");
            // Step 1: start downloading user objects using partitioning
            // This will return users as they become available from concurrent response handlers
            IEnumerable<User> users;
            using (var builder = GraphRequestBuilder<User>.GetBuilder<UserCollectionResponseHandler>(requestManager, out users))
            {
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("userPrincipalName"))
                {
                    builder.Users.Request().Select("id, userPrincipalName").Top(999).Filter(filter).GetAsync().Wait();
                }
            }
            IEnumerable<OperationResult<User>> results;
            using (var builder = GraphRequestBuilder<User>.GetBuilderForSingleOperation(requestManager, out results))
            {
                foreach (var user in users.Where(u => !u.Id.Equals("383d113a-4967-41a4-9d98-3dc9c255db2b", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!user.UserPrincipalName.EndsWith("petersgraphtest.onmicrosoft.com", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotImplementedException("You do not want to execute this by accident!");
                    }
                    builder.Users[user.Id].Request().ReturnNoContent().DeleteAsync().Wait();
                }
            }
            return results;
        }
        private static IEnumerable<User> GenerateRandomUsers(int userCount, string domainName)
        {
            while (userCount-- > 0)
            {
                string userName = GenericHelpers.GenerateRandomEntityName();
                User user = new User
                {
                    AccountEnabled = true,
                    MailNickname = userName,
                    DisplayName = userName,
                    PasswordProfile = new PasswordProfile()
                };
                user.PasswordProfile.Password = "Test1234";
                user.PasswordProfile.ForceChangePasswordNextSignIn = false;
                user.UserPrincipalName = $"{userName}@{domainName}";
                user.UsageLocation = "US";
                user.JobTitle = "RandomlyCreatedTestUser";
                yield return user;
            }
        }

        private static IEnumerable<Group> GenerateRandomGroups(int groupCount)
        {
            while (groupCount-- > 0)
            {
                string name = GenericHelpers.GenerateRandomEntityName();
                var group = new Group
                {
                    DisplayName = $"Random test group {name}",
                    Description = $"Random test group {name}",
                    MailNickname = name,
                    MailEnabled = false,
                    SecurityEnabled = true
                };
                yield return group;
            }
        }
    }
}
