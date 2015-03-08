using System;
namespace ElasticsearchWorker.Utility
{
    public interface IWebArtifact
    {
        void DownloadTo(string filePath);
        string Name { get; }
        string SourceUrl { get; }
    }
}
