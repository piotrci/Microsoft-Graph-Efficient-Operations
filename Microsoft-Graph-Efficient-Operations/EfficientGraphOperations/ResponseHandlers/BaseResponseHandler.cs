using EfficientRequestHandling.RequestBuilders;
using EfficientRequestHandling.RequestManagement;
using Microsoft.Graph;
using System;
using System.Net;
using System.Net.Http;

namespace EfficientRequestHandling.ResponseHandlers
{
    public abstract class BaseResponseHandler<TResponse> : IDisposable
    {
        protected virtual bool IgnoreHttpError(HttpStatusCode code)
        {
            return false;
        }
        protected readonly RequestManager requestManager;
        //private readonly IInputForResultAggregator<TResponse> resultAggregator;
        private readonly ResultAggregator<TResponse> resultAggregator;
        public BaseResponseHandler(RequestManager rm, ResultAggregator<TResponse> ra)
        {
            this.requestManager = rm;
            this.requestManager.RegisterForCancellation(this.OnManagerCancellation);
            this.resultAggregator = ra;
            this.resultAggregator.RegisterResponseHandler += this.EmptyHandler;

        }
        private void EmptyHandler(object sender, EventArgs ea) { }
        public virtual void InitializeRequest(HttpRequestMessage request)
        {
            requestManager.QueueRequest(request, this.ProcessResponse);
        }
        protected abstract void ProcessResponse(HttpResponseMessage rawResponse);

        protected bool CheckForErrors(HttpResponseMessage rawResponse)
        {
            if (!rawResponse.IsSuccessStatusCode)
            {
                this.Unregister();
                if (!this.IgnoreHttpError(rawResponse.StatusCode))
                {
                    throw new ErrorResponseException($"Service returned error code: {rawResponse.StatusCode}. See Data for response message.", rawResponse.Content.ReadAsStringAsync().Result);
                }
                else
                {
                    Logger.WriteLine($"Got error response with ignorable error: {rawResponse.StatusCode}");
                    return true;
                }
            }
            return false;
        }
        public void OnManagerCancellation()
        {
            this.Unregister();
        }
        protected void ReturnResponse(TResponse response)
        {
            this.resultAggregator.AddResult(response);
        }
        virtual protected void Unregister()
        {
            this.resultAggregator.RegisterResponseHandler -= this.EmptyHandler;
        }
        public void Dispose()
        {
            this.Unregister();
        }
        public static BaseResponseHandler<TResponse> GetHandler(Type handlerType, RequestManager rm, ResultAggregator<TResponse> ra)
        {
            object handler;
            switch (handlerType)
            {
                case Type t when t == typeof(GroupCollectionResponseHandler):
                    handler = new GroupCollectionResponseHandler(rm, ra as ResultAggregator<Group>);
                    break;
                case Type t when t == typeof(GroupMembersCollectionResponseHandler):
                    handler = new GroupMembersCollectionResponseHandler(rm, ra as ResultAggregator<DirectoryObject>);
                    break;
                case Type t when t == typeof(UserCollectionResponseHandler):
                    handler = new UserCollectionResponseHandler(rm, ra as ResultAggregator<User>);
                    break;
                case Type t when t == typeof(MessageCollectionResponseHandler):
                    handler = new MessageCollectionResponseHandler(rm, ra as ResultAggregator<Message>);
                    break;
                case Type t when t == typeof(MessageCollectionPartitioningResponseHandler):
                    handler = new MessageCollectionPartitioningResponseHandler(rm, ra as ResultAggregator<Message>);
                    break;
                case Type t when t == typeof(DeviceCollectionResponseHandler):
                    handler = new DeviceCollectionResponseHandler(rm, ra as ResultAggregator<Device>);
                    break;
                default:
                    throw new NotImplementedException($"Type {handlerType.FullName} is not implemented by this factory. The factory needs to be updated.");
            }
            return (BaseResponseHandler<TResponse>)handler;
        }
    }
    public class ErrorResponseException : Exception
    {
        public ErrorResponseException(string message, string response) : base(message)
        {
            this.Data.Add("response", response);
        }
    }
}
