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
        protected string _binaryArchive;
        protected Uri _binaryDownloadURL;
        protected string _archiveRoot;
        protected string _logRoot;

        public SoftwareManager(string binary, string binaryDownloadURL, string archiveRoot, string logRoot)
        {
            _binaryArchive = Path.Combine(archiveRoot, binary);
            _binaryDownloadURL = new Uri(binaryDownloadURL);
            _archiveRoot = archiveRoot;
            _logRoot = logRoot;
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

                var client = new WebClient();

                //Download to temporary file so we have less cleanup issues if download fails or operation is cancelled
                var tempBinaryArchive = Path.GetTempFileName();

                client.DownloadFile(_binaryDownloadURL, tempBinaryArchive);

                Trace.TraceInformation("Download from {0} complete.....",_binaryDownloadURL);

                File.Copy(tempBinaryArchive, _binaryArchive);

                Trace.TraceInformation("{0} Created",_binaryArchive);

            }
        }
    }
}
