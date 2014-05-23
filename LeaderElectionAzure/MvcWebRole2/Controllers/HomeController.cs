using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.ServiceRuntime;
using WorkerRole1;

namespace MvcWebRole2.Controllers
{

    public class InstanceModel
    {
        public string Id { get; set; }
        public bool IsMaster { get; set; }
        public Dictionary<string, TenantStatus> Tenants { get; set; }
    }
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Modify this template to jump-start your ASP.NET MVC application.";
            var workers = RoleEnvironment.Roles["WorkerRole1"].Instances;
            ViewBag.Workers = new List<InstanceModel>();
            var leaderId = AzureLeaderElectionProvider.GetCurrentLeaderId();
            foreach (var worker in workers)
            {
                var workerState = GetWorkerState(worker);
                if(worker.Id == leaderId)
                    ViewBag.Workers.Add(new InstanceModel{Id = worker.Id, IsMaster = true, Tenants = workerState.Tenants});
                else
                {
                    ViewBag.Workers.Add(new InstanceModel{Id = worker.Id, IsMaster = false, Tenants = workerState.Tenants});                    
                }
            }
            return View();
        }

        private RoleStatus GetWorkerState(RoleInstance worker)
        {
            try
            {
                var address = AzureLeaderElectionProvider.GetInternalServiceUri(worker);

                var channelFactory = new ChannelFactory<ITenantService>(new NetTcpBinding());
                var channel = channelFactory.CreateChannel(new EndpointAddress(address));
                return channel.GetRoleStatus();
            }
            catch
            {
                return new RoleStatus() { Tenants = new Dictionary<string, TenantStatus>()};
            }
        }

        public ActionResult Kill(string instanceId)
        {
            var instance = AzureLeaderElectionProvider.GetRoleInstance(instanceId);
            var address = AzureLeaderElectionProvider.GetInternalServiceUri(instance);

            var channelFactory = new ChannelFactory<ITenantService>(new NetTcpBinding());
            var channel = channelFactory.CreateChannel(new EndpointAddress(address));
            channel.KillRole();

            return RedirectToAction("Index");
        }

        public ActionResult KillTenant(string instanceId, string tenantId)
        {
            var instance = AzureLeaderElectionProvider.GetRoleInstance(instanceId);
            var address = AzureLeaderElectionProvider.GetInternalServiceUri(instance);

            var channelFactory = new ChannelFactory<ITenantService>(new NetTcpBinding());
            var channel = channelFactory.CreateChannel(new EndpointAddress(address));
            channel.KillTenant(tenantId);

            return RedirectToAction("Index");
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your app description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
