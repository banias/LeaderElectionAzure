using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace WorkerRole1
{
    public class MasterRef : TableEntity
    {
        public MasterRef(string id, string nodeId, string timeStamp)
        {
            this.PartitionKey = id;
            this.RowKey = id;
        }

        public MasterRef()
        {
            PartitionKey = "master";
            RowKey = "master";
        }

        public string NodeId { get; set; }

        public string TimeStamp { get; set; }
    }
}
