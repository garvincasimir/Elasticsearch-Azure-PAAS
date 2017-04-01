using ElasticsearchWorker.Utility;
using Microsoft.Win32.SafeHandles;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace ElasticsearchWorker.Core
{
    public class ElasticsearchManager : SoftwareManager
    {
        public const string ELASTICSEARCH_CONFIG_FILE = "elasticsearch.yml";
        public const string ELASTICSEARCH_PLUGIN_DIR = "plugins";
        public const string JAVA_OPTIONS_FILE = "jvm.options";

        protected string _ElasticRoot;
        protected string _PluginRoot;
        protected string _InstallRoot;
        protected string _DataPath;
        protected int _BridgePort;
        protected string _PackagePluginPath;
        protected string _TemplateConfigFile;
        protected string _TemplateLogConfigFile;
        protected Process _process = null;

        protected List<Func<IEnumerable<IWebArtifact>>> _sources = new List<Func<IEnumerable<IWebArtifact>>>();
        protected ConcurrentBag<string> _pluginArtifactPaths;

        public ElasticsearchManager(IElasticsearchServiceSettings settings, string dataPath, int bridgePort)
            : base(settings,settings.ElasticsearchInstaller)
        {
            _InstallRoot = settings.ElasticsearchDirectory;
            _DataPath = dataPath;
            _BridgePort = bridgePort;

            //Maybe there is a less noisy way to do this? 
            if (!string.IsNullOrWhiteSpace(settings.ElasticsearchDownloadType) && settings.ElasticsearchDownloadType == "storage")
            {
                _installer = new StorageArtifact(settings.ElasticsearchDownloadURL, settings.ElasticsearchInstaller, settings.StorageAccount);
            }
            else
            {
                _installer = new WebArtifact(settings.ElasticsearchDownloadURL, settings.ElasticsearchInstaller);
            }

            _PackagePluginPath = Path.Combine(settings.RootDirectory,"plugins");
            _TemplateConfigFile = Path.Combine(settings.RootDirectory,"config",ELASTICSEARCH_CONFIG_FILE);
          

            _ElasticRoot = Path.Combine(_InstallRoot, Path.GetFileNameWithoutExtension(_installer.Name));
            _PluginRoot = Path.Combine(_ElasticRoot, ELASTICSEARCH_PLUGIN_DIR);
          
        }

        public override Task EnsureConfigured()
        {
            var elasticsearchSetup = Task.Run(() =>
            {
                //if elasticsearch zip does not exist download it
                DownloadIfNotExists();

                //clear installation root and extract archive to installation root
                //Should not be necessary with non-persisted resource directory
                Install();

                //Extract all packaged plugins to plugin folder 
                ConfigurePackagePlugins();

                //Write elasticsearch.yaml
                ConfigureElasticsearch();

                PatchJavaOptions();

          
            });

            var mergedConfig = Task.WhenAll( elasticsearchSetup, DownloadAdditionalPlugins()).ContinueWith((t) =>
            {
                ConfigureAdditionalPlugins();

            });

            return mergedConfig;
        }

        protected virtual void Install()
        {
            Trace.TraceInformation("Re-creating elasticshearch root");

            Trace.TraceInformation("Extracting elasticsearch to {0}", _ElasticRoot);
            ZipFile.ExtractToDirectory(_binaryArchive, _InstallRoot);
        }

        protected virtual void ConfigureElasticsearch()
        {
            string configRoot = Path.Combine(_ElasticRoot, "Config");
            if (!Directory.Exists(configRoot))
            {
                Directory.CreateDirectory(configRoot);
            }
            string configFile = Path.Combine(configRoot,ELASTICSEARCH_CONFIG_FILE);

            using (var input = new StreamReader(_TemplateConfigFile))
            using (var output = new StreamWriter(configFile, false))
            {
                Trace.WriteLine("Loading Default Config");
                var config = new Dictionary<string, string>();
                // Load the stream
                var yamlInput = new YamlStream();
                yamlInput.Load(input);


                var rootOutputNode = new YamlMappingNode();
                var outputDoc = new YamlDocument(rootOutputNode);
                var yamlOutput = new YamlStream(outputDoc);


                if (yamlInput.Documents.Count > 0)
                {
                    var mapping = (YamlMappingNode)yamlInput.Documents[0].RootNode;
                    var reservedConfigs = new string[] { "path.data",  "path.logs" };
                    foreach (var entry in mapping.Children.Where(m => !reservedConfigs.Contains(m.Key.ToString())))
                    {
                        rootOutputNode.Add(entry.Key, entry.Value);
                    }
                }

                Trace.WriteLine("Writing Critical Config values");
                //write important config values reglardless of what was provided in package

                rootOutputNode.Add(new YamlScalarNode("path.data"), new YamlScalarNode(_DataPath));
                
                rootOutputNode.Add(new YamlScalarNode("path.logs"), new YamlScalarNode(_Settings.LogDirectory));
   
                rootOutputNode.Add(new YamlScalarNode("node.name"), new YamlScalarNode(_Settings.NodeName));
               

                yamlOutput.Save(output);
                Trace.TraceInformation("Saved elasticsearch config file {0}", configFile);

            }

        }

        protected virtual void PatchJavaOptions()
        {
            string javaOptions = Path.Combine(_ElasticRoot, "Config", JAVA_OPTIONS_FILE);
            var text = File.ReadAllText(javaOptions);
            text = Regex.Replace(text, "-Xm[sx]", "# $0");
            File.WriteAllText(javaOptions, text);
        }

        protected virtual void ConfigurePackagePlugins()
        {
            var packagePlugins = Directory.GetFiles(_PackagePluginPath, "*.zip");
           
            Directory.CreateDirectory(_PluginRoot);

            ExtractPlugins(_PluginRoot, packagePlugins);

        }



        public virtual void AddPluginSource(string containerName, CloudStorageAccount account)
        {
            _sources.Add(() =>
            {
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(containerName);
                container.CreateIfNotExists();

                var artifacts = new List<StorageArtifact>();


                foreach (var item in container.ListBlobs(null, true))
                {
                    var fileName = Path.GetFileName(item.StorageUri.PrimaryUri.AbsoluteUri);
                    artifacts.Add(new StorageArtifact(item.StorageUri.PrimaryUri.AbsoluteUri, fileName, account));
                }

                return artifacts;


            });
        }

        protected virtual Task DownloadAdditionalPlugins()
        {
            _pluginArtifactPaths = new ConcurrentBag<string>();
            var processSources = _sources.Select(s => Task.Run(s).ContinueWith((source) =>
            {
                foreach (var artifact in source.Result)
                {
                    var filePath = Path.Combine(_archiveRoot, artifact.Name);
                    _pluginArtifactPaths.Add(filePath);
                    if (!File.Exists(filePath))
                    {
                        Task.Factory.StartNew(() =>
                        {
                            artifact.DownloadTo(filePath);
                        }, TaskCreationOptions.AttachedToParent);
                    }

                }
            }));



            return Task.WhenAll(processSources);
        }

        protected virtual void ConfigureAdditionalPlugins()
        {
            ExtractPlugins(_PluginRoot, _pluginArtifactPaths);
        }

        public virtual void StartAndBlock(CancellationToken token, string javaHome = null)
        {

            if (!token.IsCancellationRequested)
            {
                
                string startupScript = Path.Combine(_ElasticRoot, "bin", "elasticsearch.bat");
                _process = new Process();
                _process.StartInfo = new ProcessStartInfo
                {
                    FileName = startupScript,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = "--silent"
                };

                if (!string.IsNullOrWhiteSpace(javaHome))
                {
                    _process.StartInfo.EnvironmentVariables["JAVA_HOME"] = javaHome;
                }

                if(_Settings.ComputedHeapSize > 0 && !_Settings.IsEmulated)
                {
                    _process.StartInfo.EnvironmentVariables["ES_JAVA_OPTS"] = string.Format("-Xms{0}m -Xmx{0}m", _Settings.ComputedHeapSize);
                }
                else if (_Settings.IsEmulated)
                {
                    _process.StartInfo.EnvironmentVariables["ES_JAVA_OPTS"] = string.Format("-Xms{0}m -Xmx{0}m", 250);
                }

                _process.StartInfo.EnvironmentVariables["BRIDGE_PORT"] = _BridgePort.ToString();

                _process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    Trace.TraceInformation(e.Data);
                };

                Trace.TraceInformation("Starting Elasticsearch with script {0}", startupScript);

                _process.Start();
                _process.BeginOutputReadLine();

                var processEnded = new ManualResetEvent(false);

                processEnded.SafeWaitHandle = new SafeWaitHandle(_process.Handle, false);
 
                int index = WaitHandle.WaitAny(new[] { processEnded, token.WaitHandle });
                Trace.TraceInformation("Proces handle signaled: " + index);
                    
                //If the signal came from the caller cancellation token close the window
                if (index == 1)
                {
                    Stop();
                }

                var errors = _process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    Trace.TraceError("Proccess error output: \n{0} ", errors);
                }
            }


            
        }

        public virtual void Stop()
        {
            if (_process == null)
            {
                return;
            }

            if (_process.HasExited)
            {
                return;
            }
           
            _process.CloseMainWindow();
            
        }

        protected virtual void ExtractPlugins(string destination, IEnumerable<string> files)
        {
            Parallel.ForEach(files, (file) =>
            {
                var pluginFileName = Path.GetFileNameWithoutExtension(file);
                var pluginPath = Path.Combine(destination, pluginFileName);

                ZipFile.ExtractToDirectory(file, pluginPath);
                Trace.TraceInformation("Extracted plugin {0}", pluginFileName);
            });
        }

        public virtual void InstallNamedPlugins(CancellationToken token,string javaHome)
        {
            string pluginInstaller = Path.Combine(_ElasticRoot, "bin", "elasticsearch-plugin.bat");

            Parallel.ForEach(_Settings.NamedPlugins, p =>
            {
                 var pluginInstall = new Process();
                 pluginInstall.StartInfo = new ProcessStartInfo
                 {
                     FileName = pluginInstaller,
                     UseShellExecute = false,
                     RedirectStandardOutput = true,
                     RedirectStandardError = true,
                     Arguments = p,

                 };

                 pluginInstall.StartInfo.EnvironmentVariables["JAVA_HOME"] = javaHome;
                 pluginInstall.Start();
                 pluginInstall.BeginOutputReadLine();


                 pluginInstall.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                 {
                     Trace.TraceInformation(e.Data);
                 };

                 var installEnded = new ManualResetEvent(false);

                 installEnded.SafeWaitHandle = new SafeWaitHandle(pluginInstall.Handle, false);

                 int index = WaitHandle.WaitAny(new[] { installEnded, token.WaitHandle });
                 Trace.TraceInformation("Process handle signaled Installing Named Plugin '{0}' : {1} " ,p, index);

                 //If the signal came from the the window
                 if (index == 0 && pluginInstall.ExitCode != 0)
                 {
                     var errors = pluginInstall.StandardError.ReadToEnd();
                     
                     //Can't recover from plugin install failure
                     throw new Exception(string.Format("Error in plugin Installer '{0}' error output: \n{1} ", p, errors));
                 }
           });

        }

    }
}
