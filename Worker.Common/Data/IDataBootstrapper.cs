using ElasticsearchWorker.ClusterApi;
using ElasticsearchWorker.Core;
using System;
using System.Threading.Tasks;

namespace ElasticsearchWorker.Data
{
    public interface IDataBootstrapper
    {
        string Name { get; }
        void Run(IElasticsearchServiceSettings settings, Action<DateTime,string> onComplete);
    }
}
