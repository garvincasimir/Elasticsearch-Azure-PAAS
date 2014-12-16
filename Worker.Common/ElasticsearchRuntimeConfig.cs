using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
    public class ElasticsearchRuntimeConfig
    {
        public ElasticsearchRuntimeConfig()
        {
            MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX();

            ElasticsearchRuntimeConfig.GlobalMemoryStatusEx( memoryStatus);

           var totalPhycialBytesInMB = memoryStatus.ullTotalPhys / 1024L / 1024L;

           _ES_HEAP_SIZE = Convert.ToInt32(totalPhycialBytesInMB / 2);

           if (_ES_HEAP_SIZE == 0)
           {
               Trace.TraceWarning("Unable to calculate ES_HEAP_SIZE based on available memory");
           }
           else
           {
               Trace.TraceInformation("Calculated ES_HEAP_SIZE is {0}MB based on physical memory of {0}MB", _ES_HEAP_SIZE, totalPhycialBytesInMB);
           }

        
        }

        private int _ES_HEAP_SIZE;
        public string DataPath { get; set; }
        public string TempPath { get; set; }
        public string LogPath { get; set; }
        public string PackagePluginPath { get; set; }
        public string NodeName { get; set; }
        public string BridgePipeName { get; set; }
        public string TemplateConfigFile { get; set; }
        public string TemplateLogConfigFile { get; set; }
        public int ES_HEAP_SIZE 
        {
            get
            {
                return _ES_HEAP_SIZE;
            }
            set
            {
                _ES_HEAP_SIZE = value;
            }

        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(this);
            }
        }


        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    }
}
