# Microsoft-Graph-Efficient-Operations

Code demonstrating how you can efficiently access and modify Microsoft 365 data using Microsoft Graph APIs. Parallelization and batching patterns are demonstrated.

## Overview

This console application can be used to execute sample scenarios that perform bulk operations on tenant's Microsoft 365 data, such as users, groups, email messages, etc, using Microsoft Graph APIs($$$link to MS Graph website).

The implementation demonstrates how we can optimize interaction with Graph to drammatically reduce the time of bulk operations at scale. For example, using a traditional, sequential approach it takes around 5.5 minutes to get 100,000 users using Graph or the equivalent PowerShell cmdlets (e.g. Get-AzureAdUser). Using parallelization and batching, as shown in `UserScenarios.GetAllUsers` it takes only  18 seconds (a 18X improvement in execution time).

Check out this Microsoft Ignite 2018 talk for an overview of this approach: [THR2159 - How to perform large scale operations efficiently using Microsoft Graph](https://myignite.techcommunity.microsoft.com/sessions/65997?source=sessions#ignite-html-anchor)

## TBD: How to make this run (e.g. set up your app and authentication)

You can play around with the samples scenarios by executing the `DemoApp` console application. The code supports simple authentication using either user delegated mode (where the code runs under user permissions) or app-only mode (the app itself has permissions and does not require user sign in). Both are useful depending on the scenarios you want to try out (e.g. to be able to get all users' mailboxes you must use app only permissions, with admin consent).

The project contains a file `AuthSettings.cs` which defines some app and authentication related fields. You should create a **local only** file `AuthSettingsLocal.cs` and make sure it is listed in `.gitignore` so you do not accidentally commit any secrets or other private data into the repo.

In this file, define the constructor for the partial class and set the values of the various properties:

```csharp
 static partial class AuthSettings
    {
        static AuthSettings()
        {
            isUserAuthentication = <true/false>;                    // controls if we will try to authenticate as user, or as app. depends on the type of app and permissions you are using
            applicationId = <guid>;                           // the Guid ID of your app registered with Azure AD
            scopes = <a string array>;                              // if the app uses delegated (user) permissions, list the scopes it needs to request here. otherwise, leave null
            secretClientCredentials = new ClientCredential(<the app secret or certificate>); ;     // initialize your secret client credentials. Certificate or "app password"
            tenantId = <guid>;                                // the Guid ID of the tenant against which you will execute Graph calls.
        }
```

## Design details

These are the main components of the solution:

### EfficientRequestHandling

#### RequestManager

The `RequestManager` class is at the core of the solution. It manages a background task that executes Microsoft Graph requests efficiently. It uses parallelization to use multiple network connections to Graph to increase request throughput. Internally, it aggregates multiple requests into batches (using the $batch capability in Graph) - this allows us to optimize the scenarios where we have many small requests (such as modifying a lot of users). It also internally handles basic network errors and throttling, and implements retries.

The main goal of the `RequestManager` is to abstract away the complexity of parallel execution and batch management. The class doesn't know about particular types of requests, it only executes them. It allows you to build and queue your requests using the standard Graph client SDK for .NET ($$$Link). Internally, it uses specialized response handlers to return results. Those handlers can be specialized - for example `CollectionResponseHandler` knows how to interpret responses for collections (e.g. Users, Groups, Messages) - which use pages of results - and queues more requests with `RequestManager` to enumerate an entire collection.

Note that since `RequestManager` is agnostic of the types of requests it processes, you can use a single instance to queue many different requests, in any order. The manager will process them from the queue, and may batch them together. You can create separate instances of the manager if you want to handle different types of requests separately, for example to differently configure the level of concurrency or the size of batches.

You use the `Dispose()` method to tell the manager when to stop accepting more requests to the queue and start completing all outstanding requests. It's a good idea to instantiate the manager in a `using` block to make sure it terminates correctly.

#### GraphRequestBuilder

The `GraphRequestBuilder` allows you to use standard Graph client SDK syntax to build your requests. This is a great alternative to constructing your own REST requests and handling responses.

The class derives from `GraphServiceClient` but it implements additional code to communicate with the `RequestManager`. A key, but not very pretty, component is the `DummyHttpProvider` which is used to intercept the requests that `GraphServiceClient` would normally send over the network, and queue them with the `RequestManager` instead. At the moment, that is the only way to fully leverage the Graph SDK request building capabilities.

Note that for this reason, when building requests it is necessary to "pretend" to fully execute them and await them - this guarantees that the underlying Graph SDK code is fully executed and any errors related to request building are thrown on the executing thread. Here is an example of how this is done - the request is not actually executed, since the builder captures it internally and queues it up for background execution:

```csharp
    graphRequestBuilder.Users.Request().Top(999).Filter(filter).GetAsync().Wait();
```

You use the `Dispose()` method to tell the builder that you are done adding more requests to the queue. It's a good idea to instantiate the builder in a `using` block to make sure it correctly signals that no more requests are possible. Otherwise, the internal code will wait indefinitely expecting more requests to come in.

#### ResultAggregator

`ResultAggregator` is used to deliver results back to your code. Internally, it uses `BlockingCollection<T>` which enables an easy to work with programming model. You can simply enumerate your results and your thread will be blocked if we are waiting for the Graph service to respond with more results. Response handlers add results to the aggregator as soon as they become available, so your code can continue executing its business logic without having to wait for everything to complete.

There are factory methods on the "builder" classes that return an instance of an enumerator for you to use - the enumerators are backed by a `ResultAggregator` instance.

#### How all this comes together

A simple scenario implementation look like this (see comments inline):

```csharp
public static IEnumerable<User> GetAllUsers(RequestManager requestManager)  // RequestManager can be provided from the outside, e.g. if you want to share it accross your entire program
        {
            IEnumerable<User> users;    // this is where the results will start showing up
            using (var builder = GraphRequestBuilder<User>.GetBuilder<UserCollectionResponseHandler>(requestManager, out users))    // use factory method to get a builder for this request type. internally, a ResponseHandler and a ResultAggregator are created to plug into the RequestManager.
            {
                foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("userPrincipalName"))
                {
                    builder.Users.Request().Top(999).Filter(filter).GetAsync().Wait(); 
                    // We are using standard Graph SDK syntax to build the request.
                    // the .GetAsync().Wait() part doesn't actually cause the request to execute here.  DummyHttpProvider is used to intercept the request and queue it.
                    // It is important to call these methods: <verb>Async() causes the SDK to properly build the request. Wait() executes the faux-request here, to throw any exceptions - without it the code would continue even thought the request was not properly built.
                }
            }   // exitting the using block calls Dispose() on the builder, which tells it to stop queueing requests. This is important to make the result enumerator terminate, otherwise it will hang waiting for more potential responses.
            return users; // we are returning the result collection before the requests were executed. That is OK, the calling code can enumerate and wait, or it can decide to only take a few results and cancell the execution of the outstanding requests.
```

### ScenarioImplementations

This project contains sample implementations of scenarios, using the `EfficientRequestHandling` components. It demonstrates how you can use a simple pattern and retain the Graph SDK syntax to execute high volume bulk operations, fast. Sample scenarios include:

- `UserScenarios`: getting all users in a tenant.
- `GroupScenarios`: getting all groups with members.
- `LicenseManagementScenarios`: assigning or removing product licenses on individual users.
- `EmailScenarios`: getting full mailbox content for all users.
- `DeviceScenarios`: creating a report of all devices registered in the tenant.
- `DeltaQueryScenarios`: an optimized way to initialize a delta query for large resource collections (e.g. users or groups)

## TBD: Other remarks

### Why the mess with handlers, etc., various types - SDK driven, should get better

Parts of the code are more complex/ugly than they should be. SDK types
