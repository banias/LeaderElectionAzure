using System.Runtime.Serialization;

namespace WorkerRole1
{
    [DataContract]
    public enum TenantStatus
    {
        [EnumMember]
        Unknown,

        [EnumMember]
        Starting,

        [EnumMember]
        Started,

        [EnumMember]
        Stopped,

        [EnumMember]
        Recycling
    }
}