using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
    public class StorageArtifact : WebArtifact
    {
        protected CloudStorageAccount Account;
        public StorageArtifact(string sourceURL,string name, CloudStorageAccount account)
            :base(sourceURL,name)
        {
            Account = account;
        }

        public override void Download(string filePath, bool useTemp = true)
        {
            var client = Account.CreateCloudBlobClient();

            string downloadDestination = useTemp ? Path.GetTempFileName() : filePath;

            var blob = client.GetBlobReferenceFromServer(new Uri(_SourceURL));
            blob.DownloadToFile(downloadDestination,FileMode.OpenOrCreate);

            if (useTemp)
            {
                File.Copy(downloadDestination, filePath,true);
            }

            Trace.TraceInformation("{0} download complete", filePath);
        }
    }
}
