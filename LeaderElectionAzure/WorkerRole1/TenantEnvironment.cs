using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WorkerRole1
{
    public class TenantEnvironment
    {
        public int TenantNumber { get; private set; }
        public string TenantId { get { return "tenant" + TenantNumber; } }
        public TenantStatus TenantStatus { get; set; }

        private static Random _random = new Random();
        private bool _isRecycling;
        private CloudStorageAccount _cloudStorageAccount;
        private CloudBlobClient _blobClient;

        public TenantEnvironment(int tenantId)
        {
            TenantNumber = tenantId;
            _cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            _blobClient = _cloudStorageAccount.CreateCloudBlobClient();            
      
        }

        public void Recycle()
        {
            Trace.TraceInformation("\t\t\tTenant {0} RECYCLE", TenantNumber);
            _isRecycling = true;
        }

        public void Run()
        {
            _isRecycling = false;
            TenantStatus = TenantStatus.Starting;
            Trace.TraceInformation("tenant {0} starting", TenantNumber);
            var container = _blobClient.GetContainerReference("locks");
            var blob = container.GetBlockBlobReference("tenant" + TenantNumber);

            var amITheLeader = AzureLeaderElectionProvider.AmITheLeader;
            var tenantStatus = GetTenantStatus();
            if (amITheLeader == false && tenantStatus != TenantStatus.Started)
            {
                Trace.TraceInformation("Tenant {0} not started on the leader \t\t Recycling tenant", TenantNumber);
                return; //recycle tenant
            }

            using (var arl = new AutoRenewLease(blob))
            {
                if (arl.HasLease)
                {
                    Trace.TraceInformation("tenant {0} setup...", TenantNumber);
                    Thread.Sleep(10000);
                    Trace.TraceInformation("tenant {0} setup done", TenantNumber);
                }
                else
                {
                    Trace.TraceInformation("Lock exception on tenant {0} \t Recycle tenant", TenantNumber);
                    return; // recycle tenant
                }
            } // lease is released here
            Trace.TraceInformation("tenant {0} started", TenantNumber);
            TenantStatus = TenantStatus.Started;
            while (true)
            {
                GenerateEvent();
                Tick();
                if (_isRecycling)
                {
                    return;
                }
            }
        }

        private void Tick()
        {
            Thread.Sleep(WorkerRole.delay);
        }

        private void GenerateEvent()
        {
            var value = _random.Next(0, int.MaxValue);
            if (value %30 == 0)
            {

            }
            //if (value%30 == 0)
            //{
            //    Trace.TraceInformation("\t\t\tRole {0} RECYCLE", TenantNumber);
            //    RoleEnvironment.RequestRecycle();
            //}
            Trace.TraceInformation("Tenant {0} Working", TenantNumber);
        }

        private TenantStatus GetTenantStatus()
        {
            try
            {
                var leaderAddress = AzureLeaderElectionProvider.GetLeaderInternalServiceAddress();
                var channelFactory = new ChannelFactory<ITenantService>(new NetTcpBinding());
                var channel = channelFactory.CreateChannel(new EndpointAddress(leaderAddress));

                return channel.GetTenantStatus(TenantId);
            }
            catch (EndpointNotFoundException e)
            {
                Trace.TraceInformation("Leader not started, recycling tenant");
               // RoleEnvironment.RequestRecycle();
                
            }

            return TenantStatus.Unknown;
        }
    }
}
