using EfficientRequestHandling.RequestBuilders;
using EfficientRequestHandling.RequestManagement;
using Microsoft.Graph;

namespace EfficientRequestHandling.ResponseHandlers
{
    class GroupMembershipResponseHandler : CollectionWithNestingResponseHandler<Group, DirectoryObject>
    {
        public GroupMembershipResponseHandler(Group group, RequestManager requestManager, ResultAggregator<Group> resultAggregator)
            : base(group, typeof(GroupMembersCollectionResponseHandler), requestManager, resultAggregator)
        {
        }
        protected override ICollectionPage<DirectoryObject> SetNestedCollectionOnParentAndReturn()
        {
            this.parentItem.Members = new GroupMembersCollectionWithReferencesPage();
            return this.parentItem.Members;
        }
    }
    class UserMailboxResponseHandler : CollectionWithNestingResponseHandler<User, Message>
    {
        public UserMailboxResponseHandler(User user, RequestManager requestManager, ResultAggregator<User> resultAggregator)
            : base(user, typeof(MessageCollectionResponseHandler), requestManager, resultAggregator)
        {
        }
        protected override ICollectionPage<Message> SetNestedCollectionOnParentAndReturn()
        {
            this.parentItem.Messages = new UserMessagesCollectionPage();
            return this.parentItem.Messages;
        }
    }
}
