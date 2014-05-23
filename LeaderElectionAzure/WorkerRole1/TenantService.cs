using System.Diagnostics;
using System.Linq;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WorkerRole1
{
    public class TenantService : ITenantService
    {
        public void Dummy()
        {
            Trace.TraceInformation("Ayyyy !");
        }

        public TenantStatus GetTenantStatus(string tenantId)
        {
            TenantEnvironment result;
            return WorkerRole.TenantStatuses.TryGetValue(tenantId, out result) ? result.TenantStatus : TenantStatus.Unknown;
        }

        public RoleStatus GetRoleStatus()
        {
            return new RoleStatus()
                {
                    Tenants = WorkerRole.TenantStatuses.ToDictionary(k => k.Key, v => v.Value.TenantStatus)
                };
        }

        public bool IsAlive()
        {
            return true;
        }

        public void KillRole()
        {
            RoleEnvironment.RequestRecycle();
        }

        public void KillTenant(string tenantId)
        {
            WorkerRole.TenantStatuses[tenantId].Recycle();
        }
    }
}