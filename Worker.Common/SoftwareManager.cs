using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ElasticsearchWorker
{
    public abstract class SoftwareManager
    {
        protected WebArtifact _installer;
        protected string _archiveRoot;
        protected string _logRoot;
        protected string _binaryArchive;
        protected IElasticsearchServiceSettings _Settings;

        public SoftwareManager(IElasticsearchServiceSettings settings, string installer)
        {
            _Settings = settings;
            _archiveRoot = _Settings.DownloadDirectory;
            _logRoot = _Settings.LogDirectory;
            _binaryArchive = Path.Combine(settings.DownloadDirectory, installer);
        }

        public abstract Task EnsureConfigured();

        protected virtual bool Downloaded()
        {
            return File.Exists(_binaryArchive);
        }

        protected virtual void DownloadIfNotExists()
        {
            if (!Downloaded())
            {
                Trace.TraceInformation("{0} not found. Downloading.....",_binaryArchive);
                _installer.DownloadTo(_binaryArchive);
            }
        }
    }
}
