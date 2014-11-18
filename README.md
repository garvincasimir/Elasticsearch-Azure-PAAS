Elasticsearch-Azure-PAAS
========================

This is a Visual Studio project for creating an Elasticsearch cluster on Microsoft Azure using worker roles. It is designed to work with azure files service for data and snapshots. The only difference between running this project in the Azure Emulator and an Azure Cloud Service is, the emulator does not support mounting Azure File Service shares as drives is. For this reason, the emulator stores its data in a resource folder.


Running the project
========================
I tried my best to allow someone to clone this project and run it without doing any configuration. Unfortunately, there are a couple config steps before you can run it either in the Azure Emulator or in an Azure Cloud Service. The many configuration values are needed for the role to download and install java, then download, configure and run Elasticsearch.

1. Download the java jdk installer and copy it to a url accessible to the role or copy it to the storage account associated with the role. 
  2. If you copied the installer to a storage account 
    3. Copy the full storage url to the role setting "JavaDownloadURL" (default:http://127.0.0.1:10000/devstoreaccount1/installers/jdk-8u25-windows-x64.exe)
    4. Change the role setting "JavaDownloadType" to "storage" (default:storage)
    5. The role will look for the file in the storage account created from the connection string "StorageConnection" (default: Develoment Storage)
  5. If you copied the installer to a regular website or a storage url that can be accessed publicly
    6. Copy the full url to the role setting "JavaDownloadURL"
    7. Change the role setting "JavaDownloadType" to "web" (default:storage)
8. Set the role setting "JavaInstallerName" to the name of the exe you downloaded. (default: jdk-8u25-windows-x64.exe)
9. You have the option of leaving the defaults for Elasticsearch and it will download the zip file from the Elasticsearch website. If not, you can download the elasticsearch zipfile and set the corresponding config settings just like you did for java
10. Once the project knows the names of the installers/packages it will be using and their download locations you can run the project using the full emulator.
11. You should see two elasticsearch console windows open with logging information. 
12. To check if discovery is working, open the following url in your browser or fiddler: http://localhost:9200/_nodes
  13. When Elasticsearch has properly initialized, it should show two nodes.  
