using System.Diagnostics;
using System.Net;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using ElasticsearchWorker.Core;

namespace ElasticsearchRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudStorageAccount storage = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnection"));
        private ElasticsearchService service;
        public override void Run()
        {
            service.RunAndBlock();
        }

        public override bool OnStart()
        {
            // Not sure what this should be. Hopefully storage over smb doesn't open a million connections
            ServicePointManager.DefaultConnectionLimit = 12;

   
            var settings = ElasticsearchServiceSettings.FromStorage(storage);
            service = ElasticsearchService.FromSettings(settings);
            bool result = base.OnStart();

            Trace.TraceInformation("ElasticsearchRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("ElasticsearchRole is stopping");

            service.OnStop();


            base.OnStop();

            Trace.TraceInformation("ElasticsearchRole has stopped");
        }


    }
}
