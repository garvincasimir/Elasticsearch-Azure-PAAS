﻿using Microsoft.Azure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace ElasticsearchWorker.Core
{
    public class ElasticsearchServiceSettings : IElasticsearchServiceSettings
    {
        //Init only via static methods
        private ElasticsearchServiceSettings(){}

        //settings
        protected CloudStorageAccount _StorageAccount;
        protected string _NodeName;
        protected string _UseElasticLocalDataFolder;
        protected string _JavaInstaller;
        protected string _JavaDownloadURL;
        protected string _JavaDownloadType;
        protected string _ElasticsearchInstaller;
        protected string _ElasticsearchDownloadURL;
        protected string _ElasticsearchDownloadType;
        protected string _ElasticsearchPluginContainer;
        protected string _DataShareName;
        protected string _DataShareDrive;
        protected string _EndpointName;
        protected string _DownloadDirectory;
        protected string _LogDirectory;
        protected string _DataDirectory;
        protected string _ElasticsearchDirectory;
        protected string _RootDirectory;
        protected string _TempDirectory;
        protected IEnumerable<string> _NamedPlugins;
        protected IEnumerable<string> _ClusterNodes;
        protected bool _IsAzure;
        protected bool _IsEmulated;
        protected int _ComputedHeapSize;
        protected bool _EnableDataBootstrap;
        protected string _DataBootstrapDirectory;
        protected string _CurrentTransportHost;

        /// <summary>
        /// Init with a storage account
        /// </summary>
        /// <param name="account">Cloud Storage Account</param>
        public static ElasticsearchServiceSettings FromStorage(CloudStorageAccount account)
        { 
            var settings = new ElasticsearchServiceSettings()
            {
                _StorageAccount = account,
                _NodeName = RoleEnvironment.CurrentRoleInstance.Id,
                _UseElasticLocalDataFolder = CloudConfigurationManager.GetSetting("UseElasticLocalDataFolder"),
                _JavaInstaller = CloudConfigurationManager.GetSetting("JavaInstallerName"),
                _JavaDownloadURL = CloudConfigurationManager.GetSetting("JavaDownloadURL"),
                _JavaDownloadType = CloudConfigurationManager.GetSetting("JavaDownloadType"),
                _ElasticsearchInstaller = CloudConfigurationManager.GetSetting("ElasticsearchZip"),
                _ElasticsearchDownloadURL = CloudConfigurationManager.GetSetting("ElasticsearchDownloadURL"),
                _ElasticsearchDownloadType = CloudConfigurationManager.GetSetting("ElasticsearchDownloadType"),
                _ElasticsearchPluginContainer = CloudConfigurationManager.GetSetting("ElasticsearchPluginContainer"),
                _DataShareName = CloudConfigurationManager.GetSetting("ShareName"),
                _DataShareDrive = CloudConfigurationManager.GetSetting("ShareDrive"),
                _EndpointName = CloudConfigurationManager.GetSetting("EndpointName"),
                _DownloadDirectory = RoleEnvironment.GetLocalResource("ArchiveRoot").RootPath,
                _LogDirectory = RoleEnvironment.GetLocalResource("LogRoot").RootPath,
                _DataDirectory = RoleEnvironment.GetLocalResource("ElasticDataRoot").RootPath,
                _ElasticsearchDirectory = RoleEnvironment.GetLocalResource("ElasticRoot").RootPath,
                _RootDirectory = Environment.GetEnvironmentVariable("ROLEROOT"),
                _TempDirectory = RoleEnvironment.GetLocalResource("CustomTempRoot").RootPath,
                _DataBootstrapDirectory = RoleEnvironment.GetLocalResource("DataBootstrapDirectory").RootPath,

            };

            bool.TryParse(CloudConfigurationManager.GetSetting("EnableDataBootstrap"), out settings._EnableDataBootstrap);

            if (string.IsNullOrWhiteSpace(settings._DataBootstrapDirectory) && settings._EnableDataBootstrap)
            {
                settings._EnableDataBootstrap = false;
            }

            var namedPlugins = CloudConfigurationManager.GetSetting("NamedPlugins");
            if (!string.IsNullOrWhiteSpace(namedPlugins))
            {
                settings._NamedPlugins = namedPlugins.Split('|').Where(p => !string.IsNullOrWhiteSpace(p));
            }
            else
            {
                settings._NamedPlugins = new string[0];
            }

            //New nodes will add themselves into the cluster. Ok to make this static and remove discovery plugin.
            settings._ClusterNodes = from r in RoleEnvironment.Roles
                                     from i in r.Value.Instances
                                     from e in i.InstanceEndpoints
                                     where e.Key == settings.EndpointName && i.Id != RoleEnvironment.CurrentRoleInstance.Id
                                     select string.Format("{0}:{1}", e.Value.IPEndpoint.Address, e.Value.IPEndpoint.Port);


            settings._CurrentTransportHost = (from r in RoleEnvironment.Roles
                                              from i in r.Value.Instances
                                              from e in i.InstanceEndpoints
                                              where e.Key == settings.EndpointName && i.Id == RoleEnvironment.CurrentRoleInstance.Id
                                              select string.Format("{0}", e.Value.IPEndpoint.Address)).First();

            if (!settings._RootDirectory.EndsWith(@"\"))
            {
                settings._RootDirectory += @"\";
            }

            //Set root to approot=App directory
            settings._RootDirectory = Path.Combine(settings._RootDirectory, "approot");

            //Emulator does not copy webrole files to approot 
            if (RoleEnvironment.IsEmulated && IsWebRole)
            {
                settings._RootDirectory = Path.Combine(settings._RootDirectory, "bin");
            }

            //Calculate heap size
            MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(memoryStatus);

            var totalPhycialBytesInMB = memoryStatus.ullTotalPhys / 1024L / 1024L;

            //TODO: calculate the lost result which could cause this to throw;
            settings._ComputedHeapSize = Convert.ToInt32(totalPhycialBytesInMB / 2); 

            return settings;
        }

        /// <summary>
        /// Init with a connection string
        /// </summary>
        /// <param name="connectionString">Storage Connection String</param>
        public static ElasticsearchServiceSettings FromStorage(string connectionString)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            return FromStorage(account);
        }

        public CloudStorageAccount StorageAccount{ get { return _StorageAccount; } }
        public string NodeName { get { return _NodeName; } }
        public string UseElasticLocalDataFolder { get { return _UseElasticLocalDataFolder; } }
        public string JavaInstaller { get { return _JavaInstaller; } }
        public string JavaDownloadURL { get { return _JavaDownloadURL; } }
        public string JavaDownloadType { get { return _JavaDownloadType; } }
        public string ElasticsearchInstaller { get { return _ElasticsearchInstaller; } }
        public string ElasticsearchDownloadURL { get { return _ElasticsearchDownloadURL; } }
        public string ElasticsearchPluginContainer { get { return _ElasticsearchPluginContainer; } }
        public string ElasticsearchDownloadType { get { return _ElasticsearchDownloadType; } }
        public string DataShareName { get { return _DataShareName; } }
        public string DataShareDrive { get { return _DataShareDrive; } }
        public string EndpointName { get { return _EndpointName; } }
        public string DownloadDirectory { get { return _DownloadDirectory; } }
        public string LogDirectory { get { return _LogDirectory; } }
        public string DataDirectory { get { return _DataDirectory; } }
        public string ElasticsearchDirectory { get { return _ElasticsearchDirectory; } }
        public string RootDirectory { get { return _RootDirectory; } }
        public string TempDirectory { get { return _TempDirectory; } }
        public bool IsAzure { get { return RoleEnvironment.IsAvailable; } }
        public bool IsEmulated { get { return RoleEnvironment.IsEmulated; } }
        public int ComputedHeapSize { get { return _ComputedHeapSize; } }
        public bool EnableDataBootstrap { get { return _EnableDataBootstrap;  } }
        public string DataBootstrapDirectory { get { return _DataBootstrapDirectory; } }
        public IEnumerable<string> NamedPlugins { get { return _NamedPlugins; } }
        public IEnumerable<string> ClusterNodes { get { return _ClusterNodes; } }
        public string CurrentTransportHost { get { return _CurrentTransportHost; } }
        public string GetExtra(string key)
        {
            return CloudConfigurationManager.GetSetting(key);
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

        private static bool IsWebRole
        {
            get
            {
                return RoleEnvironment.CurrentRoleInstance.Id.ToLower().Contains("webrole");
            }
        }
    }
}
