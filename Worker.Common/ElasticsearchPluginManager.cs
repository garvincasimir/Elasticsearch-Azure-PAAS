using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace Worker.Common
{
    public class ElasticsearchPluginManager
    {
        private readonly CloudStorageAccount account;
        private string tempPath;
        private string filePath;
        private string pluginDownloadFolder;
        private string elasticsearchPluginRoot;

        public ElasticsearchPluginManager(CloudStorageAccount account, string filePath, string tempPath, string pluginDownloadFolder, string elasticsearchRoot)         
        {
            this.account = account;
            this.tempPath = tempPath;
            this.filePath = filePath;
            this.pluginDownloadFolder = pluginDownloadFolder;
            elasticsearchPluginRoot = Path.Combine(elasticsearchRoot, "plugins");

            this.tempPath = Path.Combine(this.tempPath, pluginDownloadFolder);
            this.filePath = Path.Combine(this.filePath, pluginDownloadFolder);

            if (!Directory.Exists(this.tempPath))
                Directory.CreateDirectory(this.tempPath);

            if (!Directory.Exists(this.filePath))
                Directory.CreateDirectory(this.filePath);
        }

        public Task EnsureConfigured()
        {
            return Task.Factory.StartNew(() => DownloadIfNotExists());
        }

        //Copies and extracts all plugins to elastic search plugin folder
        public void CopyAndExtractPluginsToElasticFolder()
        {
            foreach (var file in Directory.GetFiles(filePath))
            {
                var destFile = Path.Combine(elasticsearchPluginRoot, Path.GetFileName(file));
                File.Copy(file, destFile);
                ZipFile.ExtractToDirectory(file, elasticsearchPluginRoot);
                File.Delete(destFile);
            }
            Trace.TraceInformation("Elasticsearch plugins copied and extracted");
        }

        //Downloads all plugins from storage
        private void DownloadIfNotExists(bool useTemp = true)
        {
            var client = account.CreateCloudBlobClient();

            var pluginContainer = client.GetContainerReference(pluginDownloadFolder);
            var plugins = pluginContainer.ListBlobs().ToList();
      
            foreach (var plugin in plugins)
            {
                var blob = client.GetBlobReferenceFromServer(plugin.Uri);
                if (!File.Exists(Path.Combine(filePath, blob.Name)))
                {
                    string downloadDestination = useTemp
                        ? Path.Combine(tempPath, Guid.NewGuid().ToString())
                        : Path.Combine(filePath, blob.Name);

                    blob.DownloadToFile(downloadDestination, FileMode.OpenOrCreate);

                    if (useTemp)
                    {
                        File.Copy(downloadDestination, Path.Combine(filePath, blob.Name), true);
                    }
                }
            }
           
            Trace.TraceInformation("Elasticsearch plugins download complete");
        }
    }
}
