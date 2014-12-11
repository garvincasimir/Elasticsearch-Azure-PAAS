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
    //TODO: Support for file signature verification
    public  class WebArtifact
    {
        protected string _SourceURL;
        protected string _Name;
        protected string _TempPath;

        public string SourceUrl
        {
            get{return _SourceURL;}
        }

        public string Name
        {
            get { return _Name; }
        }
        public WebArtifact(string sourceURL, string name, string tempPath)
        {
            _SourceURL = sourceURL;
            _Name = name;
            _TempPath = tempPath;
        }

        public virtual void Download(string filePath, bool useTemp = true)
        {
            var client = new WebClient();
            

            string downloadDestination = useTemp ? Path.Combine(_TempPath,Guid.NewGuid().ToString()) : filePath;

            client.DownloadFile(_SourceURL, downloadDestination);

            if (useTemp)
            {
                File.Copy(downloadDestination, filePath,true);
            }

            Trace.TraceInformation("{0} download complete", filePath);


            
        }



    }
}
