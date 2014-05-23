using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Net;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace WorkerRole1
{
    public static class LeaseBlobExtensions
    {
       static string newLeaseId = Guid.NewGuid().ToString(); 
        public static string TryAcquireLease(this CloudBlockBlob blob)
        {
            try { return blob.AcquireLease(TimeSpan.FromMinutes(1), newLeaseId); }
            catch (StorageException e)
            {
                if ( e.RequestInformation.HttpStatusCode != 409) // 409, already leased
                {
                    throw;
                }
                
                return null;
            }
        }



        public static void ReleaseLease(this CloudBlockBlob blob, string leaseId)
        {
            blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
        }

        public static bool TryRenewLease(this CloudBlockBlob blob, string leaseId)
        {
            try { blob.RenewLease(AccessCondition.GenerateLeaseCondition(leaseId)); return true; }
            catch { return false; }
        }


    }
}