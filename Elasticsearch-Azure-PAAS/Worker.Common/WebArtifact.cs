using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Worker.Common
{
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

        public virtual void Download(string filePath,bool useTemp=true)
        {
            var client = new WebClient();
           

            string downloadDestination = useTemp ? Path.GetTempFileName() : filePath;

            client.DownloadFile(_SourceURL, downloadDestination);

            if (useTemp)
            {
                File.Copy(downloadDestination, filePath);
            }


            
        }



    }
}
