using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Worker.Common;
using RedDog.Storage.Files;
using System.IO;

namespace ElasticsearchRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudStorageAccount storage = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnection"));
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private ElasticsearchManager elasticsearchManager;
        private JavaManager javaManager;
        private string nodeName;
        private PipesRuntimeBridge bridge;
       
        public override void Run()
        {
            try
            {
                var configTasks = new Task[] { javaManager.EnsureConfigured(), elasticsearchManager.EnsureConfigured() };
                Trace.TraceInformation("Attempting to configure node: ", nodeName);
                Task.WaitAll(configTasks, cancellationTokenSource.Token);

                Trace.TraceInformation("Attempting to start elasticsearch as node: ", nodeName);
                elasticsearchManager.StartAndBlock();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Not sure what this should be. Hopefully storage over smb doesn't open a million connections
            ServicePointManager.DefaultConnectionLimit = 12;

            nodeName = RoleEnvironment.CurrentRoleInstance.Id;

            string javaInstaller = CloudConfigurationManager.GetSetting("JavaInstallerName");
            string javaDownloadURL = CloudConfigurationManager.GetSetting("JavaDownloadURL");
            string elasticsearchZip = CloudConfigurationManager.GetSetting("ElasticsearchZip");
            string elasticsearchDownloadURL = CloudConfigurationManager.GetSetting("ElasticsearchDownloadURL");
            string shareName =  CloudConfigurationManager.GetSetting("ShareName"); //root path for es data
            string shareDrive =  CloudConfigurationManager.GetSetting("ShareDrive"); //Drive letter to map azure share
            string endpointName =  CloudConfigurationManager.GetSetting("EndpointName"); 
            string archiveRoot = RoleEnvironment.GetLocalResource("ArchiveRoot").RootPath;
            string logRoot =  RoleEnvironment.GetLocalResource("LogRoot").RootPath;
            string elasticRoot = RoleEnvironment.GetLocalResource("ElasticRoot").RootPath;
            string emulatorDataRoot = RoleEnvironment.GetLocalResource("EmulatorDataRoot").RootPath; // we need this cause we can't emulate shares
            string roleRoot = Environment.GetEnvironmentVariable("ROLEROOT");
            string tempPath = Path.GetTempPath();


            javaManager = new JavaManager(javaInstaller, javaDownloadURL, archiveRoot, logRoot); //Java installer
            bridge = new PipesRuntimeBridge(endpointName); //Discovery helper

            //Azure file is not available in emulator
            if (!RoleEnvironment.IsEmulated)
            {
                // Mount a drive for a CloudFileShare.
                Trace.WriteLine("Configuring file Share");
                var share = storage.CreateCloudFileClient()
                                        .GetShareReference(shareName);
                share.CreateIfNotExists();

                Trace.WriteLine("Mapping Share to " + shareDrive);
                share.Mount(shareDrive);
            }
            else
            {
                shareDrive = emulatorDataRoot;
            }

            var runtimeConfig = new ElasticsearchRuntimeConfig
            {
                DataPath= shareDrive,
                LogPath = logRoot,
                TempPath = tempPath,
                NodeName = nodeName,
                BridgePipeName = bridge.PipeName,
                PackagePluginPath = Path.Combine(roleRoot,"approot","plugins"),
                TemplateConfigFile = Path.Combine(roleRoot,"approot","config",ElasticsearchManager.ELASTICSEARCH_CONFIG_FILE),
                TemplateLogConfigFile = Path.Combine(roleRoot,"approot","config",ElasticsearchManager.ELASTICSEARCH_LOG_CONFIG_FILE)
                
            };

            elasticsearchManager = new ElasticsearchManager(runtimeConfig, elasticsearchZip, elasticsearchDownloadURL, archiveRoot, elasticRoot, logRoot);

            bool result = base.OnStart();

            Trace.TraceInformation("ElasticsearchRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("ElasticsearchRole is stopping");

            cancellationTokenSource.Cancel();
            
            elasticsearchManager.Stop();

            runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("ElasticsearchRole has stopped");
        }


    }
}
