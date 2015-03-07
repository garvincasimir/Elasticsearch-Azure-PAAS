using ElasticsearchWorker.ClusterApi;
using ElasticsearchWorker.Core;
using System;
using System.Threading.Tasks;

namespace ElasticsearchWorker.Data
{
    public interface IDataBootstrapper
    {
        string Name { get; }
        Task Run(IElasticsearchServiceSettings settings, Func<ResultWrapper<Boolean>> isMaster, Func<ResultWrapper<Boolean>> isHealthy);
    }
}
