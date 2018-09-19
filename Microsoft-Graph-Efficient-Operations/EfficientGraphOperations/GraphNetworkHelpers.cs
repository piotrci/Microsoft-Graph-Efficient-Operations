using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EfficientGraphOperations
{
    public static class GraphNetworkHelpers
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static GraphNetworkHelpers()
        {
            httpClient.Timeout = TimeSpan.FromMinutes(3);
        }

        private const int maxThrottlingRetries = 3;
        private const int maxNetworkErrorRetries = 3;
        private const int networkErrorRetryDelayInSec = 5;

        async public static Task<HttpResponseMessage> SendWithRetriesAsync(HttpRequestMessage originalRequest, IAuthenticationProvider authProvider, CancellationToken ct, TimeSpan delaySend = default(TimeSpan))
        {
            // caller may have requested a delay
            if (delaySend > TimeSpan.Zero)
            {
                await Task.Delay(delaySend, ct);
                Logger.WriteLine($"Resuming delayed request after {delaySend.TotalSeconds} seconds.");
            }
            int throttlingRetryAttempt = 0;
            int networkErrorAttempt = 0;
            HttpResponseMessage responseMessage;
            // in this loop we retry on any type of error. we count throttling errors and network errors separately
            // throttle errors are to be expected and retried up to N times. each retry has up to M retries due to low level error errors
            while (true)
            {
                TimeSpan retryDelay;
                var request = await CloneHttpRequestMessageAsync(originalRequest);
                // authenticate the request before sending
                await authProvider.AuthenticateRequestAsync(request);
                try
                {
                    responseMessage = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                }
                // low level networking errors or cancelation due to timeout (TaskCanceledException is thrown)
                catch (Exception ex) when(ex is HttpRequestException || ex is TaskCanceledException)
                {
                    if (networkErrorAttempt++ < maxNetworkErrorRetries && !ct.IsCancellationRequested) 
                    {
                        retryDelay = TimeSpan.FromSeconds(networkErrorRetryDelayInSec * networkErrorAttempt);
                        Logger.WriteLine($"Network error detected. Will retry after {retryDelay.TotalSeconds}s. Retry attempt no {networkErrorAttempt}. Error: {ex.Message}");
                        // retry loop
                        continue;
                    }
                    else
                    {
                        throw ex;
                    }
                }
                // response has been received. 
                if (responseMessage.IsSuccessStatusCode)
                {
                    return responseMessage;
                }
                //let's check if it is in one of the error states we can recover from
                if (ShouldRetryOnThrottling(responseMessage, throttlingRetryAttempt, out retryDelay))
                {
                    ++throttlingRetryAttempt;
                    networkErrorAttempt = 0;
                    Logger.WriteLine($"Service throttling detected. Will retry after {retryDelay.TotalSeconds}s. Retry attempt no {throttlingRetryAttempt}");
                }
                else if (ShouldRetryOnErrorResponseOrNetworkFailure(responseMessage, networkErrorAttempt, out retryDelay))
                {
                    ++networkErrorAttempt;
                    Logger.WriteLine($"Retryable error code: {responseMessage.StatusCode}. Will retry after {retryDelay.TotalSeconds}s. Retry attempt no {networkErrorAttempt}");
                }
                else
                {
                    // we should not retry, return the response as is
                    return responseMessage;
                }
                // if we made it here, we need to wait and retry the loop
                Logger.WriteLine($"Waiting {retryDelay.TotalSeconds} to retry network request.");
                await Task.Delay(retryDelay).ConfigureAwait(false);
            }
        }

        private static bool ShouldRetryOnErrorResponseOrNetworkFailure(HttpResponseMessage response, int networkErrorAttempt, out TimeSpan retryDelay)
        {
            retryDelay = TimeSpan.MinValue;
            if (networkErrorAttempt >= maxNetworkErrorRetries)
            {
                return false;
            }

            TimeSpan calculateDelay() =>
                    response.Headers?.RetryAfter?.Delta ?? TimeSpan.FromSeconds(networkErrorRetryDelayInSec * (networkErrorAttempt + 1));

            // retry only on certain supported status codes
            switch (response.StatusCode)
            {
                case HttpStatusCode.RequestTimeout:   // this happens sometimes (randomly or when using Fiddler)
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.ServiceUnavailable:
                    retryDelay = calculateDelay();
                    return true;
                // 401 may mean that our token expired, so we should retry immediately as this should trigger acquiring of a new token
                case HttpStatusCode.Unauthorized:
                    retryDelay = TimeSpan.Zero;
                    return true;
                // do not retry on unknown error codes
                default:
                    return false;
            }
        }

        private static bool ShouldRetryOnThrottling(HttpResponseMessage response, int throttlingRetryAttempt, out TimeSpan retryDelay)
        {
            if (response.StatusCode == (HttpStatusCode)429 && throttlingRetryAttempt < maxThrottlingRetries)
            {
                // we expect a value to exist always when 429 is returned. otherwise an exception will be thrown and we want that.
                retryDelay = response.Headers.RetryAfter.Delta.Value;
                return true;
            }
            retryDelay = TimeSpan.MinValue;
            return false;
        }

        public static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
        {
            HttpRequestMessage clone = new HttpRequestMessage(req.Method, req.RequestUri);
            var ms = new System.IO.MemoryStream();
            if (req.Content != null)
            {
                await req.Content.CopyToAsync(ms).ConfigureAwait(false);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);

                // Copy the content headers
                if (req.Content.Headers != null)
                    foreach (var h in req.Content.Headers)
                        clone.Content.Headers.Add(h.Key, h.Value);
            }
            clone.Version = req.Version;

            foreach (KeyValuePair<string, object> prop in req.Properties)
                clone.Properties.Add(prop);

            foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            return clone;
        }
    }
}
