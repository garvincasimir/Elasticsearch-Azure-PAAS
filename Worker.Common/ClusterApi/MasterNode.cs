﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElasticsearchWorker.IndexResponse
{
    public class MasterNode
    {
        public string master_node { get; set; }
        public Dictionary<string,NodeItem> nodes {get;set;}
    }
}
