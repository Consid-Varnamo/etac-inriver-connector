using System.Collections.Generic;
using inRiver.EPiServerCommerce.CommerceAdapter;
using inRiver.Remoting;
using inRiver.Remoting.Extension;

namespace Tester
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var importer = new XmlExporter();
            importer.Context = new inRiverContext(
                RemoteManager.CreateInstance("http://localhost:8080", "pimuser1@testcustomer3.com", "Pimuser1!", "PROD"), 
                new ConsoleLogger());

            importer.Context.Settings = new Dictionary<string, string>
            {
                {"CHANNEL_ID", "15880"},
                {"EPI_APIKEY", "epi123"},
                {"EPI_ENDPOINT_URL", "http://localhost:64010/inriverapi/InriverDataImport/"},
                {"RESOURCE_CONFIGURATION", "Thumbnail"},
                {"PUBLISH_FOLDER", "c:\\temp"},
                {"PUBLISH_FOLDER_RESOURCES", "c:\\temp"},
                {"EPI_RESTTIMEOUT", "1"},
                {"ITEM_TO_SKUS", "false"},
                {"LANGUAGE_MAPPING", "<languages><language><epi>en-us</epi><inriver>en</inriver></language></languages>"},
                {"STORAGE_NAME", "epifileshare"},
                {"STORAGE_KEY","AddKeyHere" },
                {"STORAGE_SHARE_REFERENCE", "myshare"},
                {"STORAGE_CATALOG_DIRECTORY_REFERENCE", "catalog"},
                {"STORAGE_RESOURCES_DIRECTORY_REFERENCE", "resource"}
            };


            importer.Publish(15880);
        }
    }
}
