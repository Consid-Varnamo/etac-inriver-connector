using inRiver.Connectors.EPiServer.Communication;
using inRiver.Connectors.EPiServer.Enums;
using inRiver.Connectors.EPiServer.EpiXml;
using inRiver.Connectors.EPiServer.Helpers;
using inRiver.Remoting.Connect;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace inRiver.Connectors.EPiServer.Utilities
{
    public class AddUtility
    {
        private Configuration ConnectorConfig { get; set; }
        private readonly inRiverContext _context;
        private readonly EpiDocument _epiDocument;

        public AddUtility(Configuration connectorConfig, inRiverContext inRiverContext)
        {
            ConnectorConfig = connectorConfig;
            _context = inRiverContext;
            _epiDocument = new EpiDocument(inRiverContext, connectorConfig);
        }

        internal void Add(Entity channelEntity, ConnectorEvent connectorEvent, out bool resourceIncluded)
        {
            resourceIncluded = false;

            var channelHelper = new ChannelHelper(_context);
            var epiApi = new EpiApi(_context);
            var connectorEventHelper = new ConnectorEventHelper(_context);


            connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Generating catalog.xml...", 11);
            Dictionary<string, List<XElement>> epiElements = _epiDocument.GetEPiElements(ConnectorConfig);

            XDocument doc = _epiDocument.CreateImportDocument(
                channelEntity,
                null,
                null,
                epiElements,
                ConnectorConfig);

            string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");

            if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, doc, ConnectorConfig, folderDateTime))
            {
                _context.Log(LogLevel.Information, "Failed to zip and upload the catalog file to azure from add utility Add() method");
            }

            _context.Log(LogLevel.Information, "Catalog saved with the following:");
            _context.Log(LogLevel.Information, string.Format("Nodes: {0}", epiElements["Nodes"].Count));
            _context.Log(LogLevel.Information, string.Format("Entries: {0}", epiElements["Entries"].Count));
            _context.Log(LogLevel.Information, string.Format("Relations: {0}", epiElements["Relations"].Count));
            _context.Log(LogLevel.Information, string.Format("Associations: {0}", epiElements["Associations"].Count));
            connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Done generating catalog.xml", 25);

            connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Generating Resource.xml and saving files to disk...", 26);

            Resources resourceHelper = new Resources(_context);

            XDocument resourceDocument = resourceHelper.GetResourcesDocument(ConnectorConfig.ChannelStructureEntities, ConnectorConfig);

            // Add all files included in resource document
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

            IEnumerable<XElement> resourceFileElements = resourceDocument.Document.Element("Resources")?.Element("ResourceFiles")?.Elements("Resource");

            if (resourceFileElements != null && resourceFileElements.Any())
            {
                _context.Log(LogLevel.Information, $"Adding {resourceFileElements.Count()} resource files to zip archive.");
                foreach (XElement resourceFileElement in resourceFileElements)
                {
                    int resourceEntityId;
                    if (int.TryParse(resourceFileElement.Attribute("id").Value, out resourceEntityId))
                    {
                        Entity targetEntity = _context.ExtensionManager.DataService.GetEntity(resourceEntityId, LoadLevel.DataOnly);

                        _context.Log(LogLevel.Debug, $"Adding image file {targetEntity.DisplayName}({targetEntity.Id})");
                        int resourceFileId = resourceHelper.GetResourceFileId(targetEntity);

                        foreach (string displayConfig in resourceHelper.GetDisplayConfigurations(targetEntity, ConnectorConfig))
                        {
                            string fileName = resourceHelper.GetResourceFileName(targetEntity, resourceFileId, displayConfig, ConnectorConfig);

                            byte[] resourceData = _context.ExtensionManager.UtilityService.GetFile(resourceFileId, displayConfig);

                            if (resourceData != null)
                            {
                                files.Add($"{displayConfig}/{fileName}", resourceData);
                            }
                        }
                    }
                }
            }
            else
            {
                string elementCount = resourceFileElements == null ? "null" : resourceFileElements.Count().ToString();
                _context.Log(LogLevel.Information, $"No files linked to resource document. Document contains {elementCount} elements");
            }


            DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Resources, resourceDocument, ConnectorConfig, folderDateTime, files);

            connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Done generating/saving Resource.xml", 50);

            if (ConnectorConfig.ActivePublicationMode.Equals(PublicationMode.Automatic))
            {
                _context.Log(LogLevel.Debug, "Starting automatic import!");
                connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Sending Catalog.xml to EPiServer...", 51);
                if (epiApi.StartImportIntoEpiServerCommerce(ConnectorConfig.CatalogPathInCloud, channelHelper.GetChannelGuid(channelEntity, ConnectorConfig), ConnectorConfig))
                {
                    connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Done sending Catalog.xml to EPiServer", 75);
                }
                else
                {
                    connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Error while sending Catalog.xml to EPiServer", -1, true);

                    return;
                }

                connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Sending Resources to EPiServer...", 76);
                if (epiApi.StartAssetImportIntoEpiServerCommerce(ConnectorConfig.ResourceNameInCloud, Path.Combine(ConnectorConfig.ResourcesRootPath, folderDateTime), ConnectorConfig))
                {
                    connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Done sending Resources to EPiServer...", 99);
                    resourceIncluded = true;
                }
                else
                {
                    connectorEventHelper.UpdateConnectorEvent(connectorEvent, "Error while sending resources to EPiServer", -1, true);
                }
            }
        }

    }
}
