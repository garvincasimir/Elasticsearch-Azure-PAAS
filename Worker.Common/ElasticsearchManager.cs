using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using YamlDotNet.RepresentationModel;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using Microsoft.WindowsAzure.Storage;

namespace Worker.Common
{
    public class ElasticsearchManager : SoftwareManager
    {
        public const string ELASTICSEARCH_CONFIG_FILE = "elasticsearch.yml";
        public const string ELASTICSEARCH_LOG_CONFIG_FILE = "logging.yml";
        public const string ELASTICSEARCH_PLUGIN_DIR = "plugins";

        protected string _ElasticRoot;
        protected string _PluginRoot;
        protected string _InstallRoot;
        protected string _DataPath;
        protected string _BridgePipeName;
        protected string _PackagePluginPath;
        protected string _TemplateConfigFile;
        protected string _TemplateLogConfigFile;
        protected Process _process = null;

        protected List<Func<IEnumerable<WebArtifact>>> _sources = new List<Func<IEnumerable<WebArtifact>>>();
        protected ConcurrentBag<string> _pluginArtifactPaths;

        public ElasticsearchManager(IElasticsearchServiceSettings settings, string dataPath, string bridgeName)
            : base(settings,settings.ElasticsearchInstaller)
        {
            _InstallRoot = settings.ElasticsearchDirectory;
            _DataPath = dataPath;
            _BridgePipeName = bridgeName;

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
            _TemplateLogConfigFile = Path.Combine(settings.RootDirectory, "config", ELASTICSEARCH_LOG_CONFIG_FILE);

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

                //Write logging.yaml
                ConfigureElastisearchLogging();

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
                    var reservedConfigs = new string[] { "path.data", "path.work", "path.logs", "path.plugins" };
                    foreach (var entry in mapping.Children.Where(m => !reservedConfigs.Contains(m.Key.ToString())))
                    {
                        rootOutputNode.Add(entry.Key, entry.Value);
                    }
                }

                Trace.WriteLine("Writing Critical Config values");
                //write important config values reglardless of what was provided in package

                rootOutputNode.Add(new YamlScalarNode("path.data"), new YamlScalarNode(_DataPath));
                rootOutputNode.Add(new YamlScalarNode("path.work"), new YamlScalarNode(_Settings.TempDirectory));
                rootOutputNode.Add(new YamlScalarNode("path.logs"), new YamlScalarNode(_Settings.LogDirectory));
                rootOutputNode.Add(new YamlScalarNode("path.plugin"), new YamlScalarNode(_PluginRoot));
                rootOutputNode.Add(new YamlScalarNode("node.name"), new YamlScalarNode(_Settings.NodeName));
                rootOutputNode.Add(new YamlScalarNode("cloud.azureruntime.bridge"), new YamlScalarNode(_BridgePipeName));

                yamlOutput.Save(output);
                Trace.TraceInformation("Saved elasticsearch config file {0}", configFile);

            }

        }

        protected virtual void ConfigurePackagePlugins()
        {
            var packagePlugins = Directory.GetFiles(_PackagePluginPath, "*.zip");
           
            Directory.CreateDirectory(_PluginRoot);

            ExtractPlugins(_PluginRoot, packagePlugins);

        }

        protected virtual void ConfigureElastisearchLogging()
        {
            string configFile = Path.Combine(_ElasticRoot, "Config", ELASTICSEARCH_LOG_CONFIG_FILE);
            File.Copy(_TemplateLogConfigFile, configFile,true);
            Trace.TraceInformation("Created logging config {0}", configFile);
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
                    RedirectStandardError = true
                };

                if (!string.IsNullOrWhiteSpace(javaHome))
                {
                    _process.StartInfo.EnvironmentVariables["JAVA_HOME"] = javaHome;
                }

                if(_Settings.ComputedHeapSize > 0 && !_Settings.IsEmulated)
                {
                    _process.StartInfo.EnvironmentVariables["ES_HEAP_SIZE"] = string.Format("{0}m", _Settings.ComputedHeapSize);
                }

                _process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    /*
                     * I don't like this. Only alternative is to patch the batch file.
                     * Created issue: https://github.com/elasticsearch/elasticsearch/issues/8913
                     */
                    if(e.Data == "JAVA_HOME environment variable must be set!")
                    {
                        Trace.TraceError("Batch script could not read JAVA_HOME variable");
                        _process.Kill();

                        Trace.TraceInformation("Killed elastic search.");
                    }
                };


                Trace.TraceInformation("Starting Elasticsearch with script {0}", startupScript);

                _process.Start();
                _process.BeginOutputReadLine();

                var processEnded = new ManualResetEvent(false);

                processEnded.SafeWaitHandle = new SafeWaitHandle(_process.Handle, false);
 
                int index = WaitHandle.WaitAny(new[] { processEnded, token.WaitHandle });
                Trace.TraceInformation("One more more ES handles signaled: " + index);
                    
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
    }
}
