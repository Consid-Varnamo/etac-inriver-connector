using inRiver.Remoting.Extension.Fakes;
using inRiver.Remoting.Fakes;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace inRiver.Connectors.EPiServer.Communication.Tests
{
    [TestClass()]
    public class EpiApiTests
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

        [TestMethod()]
        public void StartImportIntoEpiServerCommercePublishTest()
        {
            StartImportIntoEpiServerCommerceTest("catalog.20200207-154419.402.zip", InteSettings);

        }

        private void StartImportIntoEpiServerCommerceTest(string fileNameInCloud, Dictionary<string, string> settings)
        {
            using (ShimsContext.Create())
            {
                //Arrange
                ShiminRiverContext.AllInstances.SettingsGet = settingsGet => settings;

                ShiminRiverContext.AllInstances.ExtensionManagerGet = extensionManager => new StubIinRiverManager()
                {
                    ModelServiceGet = () =>
                    {
                        return new StubIModelService()
                        {
                            GetAllLinkTypes = () => new List<Remoting.Objects.LinkType>(),
                        };
                    }
                };

                ShiminRiverContext.AllInstances.LogLogLevelString = (cx, logLevel, message) =>
                {
                    Trace.WriteLine($"{logLevel.ToString().ToUpper().PadRight(12, ' ')} {message}");
                };

                ShiminRiverContext context = new ShiminRiverContext();

                //Act
                EpiApi api = new EpiApi(context);
                Configuration configuration = new Configuration(context);

                api.StartImportIntoEpiServerCommerce(fileNameInCloud, Guid.NewGuid(), configuration);

                //Assert
            }
        }
    }
}