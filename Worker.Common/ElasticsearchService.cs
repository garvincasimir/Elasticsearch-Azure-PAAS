using Microsoft.WindowsAzure.ServiceRuntime;
using RedDog.Storage.Files;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Worker.Common
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
        protected PipesRuntimeBridge _Bridge;
        protected IElasticsearchServiceSettings _Settings;

        private ElasticsearchService(){}
       
        public static ElasticsearchService FromSettings(IElasticsearchServiceSettings settings)
        {
            var service = new ElasticsearchService()
            {
                _Settings = settings,
                _Bridge = new PipesRuntimeBridge(settings.EndpointName),
                _JavaManager = new JavaManager(settings)
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
                dataPath = settings.UseElasticLocalDataFolder;
            }

            service._ElasticsearchManager = new ElasticsearchManager(settings, dataPath, service._Bridge.PipeName);
             
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

                //Start discovery helper (non blocking)
                _Bridge.StartService();

                var javaHome = _JavaManager.GetJavaHomeFromReg();
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
 
    }
}
