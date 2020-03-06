using inRiver.Remoting.Extension.Fakes;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;

namespace inRiver.EPiServerCommerce.MediaPublisher.Tests
{
    [TestClass()]
    public class ImporterTests
    {
        private Dictionary<string, string> InteSettings => new Dictionary<string, string>
                {
                    { "EPI_APIKEY", "__InRiver.ApiKey__" },
                    { "EPI_RESTTIMEOUT", "60" },
                    { "EPI_ENDPOINT_URL", "http://en.etac.local/InriverApi/InriverDataImport" },
                    { "STORAGE_NAME", "etacpimfileshare01vk4d3" },
                    { "STORAGE_KEY", "a7OAK0PoDYMDsONSEk/fm+U/IMKKC/OuiNKWHrTlOvBdBciF8XIJhtdVQhrDhXfFyoHS6kC7VYm2w6/msRV5fA==" },
                    { "STORAGE_SHARE_REFERENCE", "etac02mstrxu662inte" },
                    { "STORAGE_CATALOG_DIRECTORY_REFERENCE", "catalog" },
                    { "STORAGE_RESOURCES_DIRECTORY_REFERENCE", "resource" },
                };
        private Dictionary<string, string> PrepSettings => new Dictionary<string, string>
                {
                    { "EPI_APIKEY", "__InRiver.ApiKey__" },
                    { "EPI_RESTTIMEOUT", "60" },
                    { "EPI_ENDPOINT_URL", "http://en.etac.local/InriverApi/InriverDataImport" },
                    { "STORAGE_NAME", "etacpimfileshare01vk4d3" },
                    { "STORAGE_KEY", "a7OAK0PoDYMDsONSEk/fm+U/IMKKC/OuiNKWHrTlOvBdBciF8XIJhtdVQhrDhXfFyoHS6kC7VYm2w6/msRV5fA==" },
                    { "STORAGE_SHARE_REFERENCE", "etac02mstrxu662prep" },
                    { "STORAGE_CATALOG_DIRECTORY_REFERENCE", "catalog" },
                    { "STORAGE_RESOURCES_DIRECTORY_REFERENCE", "resource" },
                };

        public TestContext TestContext { get; set; }

        [TestMethod()]
        public void ImportResourcesPublishTest()
        {
            string[] packages = new[]
            {
                "resources_.20200207-154419.402.1.zip",
                "resources_.20200207-154419.402.2.zip",
                "resources_.20200207-154419.402.3.zip",
                "resources_.20200207-154419.402.4.zip",
                "resources_.20200207-154419.402.5.zip",
                "resources_.20200207-154419.402.6.zip",
                "resources_.20200207-154419.402.7.zip",
            };
            ImportResourcesTest(packages, InteSettings);
        }

        [TestMethod()]
        public void ImportSingleResourcePublishTest()
        {
            string fileNameInCloud;

            //Preview\Molift-mover-300-environmental-2_550037.jpg

            //fileNameInCloud = "1.resources_.20200113-154125.958-unlinked.zip";
            //fileNameInCloud = "2.resources_.20200113-154125.974-deleted.zip";
            fileNameInCloud = "3.resources_.20200113-154137.661-added.zip";
            //fileNameInCloud = "4.resources_.20200113-154125.958-edited-unlinked.zip";
            //fileNameInCloud = "5.resources_.20200113-154125.974-edited-deleted.zip";
            //fileNameInCloud = "6.resources_.20200113-154137.661-edited-added.zip";
            //fileNameInCloud = "7.resources_.20200110-141859.915-edited-updated.zip";
            ImportResourcesTest(fileNameInCloud, InteSettings);



        }

        private void ImportResourcesTest(string[] fileNamesInCloud, Dictionary<string, string> settings)
        {
            foreach (string fileNameInCloud in fileNamesInCloud)
            {
                ImportResourcesTest(fileNameInCloud, settings);
            }
        }

        private void ImportResourcesTest(string fileNameInCloud, Dictionary<string, string> settings)
        {
            Trace.WriteLine($"Processing {fileNameInCloud}.");

            string baseResourcePath = "resource";

            using (ShimsContext.Create())
            {
                ShiminRiverContext.AllInstances.SettingsGet = settingsGet => settings;

                ShiminRiverContext.AllInstances.LogLogLevelString = (cx, logLevel, message) =>
                {
                    Trace.WriteLine($"{logLevel.ToString().ToUpper().PadRight(12, ' ')} {message}");
                };

                ShiminRiverContext context = new ShiminRiverContext();

                //Act
                Importer importer = new Importer(context);
                importer.ImportResources(fileNameInCloud, baseResourcePath, string.Empty);

                //Assert
            }
        }
    }
}
