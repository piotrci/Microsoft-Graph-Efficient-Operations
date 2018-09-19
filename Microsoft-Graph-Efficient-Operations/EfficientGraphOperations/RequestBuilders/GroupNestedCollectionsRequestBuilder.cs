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
    public class GroupNestedCollectionsRequestBuilder : NestedRequestBuilder<Group>
    {
        private GroupNestedCollectionsRequestBuilder(RequestManager rm, ResultAggregator<Group> ra) : base(rm, ra) { }
        public IGroupMembersCollectionWithReferencesRequestBuilder Members(Group group)
        {
            var builder = new GraphRequestBuilder<Group>(this.resultAggregator, () => new GroupMembershipResponseHandler(group, requestManager, resultAggregator));
            return builder.Groups[group.Id].Members;
        }
        #region Factories
        public static GroupNestedCollectionsRequestBuilder GetBuilder(RequestManager rm, out IEnumerable<Group> results)
        {
            var ra = new ResultAggregator<Group>(rm.GetCancellationToken());
            results = ra;
            return new GroupNestedCollectionsRequestBuilder(rm, ra);
        }
        #endregion
    }
}
