using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
    public abstract class SoftwareManager
    {
        protected WebArtifact _artifact;
        protected string _archiveRoot;
        protected string _logRoot;
        protected string _binaryArchive;

        public SoftwareManager(WebArtifact artifact, string archiveRoot, string logRoot)
        {
            _archiveRoot = archiveRoot;
            _logRoot = logRoot;
            _binaryArchive = Path.Combine(archiveRoot, artifact.Name);
            _artifact = artifact;
        }

        public abstract Task EnsureConfigured();

        protected virtual bool BinaryExists()
        {
            var binaryExists = File.Exists(_binaryArchive);
            return binaryExists;
        }

        protected virtual void DownloadIfNotExists()
        {
            if (!BinaryExists())
            {
                Trace.TraceInformation("{0} not found. Downloading.....",_binaryArchive);
                _artifact.DownloadTo(_binaryArchive);
            }
        }
    }
}
