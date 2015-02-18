using Microsoft.WindowsAzure.Storage;
using System;
using System.Diagnostics;
using System.IO;

namespace ElasticsearchWorker
{
    public class StorageArtifact : WebArtifact
    {
        protected CloudStorageAccount Account;
        public StorageArtifact(string sourceURL,string name,CloudStorageAccount account)
            :base(sourceURL,name)
        {
            Account = account;
        }

        public override void DownloadTo(string filePath)
        {
            var client = Account.CreateCloudBlobClient();
            var directory = Path.GetDirectoryName(filePath);
            var temFileName = Guid.NewGuid().ToString();

            string downloadDestination = Path.Combine(directory, temFileName);

            var blob = client.GetBlobReferenceFromServer(new Uri(_SourceURL));
            blob.DownloadToFile(downloadDestination,FileMode.OpenOrCreate);

            File.Move(downloadDestination, filePath);
            
            Trace.TraceInformation("{0} download complete", filePath);
        }
    }
}
