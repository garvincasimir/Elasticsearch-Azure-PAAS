using ElasticsearchWorker.ClusterApi;
using ElasticsearchWorker.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace ElasticsearchWorker.Data
{
    public class DataBootstrapService
    {
        protected IElasticsearchServiceSettings _Settings;
        protected List<IDataBootstrapper> _Bootstrappers = new List<IDataBootstrapper>();
        protected Timer _InitTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        protected ClusterClient _client = new ClusterClient();

        public DataBootstrapService(IElasticsearchServiceSettings settings)
        {
            _Settings = settings;
            _InitTimer.Elapsed += _InitTimer_Elapsed;
        }

        public void StartService()
        {
            //Global switch for bootstraping data
            //Each boostraper can acces its own settings using GetExtra();
            if(_Settings.EnableDataBootstrap)
            {
                _InitTimer.Start();
            }
        }

        protected void _InitTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _InitTimer.Stop();

            var respose = _client.IsClusterHealthy();

            if (respose.IsError)
            {
                Trace.TraceError(respose.ErrorMessage);
                _InitTimer.Start();//Try again
            }
            else
            {   
                //We don't care about the order of execution
                foreach (var source in _Bootstrappers.AsParallel())
                {
                    //The run method should not block. Offload to separate thread
                    source.Run(_Settings, () => _client.IsMaster(_Settings.NodeName), () => _client.IsClusterHealthy() );
                }
            }

      
        }

        public DataBootstrapService Add(IDataBootstrapper bootstrapper)
        {
            _Bootstrappers.Add(bootstrapper);
            return this;
        }


    }
}
