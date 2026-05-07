namespace MessageBroadcast.Core
{
    public class GroupInfo
    {
        public string GroupName { get; set; } = string.Empty;
        public List<DeviceInfo> GroupMembers { get; set; } = [];

        public int MemberCount => GroupMembers.Count;
    }
}
