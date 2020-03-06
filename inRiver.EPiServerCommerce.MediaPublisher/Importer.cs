#region Generated Code

using inRiver.Remoting;
using inRiver.Remoting.Extension;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.File;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LogLevel = inRiver.Remoting.Log.LogLevel;

namespace inRiver.EPiServerCommerce.MediaPublisher
{
    #endregion
    using inRiver.EPiServerCommerce.Interfaces;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Xml;
    using System.Xml.Serialization;

    public class Importer : IResourceImport
    {
        private Dictionary<string, string> settings = new Dictionary<string, string>();

        private readonly inRiverContext context;

        public Importer(inRiverContext context)
        {
            this.context = context;
        }

        public bool ImportResources(string fileNameInCloud, string baseResourcePath, string id)
        {
            context.Log(LogLevel.Information,
                string.Format("Starting Resource Import. Manifest: {0} BaseResourcePath: {1}", fileNameInCloud, baseResourcePath));

            // Get custom setting
            this.settings = context.Settings;

            // Check if ENABLE_EPI_ENDPOINT is set, and set to false
            bool enableEndPoint = true;
            if (this.settings.ContainsKey("ENABLE_EPI_ENDPOINT"))
            {
                string valueEnableEPIEndpoint = settings["ENABLE_EPI_ENDPOINT"];
                if (!string.IsNullOrEmpty(valueEnableEPIEndpoint))
                {
                    enableEndPoint = bool.Parse(valueEnableEPIEndpoint);
                }
            }
            if (!enableEndPoint)
            {
                context.Log(LogLevel.Information, "EPI Endpoint Disabled under Configuration");
                return true;
            }

            string apikey;
            if (this.settings.ContainsKey("EPI_APIKEY"))
            {
                apikey = this.settings["EPI_APIKEY"];
            }
            else
            {
                throw new ConfigurationErrorsException("Missing EPI_APIKEY setting on connector. It needs to be defined to else the calls will fail. Please see the documentation.");
            }

            int timeout;
            if (this.settings.ContainsKey("EPI_RESTTIMEOUT"))
            {
                string timeoutString = this.settings["EPI_RESTTIMEOUT"];
                if (!int.TryParse(timeoutString, out timeout))
                {
                    throw new ConfigurationErrorsException("Can't parse EPI_RESTTIMEOUT : " + timeoutString);
                }
            }
            else
            {
                throw new ConfigurationErrorsException("Missing EPI_RESTTIMEOUT setting on connector. It needs to be defined to else the calls will fail. Please see the documentation.");
            }

            if (this.settings.ContainsKey("EPI_ENDPOINT_URL") == false)
            {
                throw new ConfigurationErrorsException("Missing EPI_ENDPOINT_URL setting on connector. It should point to the import end point on the EPiServer Commerce web site. Please see the documentation.");
            }

            string endpointAddress = this.settings["EPI_ENDPOINT_URL"];
            if (string.IsNullOrEmpty(endpointAddress))
            {
                throw new ConfigurationErrorsException("Missing EPI_ENDPOINT_URL setting on connector. It should point to the import end point on the EPiServer Commerce web site. Please see the documentation.");
            }

            if (endpointAddress.EndsWith("/") == false)
            {
                endpointAddress = endpointAddress + "/";
            }

            // Name of resource import controller method
            endpointAddress = endpointAddress + "ImportResources";

            return this.ImportResourcesToEPiServerCommerce(fileNameInCloud, baseResourcePath, endpointAddress, apikey, timeout);
        }

