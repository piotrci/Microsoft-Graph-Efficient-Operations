using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EfficientGraphOperations.RequestManagement
{
    static class BatchFactory
    {
        public static HttpRequestMessage MakeBatchRequest(IEnumerable<RequestItem> requestItems, string baseUri, out TimeSpan delaySend)
        {
            delaySend = TimeSpan.Zero;
            var jsonObjects = new List<JObject>(20);
            foreach (var ri in requestItems)
            {
                if (ri.ExecutionDelay > delaySend)
                {
                    delaySend = ri.ExecutionDelay;
                }
                // make resource path relative
                var requestUrl = ri.Request.RequestUri.ToString().Remove(0, baseUri.Length);
                var request = new JObject
                            {
                                { "url", requestUrl },
                                { "method", ri.Request.Method.ToString() },
                                { "id", ri.Id },
                                { "headers", CreateHeaders(ri.Request) }
                            };
                var bodyContent = ri.Request.Content?.ReadAsStringAsync().Result ?? String.Empty;
                if (!String.IsNullOrEmpty(bodyContent))
                {
                    request.Add("body", JObject.Parse(bodyContent));
                }
                jsonObjects.Add(request);
            }
            return WrapRequests(jsonObjects, baseUri);
        }
        private static JObject CreateHeaders(HttpRequestMessage r)
        {
            JObject finalHeaders = new JObject();
            //the line below is a bit crazy but we want to union all the headers and handle null cases
            var allHeaders = (r.Headers?.AsEnumerable() ?? new KeyValuePair<string,IEnumerable<string>>[0]).Union(r.Content?.Headers?.AsEnumerable() ?? new KeyValuePair<string, IEnumerable<string>>[0]);
            foreach (var header in allHeaders)
            {
                foreach (var value in header.Value)
                {
                    finalHeaders.Add(header.Key, value);
                }
            }
            return finalHeaders;
        }
        public static IEnumerable<RequestItem> UnpackBatchResponseAndCallback(HttpResponseMessage response, IDictionary<int, RequestItem> originalItems)
        {
            var batchResponses = JObject.Parse(response.Content.ReadAsStringAsync().Result);

            var throttlingRetries = new System.Collections.Concurrent.ConcurrentBag<RequestItem>();

            var p = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20
            };
            Parallel.ForEach(batchResponses["responses"], p,
                jsonResponse =>
                {
                    int responseStatusCode = jsonResponse["status"].Value<int>();
                    int id = jsonResponse["id"].Value<int>();
                    // find the corresponding request item
                    if (!originalItems.TryGetValue(id, out RequestItem ri))
                    {
                        throw new InvalidOperationException($"Did not find original request item for item in batch response with id: {id}. This is unexpected: batch responses should always reference the original request items.");
                    }
                    // if got throttled, add to retries
                    if (responseStatusCode == 429)
                    {
                        // set time delay for retry: if there is no retry-after header, default to 5 seconds
                        ri.ExecutionDelay = TimeSpan.FromSeconds(jsonResponse["headers"]?["Retry-After"]?.Value<int>() ?? 5);
                        throttlingRetries.Add(ri);
                    }
                    else
                    {
                        var r = GetResponseFromJson(jsonResponse, responseStatusCode);
                        r.RequestMessage = ri.Request;
                        // call back with the response
                        ri.CallbackWithResponse(r);
                    }
                }
            );
            return throttlingRetries;
        }
        
        private static HttpRequestMessage WrapRequests(IEnumerable<JObject> requests, string baseUrl)
        {
            var batchUrl = baseUrl + "/$batch";
            var request = new HttpRequestMessage(HttpMethod.Post, batchUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            JArray batchRequests = new JArray(requests);
            JObject batchPayload = new JObject(new JProperty("requests", batchRequests));
            request.Content = new StringContent(JsonConvert.SerializeObject(batchPayload), Encoding.UTF8, "application/json");
            return request;
        }
        private static HttpResponseMessage GetResponseFromJson(JToken json, int statusCode)
        {
            var m = new HttpResponseMessage((HttpStatusCode)statusCode);
            if (json["headers"] != null)
            {
                foreach (var header in json["headers"].Cast<JProperty>())
                {
                    string name = header.Name;
                    string value = header.Value.ToString();
                    try
                    {
                        m.Headers.Add(name, value);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            if (json["body"] != null)
            {
                m.Content = new StringContent(json["body"].ToString(), Encoding.UTF8, "application/json");
            }
            return m;
        }
    }
}
