using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticsearchWorker.ClusterApi
{
    public class ResultWrapper<T>
    {
        public T Result { get; set; }
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }
        public int? StatusCode { get; set; }
    }
}
