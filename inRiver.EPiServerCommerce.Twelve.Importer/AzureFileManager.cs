using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EPiServer.Logging;

namespace inRiver.EPiServerCommerce.Twelve.Importer
{
    public class AzureFileManager
    {
        private static AzureFileManager instance;

        private static string _ConnectionString;
        private static string _ShareName;
        private static string _RootDirectoryName;
        private static string _GetDefaultPath;
        private static CloudStorageAccount _CSAccount;
        private static CloudFileShare _CFShare;
        private static CloudFileDirectory _RootDirectory;
        private static bool _UseAzure = false;
        private static bool _HaveLoadedConfiguration = false;

        private static readonly ILogger Log = EPiServer.Logging.LogManager.GetLogger(typeof(AzureFileManager));

#region AzureFileManager_Creation
        private AzureFileManager()
        {
            if(!_HaveLoadedConfiguration)
            {
                LoadConfigurationSettings();
                _HaveLoadedConfiguration = true;
                Log.Debug("Completed loading Azure connection settings");
            }
            Log.Debug("Connection string = " + _ConnectionString);
            if (!string.IsNullOrEmpty(GetAzureStorageConnectionString))
            {
                _CSAccount = OpenCloudStorageAccount(GetAzureStorageConnectionString);
                _CFShare = OpenCloudFileShare(_CSAccount, GetAzureShareName);
                _RootDirectory = OpenCloudDirectoryShareRoot(_CFShare, GetAzureRootDirectory);
                _UseAzure = true;
                Log.Debug("Opened connection to Azure storage account");
            }
            else
            {
                instance = null;
                Log.Debug("Azure connection information not set using local file system for import functionality.");
            }
        }

        public CloudStorageAccount OpenCloudStorageAccount(string storageConnectionString)
        {
            CloudStorageAccount storageAccount = null;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (Exception exp)
            {
                Log.Error("The specified Azure connectionstring failed with the exception - " + exp.ToString());
            }
            return storageAccount;
        }

        public CloudFileShare OpenCloudFileShare(CloudStorageAccount cloudStorageAccount, string shareName)
        {
            CloudFileShare cloudFileShare = null;
            try
            {
                CloudFileClient cloudFileClient = cloudStorageAccount.CreateCloudFileClient();
                cloudFileShare = cloudFileClient.GetShareReference(shareName);
            }
            catch (Exception exp)
            {
                Log.Error("The specified Azure share name failed with the exception - " + exp.ToString());
            }
            return cloudFileShare;
        }

        public CloudFileDirectory OpenCloudDirectoryShareRoot(CloudFileShare cloudFileShare, string azureRootDirectory)
        {
            CloudFileDirectory rootDir = null;
            try
            {
                CloudFileDirectory container = cloudFileShare.GetRootDirectoryReference();
                rootDir = container.GetDirectoryReference(azureRootDirectory);
            }
            catch (Exception exp)
            {
                Log.Error("An error occurred opening share root directory with exception - " + exp.ToString());
            }
            return rootDir;
        }

        #endregion

#region fileprocessing
        public static AzureFileManager Instance
        {
            get
            {
                if(instance == null)
                {
                    instance = new AzureFileManager();
                }
                return instance;
            }
        }

        public static bool UseAzure
        {
            get
            {
                return _UseAzure;
            }
        }

        public MemoryStream GetFileStream(string path)
        {
            string amendedPath = path.Replace(GetDefaultPath, ""); // Remove the default path so we are accessing files from share root
            string fileName  = Path.GetFileName(path);
            amendedPath = amendedPath.Replace(fileName, "");
            CloudFile targetFile = CloudFileExists(amendedPath, fileName);
            if (targetFile != null)
            {
                MemoryStream targetStream = new MemoryStream();
                targetFile.DownloadToStream(targetStream);
                return targetStream;
            }
            return null;
        }

        private CloudFile CloudFileExists( string path, string fileName)
        {
            try
            {
                CloudFileDirectory targetDir = FindDirectory(path, _RootDirectory);
                if (targetDir.Exists())
                {
                    CloudFile targetFile = targetDir.GetFileReference(fileName);
                    if (targetFile.Exists())
                    {
                        return targetFile;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception exp)
            {
                string errMess = "Failed to find file - " + path + " with exception - " + exp.ToString();
                Log.Error(errMess);
            }
            return null;
        }

        private CloudFileDirectory FindDirectory(string path, CloudFileDirectory parentDirectory)
        {
            List<string> dirPath = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            if (Path.IsPathRooted(path))
            {
                dirPath = dirPath.Skip<string>(1).ToList<string>();
            }
            dirPath = dirPath.Reverse<string>().ToList<string>();

            return FindDirectory(dirPath, parentDirectory);
        }

        private CloudFileDirectory FindDirectory(List<string> directoryPath, CloudFileDirectory parentDirectory)
        {
            string targetDirName = directoryPath.Last<string>();
            int pathLength = (int)directoryPath.LongCount() - 1;
            if (pathLength >= 0)
            {
                List<string> newDirPath = directoryPath.Take(pathLength).ToList();
                CloudFileDirectory targetDir = parentDirectory.GetDirectoryReference(targetDirName);
                if (targetDir.Exists())
                {
                    if (pathLength == 0) // we have arrived
                    {
                        return targetDir;
                    }
                    return FindDirectory(newDirPath, targetDir); // keep looking
                }
                else
                {
                    Exception exp = new Exception("The target directory was not found.");
                    throw exp;
                }
            }
            else
            {
                return null;
            }
        }
#endregion

        #region configuration

        private void LoadConfigurationSettings()
        {
            var appSettings = ConfigurationManager.AppSettings;
            var environmentName = appSettings["episerver:EnvironmentName"];
            if (!string.IsNullOrEmpty(environmentName))
            {
                _ConnectionString = LoadConfigurationItem(environmentName + "." + "inRiver.AzureStorageConnectionString");
                _ShareName = LoadConfigurationItem(environmentName + "." + "inRiver.AzureShareName");
                _RootDirectoryName = LoadConfigurationItem(environmentName + "." + "inRiver.AzureRootDirectory");
                _GetDefaultPath = LoadConfigurationItem(environmentName + "." + "inRiver.GetDefaultPath");
            }
            else
            {
                _ConnectionString = LoadConfigurationItem( "inRiver.AzureStorageConnectionString");
                _ShareName = LoadConfigurationItem( "inRiver.AzureShareName");
                _RootDirectoryName = LoadConfigurationItem( "inRiver.AzureRootDirectory");
                _GetDefaultPath = LoadConfigurationItem( "inRiver.GetDefaultPath");
            }
        }

        private string LoadConfigurationItem(string appSettingsKey)
        {
            if (ConfigurationManager.AppSettings.Count > 0)
            {
                if (!string.IsNullOrEmpty(appSettingsKey)) 
                {
                    return ConfigurationManager.AppSettings[appSettingsKey];
                }
            }
            string debugMess = string.Format("The required configuration has not been set for the inRiver PIM connector, the setting for " + appSettingsKey + " is missing.");
            Log.Debug(debugMess);
            return string.Empty;

        }

        private string GetAzureStorageConnectionString
        {
            get
            {
                return _ConnectionString;
            }
        }



        private string GetAzureShareName
        {
            get
            { 
                return _ShareName;
            }
        }

        private string GetAzureRootDirectory
        {
            get
            {
                return _RootDirectoryName;
            }
        }

        private string GetDefaultPath
        {
            get
            {
                return _GetDefaultPath;
            }
        }

#endregion

    }
}
