namespace ElasticsearchWorker.Core
{
    public interface IElasticsearchServiceSettings
    {
        string DataDirectory { get; }
        string DataShareDrive { get; }
        string DataShareName { get; }
        string DownloadDirectory { get; }
        string ElasticsearchDirectory { get; }
        string ElasticsearchDownloadURL { get; }
        string ElasticsearchDownloadType { get; }
        string ElasticsearchInstaller { get; }
        string ElasticsearchPluginContainer { get; }
        string EndpointName { get; }
        string JavaDownloadURL { get; }
        string JavaInstaller { get; }
        string JavaDownloadType { get; }
        string LogDirectory { get; }
        string NodeName { get; }
        string RootDirectory { get; }
        Microsoft.WindowsAzure.Storage.CloudStorageAccount StorageAccount { get; }
        string TempDirectory { get; }
        string UseElasticLocalDataFolder { get; }
        bool IsAzure { get; }
        bool IsEmulated { get; }
        int ComputedHeapSize { get; }
        bool EnableDataBootstrap { get; }
        string DataBootstrapDirectory { get; }
        string GetExtra(string key);

    }
}
