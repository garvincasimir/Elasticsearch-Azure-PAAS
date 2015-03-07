using Elasticsearch.Net;
using ElasticsearchWorker.IndexResponse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ElasticsearchWorker.ClusterApi
{
    public class ClusterClient
    {
        protected ElasticsearchClient _clusterClient = new ElasticsearchClient();
        public ResultWrapper<bool> IsMaster(string nodeName)
        {
            var response = _clusterClient.ClusterState<MasterNode>("master_node");
            var result = Request<bool,MasterNode>(response,(r) =>  r.Success && r.Response.master_node == nodeName);

            return result;
        }

        public ResultWrapper<bool> IsClusterHealthy()
        {
            var response = _clusterClient.ClusterHealth<ClusterHealth>();
            var result = Request<bool, ClusterHealth>(response,(r) => response.Success && response.Response.status == "green");

            return result;
        }

        public ResultWrapper<T> Request<T, K>(ElasticsearchResponse<K> response, Func<ElasticsearchResponse<K>, T> setResult)
        {
            var result = new ResultWrapper<T>();
   
            result.IsError = response.HttpStatusCode != (int)HttpStatusCode.OK;
            if (response.ServerError != null)
            {
                result.ErrorMessage = response.ServerError.Error;
            }

            result.Result = setResult(response);

            return result;

        }
    }
}
