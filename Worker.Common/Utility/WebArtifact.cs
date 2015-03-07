using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace ElasticsearchWorker.Utility
{
    //TODO: Support for file signature verification
    public  class WebArtifact
    {
        protected string _SourceURL;
        protected string _Name;

        public string SourceUrl
        {
            get{return _SourceURL;}
        }

        public string Name
        {
            get { return _Name; }
        }
        public WebArtifact(string sourceURL, string name)
        {
            _SourceURL = sourceURL;
            _Name = name;
        }

        public virtual void DownloadTo(string filePath)
        {
            var client = new WebClient();
            var directory = Path.GetDirectoryName(filePath);
            var temFileName = Guid.NewGuid().ToString();
             
            string downloadDestination = Path.Combine( directory,temFileName);

            client.DownloadFile(_SourceURL, downloadDestination);

            File.Move(downloadDestination, filePath);

            Trace.TraceInformation("{0} download complete", filePath);
            
        }

    }
}
