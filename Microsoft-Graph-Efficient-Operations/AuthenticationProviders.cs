using Microsoft.Graph;
using Microsoft.Identity.Client;
using MicrosoftGraphEfficientPatterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MicrosoftGraphEfficientPatterns
{
    abstract class MyAuthenticationProvider
    {
        private bool isInitialized = false;
        private readonly object initializationLock = new object();

        protected abstract string InitializeAppAndGetFirstToken();
        protected abstract Task<string> GetTokenSilentlyAsync();

        async public Task<string> GetAccessTokenAsync()
        {
            if (!this.isInitialized)
            {
                lock (this.initializationLock)
                {
                    if (!this.isInitialized)
                    {
                        this.isInitialized = true;
                        return this.InitializeAppAndGetFirstToken();
                    }
                }
            }
            return await this.GetTokenSilentlyAsync();
        }
    }
    class UserAuthenticationProvider : MyAuthenticationProvider
    {
        private PublicClientApplication app;
        private IAccount account;
        async protected override Task<string> GetTokenSilentlyAsync()
        {
            return (await this.app.AcquireTokenSilentAsync(AuthSettings.scopes, this.account)).AccessToken;
        }

        protected override string InitializeAppAndGetFirstToken()
        {
            this.app = new PublicClientApplication(AuthSettings.applicationId, "https://login.microsoftonline.com/organizations/", new TokenCache());
            var authResult = this.app.AcquireTokenAsync(AuthSettings.scopes).Result;
            this.account = authResult.Account;
            return authResult.AccessToken;
        }
    }
    class AppOnlyAuthenticationProvider : MyAuthenticationProvider
    {
        private readonly static string[] scopes = new[] { "https://graph.microsoft.com/.default" };
        private ConfidentialClientApplication app;

        async protected override Task<string> GetTokenSilentlyAsync()
        {
            return (await this.app.AcquireTokenForClientAsync(scopes)).AccessToken;
        }

        protected override string InitializeAppAndGetFirstToken()
        {
            this.app = new ConfidentialClientApplication(AuthSettings.applicationId, $"https://login.microsoftonline.com/{AuthSettings.tenantId}", "https://microsoft.com", AuthSettings.secretClientCredentials, null, new TokenCache());
            return GetTokenSilentlyAsync().Result;
        }
    }
}
