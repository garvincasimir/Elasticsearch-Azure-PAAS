using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
    public class ElasticsearchNode
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public string NodeName { get; set; }
        public string Status { get; set; }
    }
}
