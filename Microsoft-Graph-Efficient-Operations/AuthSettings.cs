using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoApp
{
    /// <summary>
    /// This class stores your SECRET app and tenat details to be used for authentication.
    /// Make sure not to include this in your Git repo!
    /// 
    /// Use the auto-create AuthSettingsLocal.cs to implement a static constructor that initializes the fields defined here. e.g.
    /// static AuthSettings()
    /// {
    ///     applicationId = <the actual id>
    ///     etc.
    /// }
    /// </summary>
    static partial class AuthSettings
    {
        public static readonly bool isUserAuthentication = true;                    // controls if we will try to authenticate as user, or as app. depends on the type of app and permissions you are using
        public static readonly string applicationId = "";                           // the Guid ID of your app registered with Azure AD
        public static readonly string[] scopes = null;                              // if the app uses delegated (user) permissions, list the scopes it needs to request here. otherwise, leave null
        public static readonly ClientCredential secretClientCredentials = null;     // initialize your secret client credentials. Certificate or "app password"
        public static readonly string tenantId = "";                                // the Guid ID of the tenant against which you will execute Graph calls.
    }
}