        public bool ImportResourcesToEPiServerCommerce(string fileNameInCloud, string baseResourcePath, string endpointAddress, string apikey, int timeout)
        {
            var serializer = new XmlSerializer(typeof(Resources));
            Resources resources;

            var xml = GetResourceXMLFromCloudShare(context.Settings, fileNameInCloud);
            using (var streamReader = new StreamReader(xml))
            {
                using (var reader = XmlReader.Create(streamReader.BaseStream))
                {
                    resources = (Resources)serializer.Deserialize(reader);
                }
            }

            List<InRiverImportResource> resourcesForImport = new List<InRiverImportResource>();
            foreach (var resource in resources.ResourceFiles.Resource)
            {
                InRiverImportResource newRes = new InRiverImportResource();
                newRes.Action = resource.action;
                newRes.Codes = new List<string>();
                if (resource.ParentEntries != null && resource.ParentEntries.EntryCode != null)
                {
                    foreach (EntryCode entryCode in resource.ParentEntries.EntryCode)
                    {
                        if (!string.IsNullOrEmpty(entryCode.Value))
                        {
                            newRes.Codes = new List<string>();

                            newRes.Codes.Add(entryCode.Value);
                            newRes.EntryCodes.Add(new Interfaces.EntryCode()
                            {
                                Code = entryCode.Value,
                                SortOrder = entryCode.SortOrder
                            });
                        }
                    }
                }

                if (resource.action != "deleted")
                {
                    newRes.MetaFields = this.GenerateMetaFields(resource);

                    // path is ".\some file.ext"
                    if (resource.Paths != null && resource.Paths.Path != null)
                    {
                        string filePath = resource.Paths.Path.Value.Remove(0, 1);
                        filePath = filePath.Replace("/", "\\");
                        newRes.Path = filePath;
                    }
                }

                newRes.ResourceId = resource.id;
                resourcesForImport.Add(newRes);
            }

            if (resourcesForImport.Count == 0)
            {
                context.Log(LogLevel.Debug, string.Format("Nothing to tell server about."));
                return true;
            }

            Uri importEndpoint = new Uri(endpointAddress);
            return PostResourceDataToImporterEndPoint(fileNameInCloud, importEndpoint, resourcesForImport, apikey, timeout);
        }

        private Stream GetResourceXMLFromCloudShare(Dictionary<string, string> config, string fileNameInCloud)
        {
            var cred = new StorageCredentials(config["STORAGE_NAME"], config["STORAGE_KEY"]);
            var storageAccount = new CloudStorageAccount(cred, true);

            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference(config["STORAGE_SHARE_REFERENCE"]);
            share.CreateIfNotExists();

            CloudFileDirectory root = share.GetRootDirectoryReference();
            CloudFileDirectory dir = root.GetDirectoryReference(config["STORAGE_RESOURCES_DIRECTORY_REFERENCE"]);

            var cloudFile = dir.GetFileReference(fileNameInCloud);

            var ms = new MemoryStream();
            cloudFile.DownloadToStream(ms);

            ms.Position = 0;

            ZipArchive archive = new ZipArchive(ms);
            var xml = archive.Entries.Last().Open();
            return xml;
        }

        private List<ResourceMetaField> GenerateMetaFields(Resource resource)
        {
            List<ResourceMetaField> metaFields = new List<ResourceMetaField>();
            if (resource.ResourceFields != null)
            {
                foreach (MetaField metaField in resource.ResourceFields.MetaField)
                {
                    ResourceMetaField resourceMetaField = new ResourceMetaField { Id = metaField.Name.Value };
                    List<Value> values = new List<Value>();
                    foreach (Data data in metaField.Data)
                    {
                        Value value = new Value { Languagecode = data.language };
                        if (data.Item != null && data.Item.Count > 0)
                        {
                            foreach (Item item in data.Item)
                            {
                                value.Data += item.value + ";";
                            }

                            int lastIndexOf = value.Data.LastIndexOf(';');
                            if (lastIndexOf != -1)
                            {
                                value.Data = value.Data.Remove(lastIndexOf);
                            }
                        }
                        else
                        {
                            value.Data = data.value;
                        }

                        values.Add(value);
                    }

                    resourceMetaField.Values = values;

                    metaFields.Add(resourceMetaField);
                }
            }

            return metaFields;
        }

