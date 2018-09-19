using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfficientRequestHandling.RequestBuilders
{
    public class UserNestedCollectionsRequestBuilder : NestedRequestBuilder<User>
    {
        private UserNestedCollectionsRequestBuilder(RequestManager rm, ResultAggregator<User> ra) : base(rm, ra) { }
        public IUserMessagesCollectionRequestBuilder Messages(User user)
        {
            var builder = new GraphRequestBuilder<User>(this.resultAggregator, () => new UserMailboxResponseHandler(user, requestManager, resultAggregator));
            return builder.Users[user.Id].Messages;
        }
        #region Factories
        public static UserNestedCollectionsRequestBuilder GetBuilder(RequestManager rm, out IEnumerable<User> results)
        {
            var ra = new ResultAggregator<User>(rm.GetCancellationToken());
            results = ra;
            return new UserNestedCollectionsRequestBuilder(rm, ra);
        }
        #endregion
    }
}
