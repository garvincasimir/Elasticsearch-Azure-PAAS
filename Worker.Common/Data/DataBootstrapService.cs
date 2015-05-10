using ElasticsearchWorker.ClusterApi;
using ElasticsearchWorker.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Net;

namespace ElasticsearchWorker.Data
{
    public class DataBootstrapService
    {
        public const string INDEX_DATA_SOURCE = "indexsource";
        public const string DATA_BOOTSTRAP = "databootstrap";
        protected IElasticsearchServiceSettings _Settings;
        protected List<IDataBootstrapper> _Bootstrappers = new List<IDataBootstrapper>();

        //TODO: Make this configurable
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

        //This runs in the threadpool
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
                
                //Start downloading data. Schedule based on available resources
                //Most people will probably have one of these but we will allow as many as your resources can handle.
                //Be conservative as this shares resources with your cluster. Streaming is your friend. Don't hog memory or cpu.
                Parallel.ForEach(_Bootstrappers, source =>
                {
                    var masterState = _client.IsMaster(_Settings.NodeName);

                    if (!masterState.IsError && masterState.Result == true) 
                    {
                        var state = _client.GetItem<IndexDataSource>(DATA_BOOTSTRAP, INDEX_DATA_SOURCE, source.Name);

                        //No item by this name exists or time to refresh data 
                        if (state.StatusCode.GetValueOrDefault() == (int)HttpStatusCode.NotFound || (!state.IsError && DateTime.UtcNow.CompareTo(state.Result.NextUpdate) >0))
                        {
                            //TODO: Come up with a good failure strategy
                            //Updating the indexes should be Idempotent 
                            //We can't guarantee this code will be run by a single node. 
                            //However, if cluster remains healthy, isMaster should be sufficient to mitigate unnecessary updates
                            source.Run(_Settings, (nextUpdateDate,errorMessage) =>
                            {
                                //This should not throw
                                var result = _client.AddOrUpdate<IndexDataSource>(DATA_BOOTSTRAP, INDEX_DATA_SOURCE, new IndexDataSource
                                {
                                    Name = source.Name,
                                    NextUpdate = nextUpdateDate, //Let the bootstrapper say when next it wants to run
                                    LastErrorDate = string.IsNullOrEmpty(errorMessage)? ((Nullable<DateTime>)null) : DateTime.UtcNow,
                                    LastErrorMessage = errorMessage,
                                    LastUpdated = string.IsNullOrEmpty(errorMessage) ? DateTime.UtcNow : ((Nullable<DateTime>)null), 
                                });

                                //Sorry could not save our update.
                                //Could mean that the cluster is not responding.
                                //We should probably save new state to a stack and try again later.
                                //Or is it better to log and fail quickly
                                //Nullable<int> _version ? Not sure if that will work for concurrency since stack items could have along life.
                                if (result.IsError)
                                {
                                    Trace.TraceError(result.ErrorMessage);
                                }
                            });
                        }
                        
                    }
                    
                });

                _InitTimer.Start();
            }

      
        }

        public DataBootstrapService Add(IDataBootstrapper bootstrapper)
        {
            _Bootstrappers.Add(bootstrapper);
            return this;
        }


    }
}
