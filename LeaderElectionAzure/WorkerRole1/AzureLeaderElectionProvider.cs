using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace WorkerRole1
{
    public class AzureLeaderElectionProvider
    {
        private const string InternalServiceEndpointName = "InternalService";
        private const string RoleName = "WorkerRole1";
        public static bool AmITheLeader
        {
            get
            {
                var table = GetMastersTable();
                var tableResult = GetMastersTableResult(table);

                var nodeId = GetCurrentLeaderId(tableResult);
                var leaderNode = GetRoleInstance(nodeId);
                if (leaderNode != null && IsRoleAlive(leaderNode))
                {
                    return RoleEnvironment.CurrentRoleInstance.Id == nodeId;
                }

                //Leader is dead elect a new one
                var newLeaderId = ElectTheLeader(tableResult, table);

                return RoleEnvironment.CurrentRoleInstance.Id == newLeaderId;
            }
        }

        public static RoleInstance GetRoleInstance(string nodeId)
        {
            return RoleEnvironment.Roles[RoleName].Instances.SingleOrDefault(x => x.Id == nodeId);
        }

        public static string GetCurrentLeaderId()
        {
            var table = GetMastersTable();
            var tableResult = GetMastersTableResult(table);
            return GetCurrentLeaderId(tableResult);
        }

        public static string GetCurrentLeaderId(TableResult table)
        {
            
            if (table != null)
            {
                var masterRef = table.Result as MasterRef;
                if (masterRef != null)
                {
                    return masterRef.NodeId;
                }
            }
            return null;
        }

        private static TableResult GetMastersTableResult(CloudTable table)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<MasterRef>("master", "master");

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            return retrievedResult;
        }

        private static CloudTable GetMastersTable()
        {
            var _cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var tableClient = _cloudStorageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference("masterReference");
            table.CreateIfNotExists();
            return table;
        }

        private static string ElectTheLeader(TableResult tableResult, CloudTable table)
        {
            string result = null;

            // Assign the result to a CustomerEntity object.
            MasterRef updateEntity = (MasterRef)tableResult.Result ?? new MasterRef();

                //Get an alive instance with the lowest id 
                var instance = RoleEnvironment.CurrentRoleInstance;
                var leaderNode =
                    RoleEnvironment.Roles[RoleName].Instances.Where(i => i.Id != instance.Id).OrderBy(ins => ins.Id).FirstOrDefault(IsRoleAlive);
                var leaderNodeId = leaderNode != null ? leaderNode.Id : instance.Id;
                

                updateEntity.NodeId = leaderNodeId;
                updateEntity.TimeStamp = DateTime.UtcNow.ToString();
                table.Execute(TableOperation.InsertOrMerge(updateEntity));
                result = leaderNodeId;

            return result;

        }

        private static bool IsRoleAlive(RoleInstance instance)
        {
            try
            {
                var channelFactory = new ChannelFactory<ITenantService>(new NetTcpBinding());
                var channel = channelFactory.CreateChannel(new EndpointAddress(GetLeaderInternalServiceAddress()));
                return channel.IsAlive();
            }
            catch (EndpointNotFoundException e)
            {
                return false;
            }
        }

        public static Uri GetInternalServiceUri(RoleInstance instance)
        {
            var endpoint = instance.InstanceEndpoints[InternalServiceEndpointName];
            var ub = new UriBuilder
            {
                Host = endpoint.IPEndpoint.Address.ToString(),
                Port = endpoint.IPEndpoint.Port,
                Scheme = "net.tcp",
            };

            return ub.Uri;
        }


        public static Uri GetLeaderInternalServiceAddress()
        {
            var leader = GetCurrentLeaderId();
            var leaderInstance = GetRoleInstance(leader);
            if (leaderInstance == null)
            {
                leaderInstance = RoleEnvironment.Roles[RoleName].Instances.OrderBy(i => i.Id).First();
            }
            return GetInternalServiceUri(leaderInstance);
        }
    }
}
