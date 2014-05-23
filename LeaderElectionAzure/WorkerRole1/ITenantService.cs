using System.ServiceModel;

namespace WorkerRole1
{
    [ServiceContract]
    public interface ITenantService
    {
        [OperationContract]
        void Dummy();

        [OperationContract]
        TenantStatus GetTenantStatus(string tenantId);

        [OperationContract]
        RoleStatus GetRoleStatus();

        [OperationContract]
        bool IsAlive();

        [OperationContract]
        void KillRole();

        [OperationContract]
        void KillTenant(string tenantId);
    }
}