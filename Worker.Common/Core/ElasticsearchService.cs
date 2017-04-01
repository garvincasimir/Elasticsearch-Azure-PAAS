using ElasticsearchWorker.Data;
using RedDog.Storage.Files;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ElasticsearchWorker.Core
{
    /// <summary>
    /// Serves as a wrapper for logic in the role entry point
    /// </summary>
    public class ElasticsearchService
    {
        protected readonly CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();
        protected readonly ManualResetEvent _RunCompleteEvent = new ManualResetEvent(false);
        protected ElasticsearchManager _ElasticsearchManager;
        protected JavaManager _JavaManager;
        protected IElasticsearchServiceSettings _Settings;
        protected DataBootstrapService _BootstrapService;

        private ElasticsearchService(){}
       
        public static ElasticsearchService FromSettings(IElasticsearchServiceSettings settings)
        {
            var service = new ElasticsearchService()
            {
                _Settings = settings,
                _JavaManager = new JavaManager(settings),
                _BootstrapService = new DataBootstrapService(settings)
            };

            string dataPath;
            //Use local storage for emulator and
            if (!settings.IsEmulated && settings.UseElasticLocalDataFolder.ToLower() != "true")
            {
                // Mount a drive for a CloudFileShare.
                Trace.WriteLine("Configuring file Share");
                var share = settings.StorageAccount.CreateCloudFileClient()
                    .GetShareReference(settings.DataShareName);
                share.CreateIfNotExists();

                Trace.WriteLine("Mapping Share to " + settings.DataShareDrive);
                share.Mount(settings.DataShareDrive);
                dataPath = settings.DataShareDrive;

            }
            else
            {
                dataPath = settings.DataDirectory;
            }

            service._ElasticsearchManager = new ElasticsearchManager(settings, dataPath);
             
            if (!string.IsNullOrWhiteSpace(settings.ElasticsearchPluginContainer))
            {
                service._ElasticsearchManager.AddPluginSource(settings.ElasticsearchPluginContainer, settings.StorageAccount);
            }
           

            return service;
        }

        public void RunAndBlock()
        {
            try
            {
                

                var configTasks = new Task[] 
                { 
                    _JavaManager.EnsureConfigured(), 
                    _ElasticsearchManager.EnsureConfigured()
                };

                Trace.TraceInformation("Attempting to configure node: {0}", _Settings.NodeName);
                Task.WaitAll(configTasks, _CancellationTokenSource.Token);

                //Java installed get java home path
                var javaHome = _JavaManager.GetJavaHomeFromReg();
               
                //Install Named plugins (Not Recommended: External dependency)
                _ElasticsearchManager.InstallNamedPlugins(_CancellationTokenSource.Token,javaHome);

                //Bootstrap data if configured (non blocking)
                _BootstrapService.StartService();
                
                Trace.TraceInformation("Attempting to start elasticsearch as node: {0} with JAVA_HOME = {1}", _Settings.NodeName, javaHome);
                _ElasticsearchManager.StartAndBlock(_CancellationTokenSource.Token, javaHome);
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
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
                this._RunCompleteEvent.Set();
            }
        }

        public void OnStop()
        {
            _CancellationTokenSource.Cancel();

            try
            {
               _ElasticsearchManager.Stop();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }

            _RunCompleteEvent.WaitOne();
        }

        public void AddBootstrapper(IDataBootstrapper bootstraper)
        {
            _BootstrapService.Add(bootstraper);
        }
 
    }
}
