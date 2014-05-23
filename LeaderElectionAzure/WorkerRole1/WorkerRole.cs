using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System.Timers;
using CloudStorageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private const int TenantCount = 4;
        public static int delay = 500;
        private ServiceHost host;

        public static ConcurrentDictionary<string, TenantEnvironment> TenantStatuses = new ConcurrentDictionary<string, TenantEnvironment>();

        public override void Run()
        {
            Trace.TraceInformation("Node started");
            var address = string.Format("net.tcp://{0}",
                                        (RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["InternalService"]
                                            .IPEndpoint));
            host = new ServiceHost(typeof(TenantService), new Uri(address));
            host.AddDefaultEndpoints();
            host.Open();

            new Task(() => Parallel.For(0, TenantCount, i =>
                {
                    var tenant = new TenantEnvironment(i);
                    TenantStatuses[tenant.TenantId] = tenant;
                    while (true)
                    {
                        TenantStatuses[tenant.TenantId].TenantStatus = TenantStatus.Unknown;
                        tenant.Run();
                        Trace.TraceInformation("Tenant {0} crashed", tenant.TenantNumber);
                        Thread.Sleep(1000);
                    }
                })).Start();
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("WorkerRole1 entry point called", "Information");
            var timer = new System.Timers.Timer(5000);
            timer.Elapsed += timer_Elapsed;
            timer.Start();
            while (true)
            {
                Thread.Sleep(10000);
                Trace.TraceInformation("Working", "Information");
            }


        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (AzureLeaderElectionProvider.AmITheLeader)
            {
                Trace.TraceInformation("I am the leader !");
                return;
            }
            Trace.TraceInformation("Slave !");
        }

        public override bool OnStart()
        {
            RoleEnvironment.StatusCheck +=RoleEnvironment_StatusCheck;
            //Kill instance 0
            //RoleEnvironment.StatusCheck += RoleEnvironment_StatusCheck;
            //while (RoleEnvironment.CurrentRoleInstance.Id.EndsWith("0"))
            //{
                
            //}
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            // open internal 

            Trace.TraceInformation("Starting node");

            Thread.Sleep(5000);
            var _cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var _blobClient = _cloudStorageAccount.CreateCloudBlobClient();
            var container = _blobClient.GetContainerReference("locks");
            container.CreateIfNotExists();

            var tableClient = _cloudStorageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference("masterReference");
            table.CreateIfNotExists();

            return base.OnStart();


        }

        void RoleEnvironment_StatusCheck(object sender, RoleInstanceStatusCheckEventArgs e)
        {
            if(AzureLeaderElectionProvider.AmITheLeader)
                return;
            bool isBusy = false;
            foreach (var tenants in TenantStatuses)
            {
                isBusy = isBusy || GetTenantStatus(tenants.Key) != TenantStatus.Started;
            }
            if(isBusy)
                e.SetBusy();
        }

        private TenantStatus GetTenantStatus(string tenantId)
        {
            try
            {
                var leaderAddress = AzureLeaderElectionProvider.GetLeaderInternalServiceAddress();
                var channelFactory = new ChannelFactory<ITenantService>(new NetTcpBinding());
                var channel = channelFactory.CreateChannel(new EndpointAddress(leaderAddress));

                return channel.GetTenantStatus(tenantId);
            }
            catch (EndpointNotFoundException e)
            {
                Trace.TraceInformation("Leader not started, recycling tenant");
                // RoleEnvironment.RequestRecycle();

            }

            return TenantStatus.Unknown;
        }
    }

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

    [DataContract]
    public class RoleStatus
    {
        [DataMember]
        public Dictionary<string, TenantStatus> Tenants { get; set; }
    }

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
