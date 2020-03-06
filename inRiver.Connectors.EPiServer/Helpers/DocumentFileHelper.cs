using inRiver.Connectors.EPiServer.EpiXml;
using inRiver.Connectors.EPiServer.Utilities;
using inRiver.EPiServerCommerce.CommerceAdapter.Helpers;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Objects;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.File;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using static System.String;
using LogLevel = inRiver.Remoting.Log.LogLevel;
using XmlDocumentType = inRiver.Connectors.EPiServer.Enums.XmlDocumentType;

namespace inRiver.Connectors.EPiServer.Helpers
{
    public class DocumentFileHelper
    {
        public static void SaveDocument(string channelIdentifier, XDocument doc, Configuration config, string folderDateTime, inRiverContext context)
        {
            string dirPath = Path.Combine(config.ResourcesRootPath, folderDateTime);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string filePath = Path.Combine(dirPath, "Resources.xml");
            context.Log(
                LogLevel.Information,
                string.Format("Saving document to path {0} for channel:{1}", filePath, channelIdentifier));
            doc.Save(filePath);
        }

        public static void SaveDocumentToAzure(XDocument doc, Configuration config, string folderDateTime)
        {
            if (IsNullOrEmpty(config.StorageAccountName) ||
                IsNullOrEmpty(config.StorageAccountKey) ||
                IsNullOrEmpty(config.StorageAccountShareReference) ||
                IsNullOrEmpty(config.StorageAccountCatalogDirectoryReference))
            {
                return;
            }

            StorageCredentials cred = new StorageCredentials(config.StorageAccountName, config.StorageAccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(cred, true);

            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference(config.StorageAccountShareReference);
            share.CreateIfNotExists();


            string dirPath = Path.Combine(config.ResourcesRootPath, folderDateTime);

            CloudFileDirectory root = share.GetRootDirectoryReference();
            CloudFileDirectory dir = root.GetDirectoryReference(dirPath);
            dir.CreateIfNotExists();

            CloudFile cloudFile = dir.GetFileReference("Resources.xml");

            using (MemoryStream stream = new MemoryStream())
            {
                XmlWriterSettings xws = new XmlWriterSettings
                {
                    OmitXmlDeclaration = false,
                    Indent = true
                };

                using (XmlWriter xw = XmlWriter.Create(stream, xws))
                {
                    doc.WriteTo(xw);
                }

                stream.Position = 0;
                cloudFile.UploadFromStream(stream);
            }
        }

        public static bool ZipDocumentAndUploadToAzure(XmlDocumentType xmlDocumentType, XDocument doc, Configuration config, string dateTimeStamp, IDictionary<string, byte[]> files = null)
        {


            if (IsNullOrEmpty(config.StorageAccountName) ||
                IsNullOrEmpty(config.StorageAccountKey) ||
                IsNullOrEmpty(config.StorageAccountShareReference) ||
                IsNullOrEmpty(config.StorageAccountCatalogDirectoryReference) ||
                IsNullOrEmpty(config.StorageAccountResourcesDirectoryReference))
            {
                return false;
            }

            StorageCredentials cred = new StorageCredentials(config.StorageAccountName, config.StorageAccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(cred, true);

            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference(config.StorageAccountShareReference);
            share.CreateIfNotExists();

            CloudFileDirectory root = share.GetRootDirectoryReference();
            CloudFileDirectory dir = root.GetDirectoryReference(config.GetAzureStorageDirectoryName(xmlDocumentType));
            dir.CreateIfNotExists();

            CloudFile cloudFile = dir.GetFileReference(GetZipFileName(config, xmlDocumentType, dateTimeStamp));

            using (MemoryStream stream = new MemoryStream())
            {
                XmlWriterSettings xws = new XmlWriterSettings
                {
                    OmitXmlDeclaration = false,
                    Indent = true
                };

                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {

                    if (files != null)
                    {
                        foreach (KeyValuePair<string, byte[]> imageFile in files)
                        {
                            ZipArchiveEntry zipEntry = archive.CreateEntry(imageFile.Key);
                            using (Stream entryStream = zipEntry.Open())
                            {
                                entryStream.Write(imageFile.Value, 0, imageFile.Value.Length);
                            }
                        }
                    }

                    ZipArchiveEntry entry = archive.CreateEntry("catalog.xml");
                    using (Stream entryStream = entry.Open())
                    {
                        using (XmlWriter xw = XmlWriter.Create(entryStream, xws))
                        {
                            doc.WriteTo(xw);
                        }
                    }
                }
                stream.Position = 0;
                cloudFile.UploadFromStream(stream);

                switch (xmlDocumentType)
                {
                    case XmlDocumentType.Catalog:
                        config.CatalogPathInCloud = cloudFile.Name;
                        break;
                    default:
                        config.ResourceNameInCloud = cloudFile.Name;
                        break;
                }
                return true;
            }
        }

        public IEnumerable<string> UploadResourcesAndDocumentToAzure(List<StructureEntity> channelEntities, XDocument resourceDocument, Configuration config, string folderDateTime, inRiverContext context)
        {
            try
            {

                List<int> resourceIds = new List<int>();
                foreach (StructureEntity structureEntity in channelEntities)
                {
                    if (structureEntity.Type == "Resource" && !resourceIds.Contains(structureEntity.EntityId))
                    {
                        resourceIds.Add(structureEntity.EntityId);
                    }
                }

                context.Log(LogLevel.Debug, $"channel entities count {channelEntities.Count} in UploadResources to azure");

                List<Entity> resources =
                    context.ExtensionManager.DataService.GetEntities(resourceIds, LoadLevel.DataAndLinks);

                context.Log(LogLevel.Debug, $"resources count {resources.Count} after fetching them before passing them to UploadResourceFiles to azure");

                return UploadResourceFilesToAzureFileStorage(resources, resourceDocument, config, folderDateTime, context);
            }
            catch (Exception ex)
            {
                context.Log(LogLevel.Error, "Could not add resources", ex);
                return null;
            }

        }

        internal IEnumerable<string> UploadResourceFilesToAzureFileStorage(List<Entity> resources, XDocument importXml, Configuration config, string folderDateTime, inRiverContext context)
        {
            Stopwatch saveFileStopWatch = new Stopwatch();

            List<string> cloudFileNames = new List<string>();

            try
            {
                // validate configuration settings
                if (string.IsNullOrEmpty(config.StorageAccountName) ||
                    string.IsNullOrEmpty(config.StorageAccountKey) ||
                    string.IsNullOrEmpty(config.StorageAccountShareReference) ||
                    string.IsNullOrEmpty(config.StorageAccountCatalogDirectoryReference) ||
                    string.IsNullOrEmpty(config.StorageAccountResourcesDirectoryReference))
                {
                    context.Log(LogLevel.Warning, $"Azure config settings are invalid: " +
                        $"StorageAccountName: {config.StorageAccountName}, " +
                        $"StorageAccountKey: {config.StorageAccountKey}, " +
                        $"StorageAccountShareReference: {config.StorageAccountShareReference}, " +
                        $"StorageAccountCatalogDirectoryReference: {config.StorageAccountCatalogDirectoryReference}, " +
                        $"StorageAccountResourcesDirectoryReference: {config.StorageAccountResourcesDirectoryReference}");

                    return cloudFileNames;
                }

                // validate resources argument
                if (resources == null)
                {
                    context.Log(LogLevel.Error, "Resource is null!");
                    return cloudFileNames;
                }

                // create varible files to hold filename and binary
                Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

                // setup credentials and storage account
                StorageCredentials credentials = new StorageCredentials(config.StorageAccountName, config.StorageAccountKey);
                CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, true);

                // setup file client and remoge share
                CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
                CloudFileShare share = fileClient.GetShareReference(config.StorageAccountShareReference);
                share.CreateIfNotExists();

                // setup root directory and resource directory
                CloudFileDirectory root = share.GetRootDirectoryReference();
                CloudFileDirectory directory = root.GetDirectoryReference(config.GetAzureStorageDirectoryName(XmlDocumentType.Resources));
                directory.CreateIfNotExists();

                saveFileStopWatch.Start();

                // setup resource helper object
                Resources resourcesObj = new Resources(context);

                // setup the uncompressed file size total counter
                int totalFileSize = 0;

                // setup the azure file counter
                int fileCount = 0;

                // setup the file id list
                List<int> fileIdList = new List<int>();

                foreach (Entity resource in resources)
                {
                    // get the file id of the resource
                    int resourceFileId = resourcesObj.GetResourceFileId(resource);

                    // ensure the resource id has a proper id 
                    if (resourceFileId < 0)
                    {
                        context.Log(LogLevel.Information, $"Resource with id:{resource.Id} has no value for ResourceFileId");
                        continue;
                    }

                    // loop through each display configurations
                    foreach (string displayConfiguration in resourcesObj.GetDisplayConfigurations(resource, config))
                    {
                        // setup the fileName to use the output file extension from display configuration
                        string fileName = resourcesObj.GetResourceFileName(resource, resourceFileId, displayConfiguration, config);

                        // if the file for some reason already exists, continue to the next display configuration
                        if (files.ContainsKey(fileName))
                        {
                            context.Log(LogLevel.Debug, $"{fileName} already exists in the files collection and is skipped");

                            continue;
                        }

                        // get file bytes
                        byte[] resourceData = context.ExtensionManager.UtilityService.GetFile(resourceFileId, displayConfiguration);

                        // make sure we recieved the file from the utility service
                        if (resourceData == null)
                        {
                            context.Log(LogLevel.Error, $"Resource with id:{resource.Id} and ResourceFileId: {resourceFileId} could not get file");
                            continue;
                        }

                        // add the current resource file id to the list
                        fileIdList.Add(resource.Id);

                        // log the resource file name
                        context.Log(LogLevel.Debug, $"Adding resource {displayConfiguration}/{fileName}");

                        // add the file to the files collection
                        files.Add($"{displayConfiguration}/{fileName}", resourceData);

                        // add size to total file size counter
                        totalFileSize += resourceData.Length;

                    }

                    if (totalFileSize > (config.TotalResourceSizeLimitMb * 1024 * 1024))
                    {
                        try
                        {
                            // increase file counter
                            fileCount++;

                            // setup remote zip file
                            CloudFile cloudFile = directory.GetFileReference(GetZipFileName(config, XmlDocumentType.Resources, folderDateTime, fileCount));

                            // setup reader
                            using (XmlReader reader = importXml.CreateReader())
                            {
                                // create a new file that only contains the elements specified in the file id list
                                XmlDocument doc = XmlSplitUtility.GetPartialResourcesXml(reader, fileIdList);

                                // log the resource file name
                                context.Log(LogLevel.Debug, "Adding Resources.xml");

                                // setup memory a stream
                                using (MemoryStream stream = new MemoryStream())
                                {
                                    // create a xml writer to format output
                                    using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true }))
                                    {
                                        // save partial document to the xml writer 
                                        doc.Save(writer);

                                        // add the partial document to the files collection
                                        files.Add("Resources.xml", stream.ToArray());
                                    }
                                }

                                // send the zipped file to Azure and store the file name
                                cloudFileNames.Add(ZipAndUploadToCloud(config, context, files, directory, cloudFile));

                                // clear the id list
                                fileIdList.Clear();

                                // clear the file list
                                files.Clear();

                                // reset total file size
                                totalFileSize = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Log(LogLevel.Error, "An error occured while sending the resources to the cloud.", ex);
                        }
                    }
                }

                // make sure to send the final files
                if (files.Any())
                {
                    try
                    {
                        // increase file counter
                        fileCount++;

                        // setup remote zip file
                        CloudFile cloudFile = directory.GetFileReference(GetZipFileName(config, XmlDocumentType.Resources, folderDateTime, fileCount));

                        // setup reader
                        using (XmlReader reader = importXml.CreateReader())
                        {
                            // create a new file that only contains the elements specified in the file id list
                            XmlDocument doc = XmlSplitUtility.GetPartialResourcesXml(reader, fileIdList);

                            // log the resource file name
                            context.Log(LogLevel.Debug, "Adding Resources.xml");

                            // setup memory a stream
                            using (MemoryStream stream = new MemoryStream())
                            {
                                // create a xml writer to format output
                                using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true }))
                                {
                                    // save partial document to the xml writer 
                                    doc.Save(writer);

                                    // add the partial document to the files collection
                                    files.Add("Resources.xml", stream.ToArray());
                                }
                            }

                            // send the zipped file to Azure and store the file name
                            cloudFileNames.Add(ZipAndUploadToCloud(config, context, files, directory, cloudFile));
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Log(LogLevel.Error, "An error occured while sending the resources to the cloud.", ex);
                    }
                }

            }
            catch (Exception ex)
            {
                context.Log(LogLevel.Error, "An error occured while sending the resources to the cloud.", ex);
            }

            return cloudFileNames;
        }

        internal string ZipAndUploadToCloud(Configuration config, inRiverContext context, Dictionary<string, byte[]> files, CloudFileDirectory cloudDirectory, CloudFile cloudFile)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    foreach (KeyValuePair<string, byte[]> imageFile in files)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(imageFile.Key);
                        using (Stream entryStream = entry.Open())
                        {
                            entryStream.Write(imageFile.Value, 0, imageFile.Value.Length);
                        }
                    }
                }

                stream.Position = 0;
                cloudFile.UploadFromStream(stream);

                Uri path = cloudFile.Uri;
                config.ResourceNameInCloud = cloudFile.Name;
                context.Log(LogLevel.Debug, $"done uploading resource files to the file storage in {cloudDirectory} and this is it's uri {path}");

                return path.ToString();
            }
        }

        public static string SaveAndZipDocument(string channelIdentifier, XDocument doc, string folderDateTime, Configuration config, inRiverContext context)
        {
            string dirPath = Path.Combine(config.PublicationsRootPath, folderDateTime);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            try
            {
                XDocument verified = VerifyAndCorrectDocument(doc, context);
                doc = verified;
            }
            catch (Exception exception)
            {
                context.Log(LogLevel.Error, "Fail to verify the document: ", exception);
            }

            string filePath = Path.Combine(dirPath, Configuration.ExportFileName);
            context.Log(
                LogLevel.Information,
                string.Format("Saving verified document to path {0} for channel:{1}", filePath, channelIdentifier));
            doc.Save(filePath);
            string fullZippedFileName = string.Format(
                "inRiverExport_{0}_{1}.zip",
                channelIdentifier,
                DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            ZipFile(filePath, fullZippedFileName);

            return fullZippedFileName;
        }

        public static void ZipDirectoryAndSubdirectory(string zippedFileName, Configuration configuration, inRiverContext context)
        {
            context.Log(LogLevel.Debug, "Zipping resource directory");
            Stopwatch dirZipStopWatch = new Stopwatch();
            try
            {
                dirZipStopWatch.Start();
                string directoryToZip = Path.Combine(configuration.ResourcesRootPath, "temp");
                DirectoryInfo directoryInfo = new DirectoryInfo(directoryToZip);
                string fullZippedFileName = Path.Combine(configuration.ResourcesRootPath, zippedFileName);
                using (Package zip = Package.Open(fullZippedFileName, FileMode.Create))
                {
                    foreach (DirectoryInfo di in directoryInfo.GetDirectories())
                    {
                        foreach (FileInfo fi in di.GetFiles())
                        {
                            string destFilename = string.Format(".\\{0}\\{1}", di.Name, fi.Name);
                            ZipFile(zip, fi, destFilename);
                        }
                    }

                    foreach (FileInfo fi in directoryInfo.GetFiles())
                    {
                        if (fi.Name.Equals(zippedFileName))
                        {
                            continue;
                        }

                        string destFilename = string.Format(".\\{0}", fi.Name);
                        ZipFile(zip, fi, destFilename);
                    }
                }

                dirZipStopWatch.Stop();
            }
            catch (Exception ex)
            {
                context.Log(LogLevel.Error, "Exception in ZipDirectoryAndSubdirectoryAndRemoveFiles", ex);
            }
            BusinessHelper businessHelper = new BusinessHelper(context);
            context.Log(
                LogLevel.Information,
                string.Format("Resource directory zipped, took {0}!", businessHelper.GetElapsedTimeFormated(dirZipStopWatch)));
        }

        public static void ZipFile(Package zip, FileInfo fi, string destFilename)
        {
            Uri uri = PackUriHelper.CreatePartUri(new Uri(destFilename, UriKind.Relative));
            if (zip.PartExists(uri))
            {
                zip.DeletePart(uri);
            }

            PackagePart part = zip.CreatePart(uri, string.Empty, CompressionOption.Normal);
            using (FileStream fileStream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
            {
                if (part != null)
                {
                    using (Stream dest = part.GetStream())
                    {
                        CopyStream(fileStream, dest);
                    }
                }
            }
        }

        public static string GetZipFileName(Configuration config, XmlDocumentType xmlDocumentType, string dateTimeStamp)
        {

            string zipFileWithTimeStamp = $"{config.GetAzureFileName(xmlDocumentType)}.{dateTimeStamp}.zip";

            return zipFileWithTimeStamp;
        }

        public static string GetZipFileName(Configuration config, XmlDocumentType xmlDocumentType, string dateTimeStamp, int fileCount)
        {

            string zipFileWithTimeStamp = $"{config.GetAzureFileName(xmlDocumentType)}.{dateTimeStamp}.{fileCount}.zip";

            return zipFileWithTimeStamp;
        }

        public static void CopyStream(FileStream inputStream, Stream outputStream)
        {
            const long MaxbuffertSize = 4096;
            long bufferSize = inputStream.Length < MaxbuffertSize ? inputStream.Length : MaxbuffertSize;
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }
        }

        public static void ZipFile(string fileToZip, string zippedFileName)
        {
            string path = Path.GetDirectoryName(fileToZip);
            if (path != null)
            {
                using (Package zip = Package.Open(Path.Combine(path, zippedFileName), FileMode.Create))
                {
                    string destFilename = ".\\" + Path.GetFileName(fileToZip);
                    ZipFile(zip, new FileInfo(fileToZip), destFilename);
                }
            }
        }

        private static XDocument VerifyAndCorrectDocument(XDocument doc, inRiverContext context)
        {
            List<string> unwantedEntityTypes = CreateUnwantedEntityTypeList(context);
            XDocument result = new XDocument(doc);
            XElement root = result.Root;
            if (root == null)
            {
                throw new Exception("Can't verify the Catalog.cml as it's empty.");
            }

            IEnumerable<XElement> entryElements = root.Descendants("Entry");
            List<string> codesToBeRemoved = new List<string>();
            foreach (XElement entryElement in entryElements)
            {
                string code = entryElement.Elements("Code").First().Value;
                string metaClassName =
                    entryElement.Elements("MetaData").Elements("MetaClass").Elements("Name").First().Value;

                if (unwantedEntityTypes.Contains(metaClassName))
                {
                    context.Log(
                        LogLevel.Debug,
                        string.Format("Code {0} will be removed as it has wrong metaclass name ({1})", code, metaClassName));
                    codesToBeRemoved.Add(code);
                }
            }

            foreach (string code in codesToBeRemoved)
            {
                string theCode = code;
                root.Descendants("Entry").Where(
                    e =>
                    {
                        XElement codeElement = e.Element("Code");
                        return codeElement != null && codeElement.Value == theCode;
                    }).Remove();
            }

            return result;
        }

        private static List<string> CreateUnwantedEntityTypeList(inRiverContext context)
        {
            List<string> typeIds = new List<string>
                                       {
                                           "Channel",
                                           "Assortment",
                                           "Resource",
                                           "Task",
                                           "Section",
                                           "Publication"
                                       };
            List<string> result = new List<string>();
            foreach (string typeId in typeIds)
            {
                List<FieldSet> fieldSets = context.ExtensionManager.ModelService.GetFieldSetsForEntityType(typeId);
                if (!fieldSets.Any())
                {
                    if (!result.Contains(typeId))
                    {
                        result.Add(typeId);
                    }

                    continue;
                }

                foreach (FieldSet fieldSet in fieldSets)
                {
                    string value = string.Format("{0}_{1}", typeId, fieldSet.Id);
                    if (!result.Contains(value))
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }





    }
}