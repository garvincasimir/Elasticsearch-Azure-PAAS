using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticsearchWorker
{
    /// <summary>
    /// State of index bootstrapper. 
    /// Saved to cluster so any node can run data bootstrapers
    /// </summary>
    public class IndexDataSource
    {
        public string Name { get; set; }
        public DateTime? LastUpdated {get;set;}
        public DateTime? NextUpdate { get; set; }
        public DateTime? LastErrorDate { get; set; }
        public string LastErrorMessage { get; set; }
        
    }
}
