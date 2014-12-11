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

        private volatile bool onStopCalled = false;
        public override void Run()
        {
            try
            {
                if (onStopCalled)
                {
                    Trace.TraceInformation("OnStop has been called, returning from Run(): {0}", nodeName);
                    return;
                }

                var configTaskjava = new Task[]{ javaManager.EnsureConfigured()};
                Trace.TraceInformation("Attempting to configure node with java: {0}", nodeName);
                Task.WaitAll(configTaskjava, cancellationTokenSource.Token);

                
                var configTaskElastic = new Task[] { elasticsearchManager.EnsureConfigured() };
                Trace.TraceInformation("Attempting to configure node with Elastic: {0}", nodeName);
                Task.WaitAll(configTaskElastic, cancellationTokenSource.Token);

                //Start discovery helper (non blocking)
                bridge.StartService();

                Trace.TraceInformation("Attempting to start elasticsearch as node: {0} ", nodeName);
                elasticsearchManager.StartAndBlock(cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message + " " + e.InnerException);
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

            #region Load Config Settings
            nodeName = RoleEnvironment.CurrentRoleInstance.Id;
            string javaInstaller = CloudConfigurationManager.GetSetting("JavaInstallerName");
            string javaDownloadURL = CloudConfigurationManager.GetSetting("JavaDownloadURL");
            string elasticsearchZip = CloudConfigurationManager.GetSetting("ElasticsearchZip");
            string elasticsearchDownloadURL = CloudConfigurationManager.GetSetting("ElasticsearchDownloadURL");
            string shareName = CloudConfigurationManager.GetSetting("ShareName"); //root path for es data
            string shareDrive = CloudConfigurationManager.GetSetting("ShareDrive"); //Drive letter to map azure share
            string endpointName = CloudConfigurationManager.GetSetting("EndpointName");
            string archiveRoot = RoleEnvironment.GetLocalResource("ArchiveRoot").RootPath;
            string logRoot = RoleEnvironment.GetLocalResource("LogRoot").RootPath;
            string elasticRoot = RoleEnvironment.GetLocalResource("ElasticRoot").RootPath;
            string emulatorDataRoot = RoleEnvironment.GetLocalResource("EmulatorDataRoot").RootPath; // we need this cause we can't emulate shares
            string roleRoot = Environment.GetEnvironmentVariable("ROLEROOT");
            
            /**
             * Issue #1.  In azure the role root is just a drive letter. Unfortunately, System.IO doesn't add needed slash 
             *  so Path.Combine("E:","path\to\file") yields E:path\to\file
             *  Still hoping there is a .net api that I an use to avoid the code below.
             */
            if (!roleRoot.EndsWith(@"\"))
            {
                roleRoot +=   @"\";
            }
            string tempPath = Path.GetTempPath(); 
            #endregion

            #region Configure Java  manager
            //Are we downloading java from storage or regular url?
            string javaDownloadType = CloudConfigurationManager.GetSetting("JavaDownloadType");
            WebArtifact javaArtifact;

            if (!string.IsNullOrWhiteSpace(javaDownloadType) && javaDownloadType == "storage")
            {
                javaArtifact = new StorageArtifact(javaDownloadURL, javaInstaller, storage);
            }
            else
            {
                javaArtifact = new WebArtifact(javaDownloadURL, javaInstaller);
            }

            javaManager = new JavaManager(javaArtifact, archiveRoot, logRoot); //Java installer
            
            #endregion

            #region Configure Elasticsearch manager
            //Are we downloading elasticsearch from storage or regular url?
            string elasticsearchDownloadType = CloudConfigurationManager.GetSetting("ElasticsearchDownloadType");
            WebArtifact elasticsearchArtifact;

            if (!string.IsNullOrWhiteSpace(elasticsearchDownloadType) && elasticsearchDownloadType == "storage")
            {
                elasticsearchArtifact = new StorageArtifact(elasticsearchDownloadURL, elasticsearchZip, storage);
            }
            else
            {
                elasticsearchArtifact = new WebArtifact(elasticsearchDownloadURL, elasticsearchZip);  
            }

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

             if (!shareDrive.EndsWith(@"\"))
            {
                shareDrive +=   @"\";
            }

            var runtimeConfig = new ElasticsearchRuntimeConfig
            {
                DataPath=  Path.Combine(shareDrive, nodeName, "data"),
                LogPath = Path.Combine(shareDrive, nodeName, "log"),
                TempPath = tempPath,
                NodeName = nodeName,
                BridgePipeName = bridge.PipeName,
                PackagePluginPath = Path.Combine(roleRoot,"approot","plugins"),
                TemplateConfigFile = Path.Combine(roleRoot,"approot","config",ElasticsearchManager.ELASTICSEARCH_CONFIG_FILE),
                TemplateLogConfigFile = Path.Combine(roleRoot,"approot","config",ElasticsearchManager.ELASTICSEARCH_LOG_CONFIG_FILE)
                
            };     

            elasticsearchManager = new ElasticsearchManager(runtimeConfig, elasticsearchArtifact, archiveRoot, elasticRoot, logRoot);
            #endregion

            bool result = base.OnStart();

            Trace.TraceInformation("ElasticsearchRole has been started");

            return result;
        }

        public override void OnStop()
        {
            onStopCalled = true;

            Trace.TraceInformation("ElasticsearchRole is stopping");
            
            cancellationTokenSource.Cancel();

            try
            {
                elasticsearchManager.Stop();
            }
            catch(Exception e)
            {
                Trace.TraceError(e.Message);
            }

            runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("ElasticsearchRole has stopped");
        }


    }
}
