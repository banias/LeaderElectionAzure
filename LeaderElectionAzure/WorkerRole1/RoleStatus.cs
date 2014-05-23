using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WorkerRole1
{
    [DataContract]
    public class RoleStatus
    {
        [DataMember]
        public Dictionary<string, TenantStatus> Tenants { get; set; }
    }
}