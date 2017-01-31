using System;
using System.IO;
using System.Reflection;
using log4net;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace BatchLongRunningTest
{
    public static class AzureStorageHelper
    {
        static readonly ILog _logger = LogManager.GetLogger(typeof(AzureStorageHelper));

        public static bool FileExists(string blobName)
        {
            return GetBlob(blobName).Exists();
        }

        public static void Upload(string blobName, string location)
        {
            _logger.Debug($"uploading {blobName} from location {location}");

            var blob = GetBlob(blobName);
            using (var stream = File.OpenRead(location))
            {
                blob.UploadFromStream(stream);
            }
        }

        public static void CopyFile(string blobName, string destinationName, int numberOfTimes)
        {
            var source = GetBlobSasUri(blobName);

            for (int i = 0; i <= numberOfTimes; i++)
            {
                var newBlob = GetBlob($"{destinationName}_{i}.exe");
                newBlob.StartCopy(source);
            }
        }

        public static Uri GetBlobSasUri(string blobName)
        {
            var blob = GetBlob(blobName);
            var sasToken = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddMonths(1),
                Permissions = SharedAccessBlobPermissions.Read
            });

            return new Uri(blob.Uri + sasToken);
        }

        static CloudBlockBlob GetBlob(string blobName)
        {
            _logger.Debug($"Getting blob {blobName} from container batchlongrunningtests");
            var blobClient = GetBlobClient();
            var container = blobClient.GetContainerReference("batchlongrunningtests");
            container.CreateIfNotExists();
            return container.GetBlockBlobReference(blobName);
        }

        static CloudBlobClient GetBlobClient()
        {
            return
                CloudStorageAccount.Parse(
                    "DefaultEndpointsProtocol=https;AccountName=icic4e;AccountKey=KRE4XuEvMXcxlnrSn2bU7jlfcqvVSBH8PeZ19dXdyRaATCc6RBBLdzNoFDT628gGCa/8zciV7Bjcc/VEZW1ZsA==")
                    .CreateCloudBlobClient();
        }
    }
}