        /// <summary>
        /// </summary>
        /// <param name="fileNameInCloud"></param>
        /// <param name="importEndpoint">// http://server:port/inriverapi/InriverDataImport/ImportImages</param>
        /// <param name="resourcesForImport"></param>
        /// <param name="apikey"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private bool PostResourceDataToImporterEndPoint(string fileNameInCloud, Uri importEndpoint, List<InRiverImportResource> resourcesForImport, string apikey, int timeout)
        {
            List<List<InRiverImportResource>> listofLists = new List<List<InRiverImportResource>>();
            int maxSize = 1000;
            for (int i = 0; i < resourcesForImport.Count; i += maxSize)
            {
                listofLists.Add(resourcesForImport.GetRange(i, Math.Min(maxSize, resourcesForImport.Count - i)));
            }

            foreach (List<InRiverImportResource> resources in listofLists)
            {
                HttpClient client = new HttpClient();
                string baseUrl = importEndpoint.Scheme + "://" + importEndpoint.Authority;

                context.Log(LogLevel.Debug, $"Sending {resources.Count} of {resourcesForImport.Count} resources from {fileNameInCloud} to {importEndpoint}");
                client.BaseAddress = new Uri(baseUrl);

                // Add an Accept header for JSON format.
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("apikey", apikey);

                client.Timeout = new TimeSpan(timeout, 0, 0);
                var data = new InRiverImportResourceInformation(fileNameInCloud, resources);
                HttpResponseMessage response = client.PostAsJsonAsync(importEndpoint.PathAndQuery, data).Result;
                if (response.IsSuccessStatusCode)
                {
                    // Parse the response body. Blocking!
                    var result = response.Content.ReadAsAsync<bool>().Result;
                    if (result)
                    {
                        string resp = this.Get(apikey, timeout);

                        int tries = 0;
                        while (resp == "importing")
                        {
                            tries++;
                            if (tries < 10)
                            {
                                Thread.Sleep(5000);
                            }
                            else if (tries < 30)
                            {
                                Thread.Sleep(60000);
                            }
                            else
                            {
                                Thread.Sleep(600000);
                            }

                            resp = this.Get(apikey, timeout);
                        }

                        if (resp.StartsWith("ERROR"))
                        {
                            context.Log(LogLevel.Error, resp);
                            return false;
                        }
                    }
                }
                else
                {
                    context.Log(
                        LogLevel.Error,
                        $"Import failed: {(int)response.StatusCode} ({response.ReasonPhrase}) {importEndpoint}");
                    return false;
                }
            }

            return true;
        }

        private string Get(string apikey, int timeout)
        {
            string endpointAddress = this.settings["EPI_ENDPOINT_URL"];
            if (string.IsNullOrEmpty(endpointAddress))
            {
                throw new ConfigurationErrorsException("Missing EPI_ENDPOINT_URL setting on connector. It should point to the import end point on the EPiServer Commerce web site. Please see the documentation.");
            }

            if (endpointAddress.EndsWith("/") == false)
            {
                endpointAddress = endpointAddress + "/";
            }

            // Name of resource import controller method
            endpointAddress = endpointAddress + "IsImporting";

            Uri uri = new Uri(endpointAddress);

            HttpClient client = new HttpClient();
            string baseUrl = uri.Scheme + "://" + uri.Authority;

            client.BaseAddress = new Uri(baseUrl);

            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apikey", apikey);

            // HttpResponseMessage response = client.GetAsync("").Result;  // Blocking call!
            client.Timeout = new TimeSpan(timeout, 0, 0);
            HttpResponseMessage response = client.GetAsync(uri.PathAndQuery).Result;

            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                string resp = response.Content.ReadAsAsync<string>().Result;

                return resp;
            }

            string errorMsg = $"Import failed: {(int)response.StatusCode} ({response.ReasonPhrase}) {uri}";
            context.Log(LogLevel.Error, errorMsg);
            throw new HttpRequestException(errorMsg);
        }

        private static RemoteManager CreateRemoteManager(string serverUrl, string username, string password, string environment)
        {
            var ticket = new RemoteManager(serverUrl).Authenticate(serverUrl, username, password, environment);

            if (ticket == null)
            {
                Console.WriteLine("Could not create a Remote Manager ticket.");
                return null;
            }

            var manager = RemoteManager.CreateInstance(serverUrl, ticket);
            if (manager == null)
            {
                Console.WriteLine("Error when creating Remote Manager instance");
                return null;
            }

            return manager;
        }
    }

}
