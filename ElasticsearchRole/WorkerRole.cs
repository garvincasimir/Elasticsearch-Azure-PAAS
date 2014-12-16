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
                Trace.TraceInformation("Attempting to configure node: {0}", nodeName);
                Task.WaitAll(configTasks, cancellationTokenSource.Token);

                //Start discovery helper (non blocking)
                bridge.StartService();

                var javaHome = javaManager.GetJavaHomeFromReg();
                Trace.TraceInformation("Attempting to start elasticsearch as node: {0} with JAVA_HOME =  ", nodeName, javaHome);
                elasticsearchManager.StartAndBlock(cancellationTokenSource.Token, javaHome);
            }
            catch(AggregateException ae)
            {
                foreach(var ex in ae.InnerExceptions)
                {
                    Trace.TraceError(ex.Message + " : " + ex.StackTrace);
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message + " : " + e.StackTrace);
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
            string UseElasticLocalDataFolder = CloudConfigurationManager.GetSetting("UseElasticLocalDataFolder");
            string javaInstaller = CloudConfigurationManager.GetSetting("JavaInstallerName");
            string javaDownloadURL = CloudConfigurationManager.GetSetting("JavaDownloadURL");
            string elasticsearchZip = CloudConfigurationManager.GetSetting("ElasticsearchZip");
            string elasticsearchDownloadURL = CloudConfigurationManager.GetSetting("ElasticsearchDownloadURL");
            string shareName = CloudConfigurationManager.GetSetting("ShareName"); //root path for es data
            string shareDrive = CloudConfigurationManager.GetSetting("ShareDrive"); //Drive letter to map azure share
            string endpointName = CloudConfigurationManager.GetSetting("EndpointName");
            string archiveRoot = RoleEnvironment.GetLocalResource("ArchiveRoot").RootPath;
            string logRoot = RoleEnvironment.GetLocalResource("LogRoot").RootPath;
            string elasticDataRoot = RoleEnvironment.GetLocalResource("ElasticDataRoot").RootPath;
            string elasticRoot = RoleEnvironment.GetLocalResource("ElasticRoot").RootPath;
            string emulatorDataRoot = RoleEnvironment.GetLocalResource("EmulatorDataRoot").RootPath; // we need this cause we can't emulate shares
            string roleRoot = Environment.GetEnvironmentVariable("ROLEROOT");
            string tempPath = RoleEnvironment.GetLocalResource("CustomTempRoot").RootPath; //Standard temp folder is too small
            
            /**
             * Issue #1.  In azure the role root is just a drive letter. Unfortunately, System.IO doesn't add needed slash 
             *  so Path.Combine("E:","path\to\file") yields E:path\to\file
             *  Still hoping there is a .net api that I an use to avoid the code below.
             */
            if (!roleRoot.EndsWith(@"\"))
            {
                roleRoot +=   @"\";
            }
            
            #endregion

            #region Configure Java  manager
            //Are we downloading java from storage or regular url?
            string javaDownloadType = CloudConfigurationManager.GetSetting("JavaDownloadType");
            WebArtifact javaArtifact;

            if (!string.IsNullOrWhiteSpace(javaDownloadType) && javaDownloadType == "storage")
            {
                javaArtifact = new StorageArtifact(javaDownloadURL, javaInstaller, tempPath,storage);
            }
            else
            {
                javaArtifact = new WebArtifact(javaDownloadURL, javaInstaller,tempPath);
            }

            javaManager = new JavaManager(javaArtifact, archiveRoot, logRoot); //Java installer
            
            #endregion

            #region Configure Elasticsearch manager
            //Are we downloading elasticsearch from storage or regular url?
            string elasticsearchDownloadType = CloudConfigurationManager.GetSetting("ElasticsearchDownloadType");
            WebArtifact elasticsearchArtifact;

            if (!string.IsNullOrWhiteSpace(elasticsearchDownloadType) && elasticsearchDownloadType == "storage")
            {
                elasticsearchArtifact = new StorageArtifact(elasticsearchDownloadURL, elasticsearchZip, tempPath ,storage);
            }
            else
            {
                elasticsearchArtifact = new WebArtifact(elasticsearchDownloadURL, elasticsearchZip, tempPath);  
            }

            bridge = new PipesRuntimeBridge(endpointName); //Discovery helper

            if (UseElasticLocalDataFolder.ToLower() != "true")
            {  
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
            }
            else
            {
                shareDrive = elasticDataRoot;
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

            elasticsearchManager = new ElasticsearchManager(runtimeConfig, elasticsearchArtifact, archiveRoot, elasticRoot, logRoot);
            #endregion

            bool result = base.OnStart();

            Trace.TraceInformation("ElasticsearchRole has been started");

            return result;
        }

        public override void OnStop()
        {
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
