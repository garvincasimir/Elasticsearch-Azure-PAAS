using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
    public class ElasticsearchRuntimeConfig
    {
        public string DataPath { get; set; }
        public string TempPath { get; set; }
        public string LogPath { get; set; }
        public string PackagePluginPath { get; set; }
        public string NodeName { get; set; }
        public string BridgePipeName { get; set; }
        public string TemplateConfigFile { get; set; }
        public string TemplateLogConfigFile { get; set; }

    }
}
