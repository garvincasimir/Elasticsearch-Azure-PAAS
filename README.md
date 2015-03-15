Elasticsearch-Azure-PAAS
========================

[![Join the chat at https://gitter.im/garvincasimir/Elasticsearch-Azure-PAAS](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/garvincasimir/Elasticsearch-Azure-PAAS?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This is a Visual Studio project for creating an [Elasticsearch](http://http://www.elasticsearch.org) cluster on Microsoft Azure using worker roles. 
![System Design](https://garvincasimir.files.wordpress.com/2014/10/elasticsearch-paas.png "Project Conceptual Design")

Who is this for?
---------------------------
This is for people who want to run Elastic search on Azure in the Platform as a Service enviroment. This is also an opportunity to test Elasticsearch in a simulated distributed environment. 

How does this work?
----------------------
This is a visual studio project which can serve as a base for a solution based on an Elasticsearch cluster. The intent is to handle all the different aspecs of setting up and managing a cluster
* Installation 
* Configuration
* Plugin Setup
* Logging
* Snapshots
* Automatic Node Discovery
* Security

Typcial usage involves installing the NuGet package on existing Web or Worker role in your visual studio project. Once the package is installed you update the configuration settings and include the service initializer in your WorkerRole.cs or WebRole.cs.

Do I need an Azure Account to try this?
---------------------------------------
No, it runs in the full Azure Emulator on your desktop. The project is designed to work with azure files service for data and snapshots but falls back to a resource folder in the Azure Emulator. Other than that, there is no significant difference between running this project on the Azure Emulator and publishing it to Azure. 

Installation
-------------------
Install the NuGet package

    Install-Package Elasticsearch-Azure-PAAS -Version 1.0.0-Beta -Pre

This package will add the required settings to any cloud service projects that refer to the Web or Worker Role the packaged was installed on.


Settings
-------------------
There are a couple config steps before you can run it either in the Azure Emulator or in an Azure Cloud Service. The NuGet package has already added those settings with defaults from this project. Please change them where necessary.

######JavaDownloadURL
The service will download the java jre installer from this url.

**Default**: http://127.0.0.1:10000/devstoreaccount1/installers/jre-8u40-windows-x64.exe

######JavaDownloadType 
This tells the service whether **JavaDownloadURL** is a web accessible location or on the configured storage account

**Default**: storage
**Options**: web, storage

######JavaInstallerName
This is simply the name used to save the jre installer into the download cache on the role instance

**Default**: jre-8u40-windows-x64.exe

######ElasticsearchDownloadURL
The service will download the java jre installer from this url.

**Default**: https://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-1.4.3.zip

######ElasticsearchDownloadType 
This tells the service whether **ElasticsearchDownloadURL** is a web accessible location or on the configured storage account

**Default**: storage
**Options**: web, storage

######ElasticsearchZip
This is simply the name used to save the elasticsearch package into the download cache on the role instance

**Default**: elasticsearch-1.4.3.zip

######StorageConnection
The service will use this connection string to download any download types marked as *storage*. It will also be used to create the share used to store elasticsearch data.

**Default**: UseDevelopmentStorage=true

######ShareName
This config value will be used the name of the azure file service share. https://myaccount.file.core.windows.net/[ShareName]. This share will be used as a persistent store for elasticsearch data and snapshots.

**Default**: elasticdata

######ShareDrive
This is the drive letter assined to the azure file service share on the role instance

**Default**: P:

######EndpointName
This is the name of the endpoint which elasticsearch nodes in the cluster will use to communicate which each other

**Default**: elasticsearch

######UseElasticLocalDataFolder
If this option is enabled, the service will store data on the role instance rather than on the azure file service share. This might be handy when you need the maxium i/o speed and your data is easily replaceable. This is the only available option when using the emulator.

**Default**: true

######ElasticsearchPluginContainer
This is the name of a container accessible through the storage account in the **StorageConnection** setting which contains plugin zip files you intend to install in your cluster.

**Default**: elasticsearchplugins

Usage
------------------------
Once the package is installed and all configuration values are correct you can go ahead and add the initilize the service in WebRole.cs or WorkerRole.cs

```
    public class WorkerRole : RoleEntryPoint
    {
        private CloudStorageAccount storage = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnection"));
        private ElasticsearchService service;
        public override void Run()
        {
            service.RunAndBlock();
        }

        public override bool OnStart()
        {
            // Not sure what this should be. Hopefully storage over smb doesn't open a million connections
            ServicePointManager.DefaultConnectionLimit = 12;

   
            var settings = ElasticsearchServiceSettings.FromStorage(storage);
            service = ElasticsearchService.FromSettings(settings);
            bool result = base.OnStart();

            Trace.TraceInformation("ElasticsearchRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("ElasticsearchRole is stopping");

            service.OnStop();


            base.OnStop();

            Trace.TraceInformation("ElasticsearchRole has stopped");
        }


    }
```

Running in the Emulator
-------------------------

![Project Running](https://garvincasimir.files.wordpress.com/2014/11/elasticsearch-azure-paas-running1.png "Running in Emulator with Fiddler for test")

The Discovery Plugin
-----------------------
The discovery plugin gets information about the role instances from the webrole using named pipes. I would have used the java azure sdk Runtime api but the named pipe it depends on is only available when using the ProgramEntryPoint option. The code can be viewed in the [Elasticsearch-Azure-PAAS-Plugin](https://github.com/garvincasimir/Elasticsearch-Azure-PAAS-Plugin) repository. There is lots of cleaning to be done. 

Other Useful Plugins
-----------------------
Although I want to keep this project as generic as possible, if anyone has suggestions for plugins that everyone should definitely be using let me know.
