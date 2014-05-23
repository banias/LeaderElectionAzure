using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Globalization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;


//using smarx.WazStorageExtensions;

namespace WorkerRole1
{
    public class AutoRenewLease : IDisposable
    {
        public bool HasLease { get { return leaseId != null; } }

        private CloudBlockBlob blob;
        private string leaseId;
        private bool disposed = false;

        public static void DoOnce(CloudBlockBlob blob, Action action) { DoOnce(blob, action, TimeSpan.FromSeconds(5)); }
        public static void DoOnce(CloudBlockBlob blob, Action action, TimeSpan pollingFrequency)
        {
            // blob.Exists has the side effect of calling blob.FetchAttributes, which populates the metadata collection
            while (!blob.Exists() || blob.Metadata["progress"] != "done")
            {
                using (var arl = new AutoRenewLease(blob))
                {
                    if (arl.HasLease)
                    {
                        action();
                        blob.Metadata["progress"] = "done";
                        blob.SetMetadata(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                    }
                    else
                    {
                        Thread.Sleep(pollingFrequency);
                    }
                }
            }
        }

        public static void DoEvery(CloudBlockBlob blob, TimeSpan interval, Action action)
        {
            while (true)
            {
                var lastPerformed = DateTimeOffset.MinValue;
                using (var arl = new AutoRenewLease(blob))
                {
                    if (arl.HasLease)
                    {
                        blob.FetchAttributes();
                        DateTimeOffset.TryParseExact(blob.Metadata["lastPerformed"], "R", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out lastPerformed);
                        if (DateTimeOffset.UtcNow >= lastPerformed + interval)
                        {
                            action();
                            lastPerformed = DateTimeOffset.UtcNow;
                            blob.Metadata["lastPerformed"] = lastPerformed.ToString("R");
                            blob.SetMetadata(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                        }
                    }
                }
                var timeLeft = (lastPerformed + interval) - DateTimeOffset.UtcNow;
                var minimum = TimeSpan.FromSeconds(5); // so we're not polling the leased blob too fast
                Thread.Sleep(
                    timeLeft > minimum
                    ? timeLeft
                    : minimum);
            }
        }

        public AutoRenewLease(CloudBlockBlob blob)
        {
            this.blob = blob;

            if(blob.Exists() == false)
                blob.UploadFromStream(new MemoryStream(new byte[0]), AccessCondition.GenerateIfNoneMatchCondition("*"));

            leaseId = blob.TryAcquireLease();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if(leaseId != null)
                        blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
                }
                disposed = true;
            }
        }

        ~AutoRenewLease()
        {
            Dispose(false);
        }
    }
}