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
        protected ElasticLowLevelClient _clusterClient;

        public ClusterClient()
        {
            var settings = new ConnectionConfiguration();

            settings.ThrowExceptions(false);
         
            settings.RequestTimeout(TimeSpan.FromMinutes(30));
           

            _clusterClient = new ElasticLowLevelClient(settings);
            
        }
        public ResultWrapper<bool> IsMaster(string nodeName)
        {
            var response = _clusterClient.ClusterState<MasterNode>("master_node,nodes");

            var result = Request<bool,MasterNode>(response,(r) =>  r.Success && r.Body.nodes[r.Body.master_node].name == nodeName);

            return result;
        }

        public ResultWrapper<bool> IsClusterHealthy()
        {
            try
            {
                var response = _clusterClient.ClusterHealth<ClusterHealth>();
                var result = Request<bool, ClusterHealth>(response, (r) => response.Success && response.Body.status == "green");
                return result;
            }
            catch(WebException e)
            {
                return new ResultWrapper<bool>()
                {
                    ErrorMessage = e.Message,
                    StatusCode = (int)e.Status,
                    IsError = true
                    
                };
            }
        }

        public ResultWrapper<T> BulkAddOrUpdate<T>(string index, string type, IEnumerable<T> body, Func<T, string> getId) where T : class
        {
            var dynamicBody = new List<object>();

            foreach (var item in body)
            {
                var id = getId(item);
                dynamicBody.Add(new { 
                    index = new { 
                        _id = id 
                    } 
                }); 
                dynamicBody.Add(item);
            }



            var response = _clusterClient.BulkPut<T>(index, type, new PostData<object>(dynamicBody));

            return new ResultWrapper<T>
            {
                ErrorMessage = response.ServerError.Error.Reason == null ? null : response.ServerError.Error.Reason,
                IsError =  response.HttpStatusCode != (int)HttpStatusCode.OK,
                StatusCode = response.HttpStatusCode
            };
        } 
        public ResultWrapper<T> AddOrUpdate<T>(string index, string type, string id,T body) where T : class
        {
            var response = _clusterClient.Index<T>(index, type, id, new PostData<T>(body));
         
            var result = Request<T, T>(response, (r) => response.Body);
            return result;
        }
        public ResultWrapper<T> GetItem<T>(string index, string type, string id) where T : class
        {
            var response = _clusterClient.GetSource<T>(index, type, id);
            
            var result = Request<T, T>(response, (r) => response.Body);
            return result;
        }

        public ResultWrapper<T> Request<T, K>(ElasticsearchResponse<K> response, Func<ElasticsearchResponse<K>, T> setResult)
        {
            var result = new ResultWrapper<T>
            {
                IsError = response.HttpStatusCode != (int)HttpStatusCode.OK,
                
                StatusCode = response.HttpStatusCode,
                Result = setResult(response)
            };

            if (response.ServerError != null)
            {
                result.ErrorMessage = response.ServerError.Error.Reason;
            }
            return result;
        }
    }
}
