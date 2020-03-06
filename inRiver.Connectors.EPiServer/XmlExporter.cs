using inRiver.Connectors.EPiServer;
using inRiver.Connectors.EPiServer.Communication;
using inRiver.Connectors.EPiServer.Enums;
using inRiver.Connectors.EPiServer.EpiXml;
using inRiver.Connectors.EPiServer.Helpers;
using inRiver.Connectors.EPiServer.Utilities;
using inRiver.EPiServerCommerce.CommerceAdapter.Helpers;
using inRiver.EPiServerCommerce.Interfaces.Enums;
using inRiver.Remoting.Connect;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Extension.Interface;
using inRiver.Remoting.Objects;
using inRiver.Remoting.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LogLevel = inRiver.Remoting.Log.LogLevel;

namespace inRiver.EPiServerCommerce.CommerceAdapter
{


    public class XmlExporter : IChannelListener, ICVLListener
    {
        private Dictionary<string, string> _defaultSettings =
            new Dictionary<string, string>
            {
                {"CHANNEL_ID", ""},
                {"EPI_APIKEY", ""},
                {"EPI_ENDPOINT_URL", ""},
                {"RESOURCE_CONFIGURATION", "Thumbnail"},
                {"PUBLISH_FOLDER", ""},
                {"PUBLISH_FOLDER_RESOURCES", ""},
                {"EPI_RESTTIMEOUT", "1"},
                {"ITEM_TO_SKUS", "false"},
                {"LANGUAGE_MAPPING", "<languages><language><epi>en-us</epi><inriver>en</inriver></language></languages>"},
                {"STORAGE_NAME", ""},
                {"STORAGE_KEY","" },
                {"STORAGE_SHARE_REFERENCE", ""},
                {"STORAGE_CATALOG_DIRECTORY_REFERENCE", ""},
                {"STORAGE_RESOURCES_DIRECTORY_REFERENCE", ""},
                {"ENABLE_EPI_ENDPOINT", "true" },
                {"EXCLUDED_FIELD_CATEGORIES", "" },
            };

        private Configuration configuration;

        public Configuration Configuration
        {
            get
            {
                if (configuration == null)
                {
                    configuration = new Configuration(Context);
                }

                return configuration;
            }
        }


        public string Test()
        {
            Context.Log(LogLevel.Information, $"At least we have entered here. and the context object is null ? *{Context == null}*");

            Context.Log(LogLevel.Information, $"configuration object has been created. and the channel id is {Configuration.ChannelId}");

            //Publish(configuration.ChannelId);
            //return "Test method done. There should be zip file in azure storage";

            string infoAssembly = $"{Assembly.GetExecutingAssembly().GetName().Version.ToString()}";

            string testMessage = $"Test - Version={infoAssembly};Channel={Configuration.ChannelId};" +
                                 $"ChannelDefaultLanguage={Configuration.ChannelDefaultLanguage.Name};" +
                                 $"StorageAccountShareReference={Configuration.StorageAccountShareReference};" +
                                 $"StorageAccountCatalogDirectoryReference={Configuration.StorageAccountCatalogDirectoryReference};" +
                                 $"StorageAccountResourcesDirectoryReference={Configuration.StorageAccountResourcesDirectoryReference}";
            return testMessage;
        }

        public void Publish(int channelId)
        {
            if (channelId != Configuration.ChannelId)
            {
                Context.Log(LogLevel.Information, $"Publish on channel {channelId} was called but it's not the target for this listener (channel id {Configuration.ChannelId}). No action was made.");

                return;
            }

            ChannelHelper channelHelper = new ChannelHelper(Context);
            EpiDocument epiDocument = new EpiDocument(Context, Configuration);
            EpiApi epiApi = new EpiApi(Context);
            EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
            EpiElement epiElement = new EpiElement(Context);
            Resources resource = new Resources(Context);
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);
            ConnectorEvent publishConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.Publish, string.Format("Publish started for channel: {0}", channelId), 0);
            Context.Log(LogLevel.Information, $"connector event handler has been successfully created");

            Stopwatch publishStopWatch = new Stopwatch();
            bool resourceIncluded = false;

            try
            {
                publishStopWatch.Start();
                Entity channelEntity = InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Failed to initial publish. Could not find the channel.", -1, true);
                    return;
                }

                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Fetching all channel entities...", 1);
                List<StructureEntity> channelEntities = channelHelper.GetAllEntitiesInChannel(Configuration.ChannelId, Configuration.ExportEnabledEntityTypes);

                Configuration.ChannelStructureEntities = channelEntities;
                channelHelper.BuildEntityIdAndTypeDict(Configuration);

                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Done fetching all channel entities", 10);

                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Generating catalog.xml...", 11);
                Dictionary<string, List<XElement>> epiElements = epiDocument.GetEPiElements(Configuration);

                XDocument doc = epiDocument.CreateImportDocument(channelEntity, epiElement.GetMetaClassesFromFieldSets(Configuration), epiDocument.GetAssociationTypes(Configuration), epiElements, Configuration);
                string channelIdentifier = channelHelper.GetChannelIdentifier(channelEntity);

                string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");


                if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, doc, Configuration, folderDateTime))
                {
                    Context.Log(LogLevel.Information, "Failed to zip and upload the catalog file to azure from publish method");
                }

                Context.Log(LogLevel.Information, string.Format("Nodes: {0}", epiElements["Nodes"].Count));
                Context.Log(LogLevel.Information, string.Format("Entries: {0}", epiElements["Entries"].Count));
                Context.Log(LogLevel.Information, string.Format("Relations: {0}", epiElements["Relations"].Count));
                Context.Log(LogLevel.Information, string.Format("Associations: {0}", epiElements["Associations"].Count));
                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Done generating catalog.xml", 25);

                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Generating Resource.xml and saving files to azure file storage...", 26);

                List<StructureEntity> resources = Context.ExtensionManager.ChannelService.GetAllChannelStructureEntitiesForTypeFromPath(channelEntity.Id.ToString(), "Resource");

                Context.Log(LogLevel.Debug, $"there are {resources.Count} resources fetched");

                Configuration.ChannelStructureEntities.AddRange(resources);

                XDocument resourceDocument = resource.GetResourcesDocument(resources, Configuration);
                DocumentFileHelper documentFileHelper = new DocumentFileHelper();
                IEnumerable<string> pathToFileinAzure = documentFileHelper.UploadResourcesAndDocumentToAzure(resources, resourceDocument, Configuration,
                    folderDateTime, Context);

                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Publish -> Done generating/saving Resource.xml", 50);
                publishStopWatch.Stop();

                if (Configuration.ActivePublicationMode.Equals(PublicationMode.Automatic))
                {
                    Context.Log(LogLevel.Debug, "Starting automatic import!");
                    connectorEventHelper.UpdateConnectorEvent(
                        publishConnectorEvent,
                        "Sending Catalog.xml to EPiServer...",
                        51);
                    if (epiApi.StartImportIntoEpiServerCommerce(Configuration.CatalogPathInCloud,
                            channelHelper.GetChannelGuid(channelEntity, Configuration),
                        Configuration))
                    {
                        connectorEventHelper.UpdateConnectorEvent(
                            publishConnectorEvent,
                            "Done sending Catalog.xml to EPiServer",
                            75);

                    }
                    else
                    {
                        connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Error while sending Catalog.xml to EPiServer", -1, true);
                        return;
                    }

                    connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Sending Resources to EPiServer...", 76);

                    if (epiApi.StartAssetImportIntoEpiServerCommerce(pathToFileinAzure, Path.Combine(Configuration.ResourcesRootPath, folderDateTime), Configuration))
                    {
                        connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Done sending Resources to EPiServer...", 99);
                        resourceIncluded = true;
                    }
                    else
                    {
                        connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Error while sending resources to EPiServer", -1, true);
                    }
                }

                if (!publishConnectorEvent.IsError)
                {
                    connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, "Publish done!", 100);
                    string channelName =
                        epiMappingHelper.GetNameForEntity(
                            Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.Shallow),
                            Configuration,
                            100);
                    epiApi.ImportUpdateCompleted(
                        channelName,
                        ImportUpdateCompletedEventType.Publish,
                        resourceIncluded,
                        Configuration);
                }
            }
            catch (Exception exception)
            {
                Context.Log(LogLevel.Error, "Exception in Publish", exception);
                connectorEventHelper.UpdateConnectorEvent(publishConnectorEvent, exception.Message, -1, true);
            }
            finally
            {
                Configuration.EntityIdAndType = new Dictionary<int, string>();
                Configuration.ChannelStructureEntities = new List<StructureEntity>();
                Configuration.ChannelEntities = new Dictionary<int, Entity>();
            }
        }

        public void UnPublish(int channelId)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            Context.Log(LogLevel.Information, string.Format("Unpublish on channel: {0} called. No action taken.", channelId));
        }

        public void Synchronize(int channelId)
        {
        }

        public void ChannelEntityAdded(int channelId, int entityId)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            Configuration.ChannelStructureEntities = new List<StructureEntity>();

            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);
            ChannelHelper channelHelper = new ChannelHelper(Context);
            BusinessHelper businessHelper = new BusinessHelper(Context);
            EpiApi epiApi = new EpiApi(Context);
            EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
            Resources resources = new Resources(Context);

            Context.Log(LogLevel.Debug, string.Format("Received entity added for entity {0} in channel {1}", entityId, channelId));
            ConnectorEvent entityAddedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.ChannelEntityAdded, string.Format("Received entity added for entity {0} in channel {1}", entityId, channelId), 0);

            bool resourceIncluded = false;
            Stopwatch entityAddedStopWatch = new Stopwatch();

            entityAddedStopWatch.Start();

            try
            {
                Entity channelEntity = InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(
                        entityAddedConnectorEvent,
                        "Failed to initial ChannelLinkAdded. Could not find the channel.",
                        -1,
                        true);
                    return;
                }

                List<StructureEntity> addedStructureEntities =
                    channelHelper.GetStructureEntitiesForEntityInChannel(Configuration.ChannelId, entityId);

                foreach (StructureEntity addedStructureEntity in addedStructureEntities)
                {
                    Configuration.ChannelStructureEntities.Add(
                        channelHelper.GetParentStructureEntity(
                            Configuration.ChannelId,
                            addedStructureEntity.ParentId,
                            addedStructureEntity.EntityId,
                            addedStructureEntities));
                }

                Configuration.ChannelStructureEntities.AddRange(addedStructureEntities);

                string targetEntityPath = channelHelper.GetTargetEntityPath(entityId, addedStructureEntities);

                foreach (
                    StructureEntity childStructureEntity in
                    channelHelper.GetChildrenEntitiesInChannel(entityId, targetEntityPath))
                {
                    Configuration.ChannelStructureEntities.AddRange(
                        channelHelper.GetChildrenEntitiesInChannel(
                            childStructureEntity.EntityId,
                            childStructureEntity.Path));
                }

                Configuration.ChannelStructureEntities.AddRange(
                    channelHelper.GetChildrenEntitiesInChannel(entityId, targetEntityPath));
                channelHelper.BuildEntityIdAndTypeDict(Configuration);

                new AddUtility(Configuration, Context).Add(channelEntity, entityAddedConnectorEvent, out resourceIncluded);
                entityAddedStopWatch.Stop();
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, "Exception in ChannelEntityAdded", ex);
                connectorEventHelper.UpdateConnectorEvent(entityAddedConnectorEvent, ex.Message, -1, true);
                return;
            }
            finally
            {
                Configuration.EntityIdAndType = new Dictionary<int, string>();
            }

            entityAddedStopWatch.Stop();

            Context.Log(LogLevel.Information, string.Format("Add done for channel {0}, took {1}!", channelId, businessHelper.GetElapsedTimeFormated(entityAddedStopWatch)));
            connectorEventHelper.UpdateConnectorEvent(entityAddedConnectorEvent, "ChannelEntityAdded complete", 100);

            if (!entityAddedConnectorEvent.IsError)
            {
                string channelName = epiMappingHelper.GetNameForEntity(Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.Shallow), Configuration, 100);
                epiApi.ImportUpdateCompleted(channelName, ImportUpdateCompletedEventType.EntityAdded, resourceIncluded, Configuration);
            }

        }

        public void ChannelEntityUpdated(int channelId, int entityId, string data)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            Configuration.ChannelEntities = new Dictionary<int, Entity>();
            Configuration.ChannelStructureEntities = new List<StructureEntity>();

            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);
            ChannelHelper channelHelper = new ChannelHelper(Context);
            EpiApi epiApi = new EpiApi(Context);
            EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
            BusinessHelper businessHelper = new BusinessHelper(Context);
            EpiDocument epiDocument = new EpiDocument(Context, Configuration);
            Resources resources = new Resources(Context);

            Context.Log(LogLevel.Debug, string.Format("Received entity update for entity {0} in channel {1}", entityId, channelId));
            ConnectorEvent entityUpdatedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.ChannelEntityUpdated, string.Format("Received entity update for entity {0} in channel {1}", entityId, channelId), 0);

            Stopwatch entityUpdatedStopWatch = new Stopwatch();
            entityUpdatedStopWatch.Start();

            try
            {
                if (channelId == entityId)
                {
                    connectorEventHelper.UpdateConnectorEvent(entityUpdatedConnectorEvent, string.Format("ChannelEntityUpdated, updated Entity is the Channel, no action required"), 100);
                    return;
                }

                Entity channelEntity = InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(entityUpdatedConnectorEvent, string.Format("Failed to initial ChannelEntityUpdated. Could not find the channel with id: {0}", channelId), -1, true);
                    return;
                }

                Entity updatedEntity = Context.ExtensionManager.DataService.GetEntity(entityId, LoadLevel.DataAndLinks);

                if (updatedEntity == null)
                {
                    Context.Log(LogLevel.Error, string.Format("ChannelEntityUpdated, could not find entity with id: {0}", entityId));
                    connectorEventHelper.UpdateConnectorEvent(entityUpdatedConnectorEvent, string.Format("ChannelEntityUpdated, could not find entity with id: {0}", entityId), -1, true);

                    return;
                }

                string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");

                bool resourceIncluded = false;
                string channelName = epiMappingHelper.GetNameForEntity(channelEntity, Configuration, 100);

                Configuration.ChannelStructureEntities =
                    channelHelper.GetStructureEntitiesForEntityInChannel(Configuration.ChannelId, entityId);

                channelHelper.BuildEntityIdAndTypeDict(Configuration);

                if (updatedEntity.EntityType.Id.Equals("Resource"))
                {
                    XDocument resDoc = resources.HandleResourceUpdate(updatedEntity, Configuration, folderDateTime);
                    IEnumerable<XElement> resourceFileElements = resDoc.Document.Element("Resources")?.Element("ResourceFiles")?.Elements("Resource");
                    Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

                    if (resourceFileElements != null && resourceFileElements.Any())
                    {
                        foreach (XElement resourceFileElement in resourceFileElements)
                        {
                            int resourceEntityId;
                            if (int.TryParse(resourceFileElement.Attribute("id").Value, out resourceEntityId))
                            {
                                Entity targetEntity = Context.ExtensionManager.DataService.GetEntity(resourceEntityId, LoadLevel.DataOnly);

                                Context.Log(LogLevel.Debug, $"Adding image file {targetEntity.DisplayName}({targetEntity.Id})");
                                int resourceFileId = resources.GetResourceFileId(targetEntity);

                                foreach (string displayConfig in resources.GetDisplayConfigurations(targetEntity, Configuration))
                                {
                                    string fileName = resources.GetResourceFileName(targetEntity, resourceFileId, displayConfig, Configuration);

                                    byte[] resourceData = Context.ExtensionManager.UtilityService.GetFile(resourceFileId, displayConfig);

                                    if (resourceData != null)
                                    {
                                        files.Add($"{displayConfig}/{fileName}", resourceData);
                                    }
                                }

                            }
                        }
                    }

                    DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Resources, resDoc, Configuration, folderDateTime, files);
                    string resourceZipFile = string.Format("resource_{0}.zip", folderDateTime);

                    Context.Log(LogLevel.Debug, "Resources saved!");
                    if (Configuration.ActivePublicationMode.Equals(PublicationMode.Automatic))
                    {
                        Context.Log(LogLevel.Debug, "Starting automatic resource import!");
                        if (epiApi.StartAssetImportIntoEpiServerCommerce(
                                        Configuration.ResourceNameInCloud, /*Path.Combine(configuration.ResourcesRootPath, folderDateTime, "Resources.xml")*/
                                        Path.Combine(Configuration.ResourcesRootPath, folderDateTime),
                                        Configuration))
                        {
                            epiApi.SendHttpPost(Configuration, Path.Combine(Configuration.ResourcesRootPath, folderDateTime, resourceZipFile));
                            resourceIncluded = true;
                        }
                    }
                }
                else
                {
                    Context.Log(
                        LogLevel.Debug,
                        string.Format(
                            "Updated entity found. Type: {0}, id: {1}",
                            updatedEntity.EntityType.Id,
                            updatedEntity.Id));

                    #region SKU and ChannelNode
                    if (updatedEntity.EntityType.Id.Equals("Item") && data != null && data.Split(',').Contains("SKUs"))
                    {
                        Field currentField = Context.ExtensionManager.DataService.GetField(entityId, "SKUs");

                        List<Field> fieldHistory = Context.ExtensionManager.DataService.GetFieldHistory(entityId, "SKUs");

                        Field previousField = fieldHistory.FirstOrDefault(f => f.Revision == currentField.Revision - 1);

                        string oldXml = string.Empty;
                        if (previousField != null && previousField.Data != null)
                        {
                            oldXml = (string)previousField.Data;
                        }

                        string newXml = string.Empty;
                        if (currentField.Data != null)
                        {
                            newXml = (string)currentField.Data;
                        }

                        List<XElement> skusToDelete, skusToAdd;
                        businessHelper.CompareAndParseSkuXmls(oldXml, newXml, out skusToAdd, out skusToDelete);

                        foreach (XElement skuToDelete in skusToDelete)
                        {
                            epiApi.DeleteCatalogEntry(skuToDelete.Attribute("id").Value, Configuration);
                        }

                        if (skusToAdd.Count > 0)
                        {
                            new AddUtility(Configuration, Context).Add(
                                channelEntity,
                                entityUpdatedConnectorEvent,
                                out resourceIncluded);
                        }
                    }
                    else if (updatedEntity.EntityType.Id.Equals("ChannelNode"))
                    {
                        new AddUtility(Configuration, Context).Add(
                            channelEntity,
                            entityUpdatedConnectorEvent,
                            out resourceIncluded);

                        entityUpdatedStopWatch.Stop();
                        Context.Log(
                            LogLevel.Information,
                            string.Format(
                                "Update done for channel {0}, took {1}!",
                                channelId,
                                businessHelper.GetElapsedTimeFormated(entityUpdatedStopWatch)));

                        connectorEventHelper.UpdateConnectorEvent(
                            entityUpdatedConnectorEvent,
                            "ChannelEntityUpdated complete",
                            100);

                        // Fire the complete event
                        epiApi.ImportUpdateCompleted(
                            channelName,
                            ImportUpdateCompletedEventType.EntityUpdated,
                            resourceIncluded,
                            Configuration);
                        return;
                    }
                    #endregion

                    if (updatedEntity.EntityType.IsLinkEntityType)
                    {
                        //ChannelEntities will be used for LinkEntity when we get EPiCode with channel prefix
                        if (!Configuration.ChannelEntities.ContainsKey(updatedEntity.Id))
                        {
                            Configuration.ChannelEntities.Add(updatedEntity.Id, updatedEntity);
                        }
                    }

                    XDocument doc = epiDocument.CreateUpdateDocument(channelEntity, updatedEntity, Configuration);

                    // If data exist in EPiCodeFields.
                    // Update Associations and relations for XDocument doc.
                    if (Configuration.EpiCodeMapping.ContainsKey(updatedEntity.EntityType.Id) &&
                        data.Split(',').Contains(Configuration.EpiCodeMapping[updatedEntity.EntityType.Id]))
                    {
                        channelHelper.EpiCodeFieldUpdatedAddAssociationAndRelationsToDocument(
                            doc,
                            updatedEntity,
                            Configuration,
                            channelId);
                    }

                    ChannelPrefixHelper channelPrefixHelper = new ChannelPrefixHelper(Context);

                    if (updatedEntity.EntityType.IsLinkEntityType)
                    {
                        List<Link> links = Context.ExtensionManager.DataService.GetLinksForLinkEntity(updatedEntity.Id);
                        if (links.Count > 0)
                        {
                            string parentId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(links.First().Source.Id, Configuration);

                            epiApi.UpdateLinkEntityData(updatedEntity, channelId, channelEntity, Configuration, parentId);
                        }
                    }

                    //string zippedName = DocumentFileHelper.SaveAndZipDocument(channelIdentifier, doc, folderDateTime, configuration, Context);

                    if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, doc, Configuration, folderDateTime))
                    {
                        Context.Log(LogLevel.Debug, "Failed to upload zip file to azure in ChannelEntityUpdated");

                    }

                    if (Configuration.ActivePublicationMode.Equals(PublicationMode.Automatic))
                    {
                        Context.Log(LogLevel.Debug, "Starting automatic import!");
                        epiApi.StartImportIntoEpiServerCommerce(
                            Configuration.CatalogPathInCloud, /*Path.Combine(configuration.PublicationsRootPath, folderDateTime, "Catalog.xml")*/
                            channelHelper.GetChannelGuid(channelEntity, Configuration),
                            Configuration);
                    }
                }

                epiApi.ImportUpdateCompleted(channelName, ImportUpdateCompletedEventType.EntityUpdated, resourceIncluded, Configuration);
                entityUpdatedStopWatch.Stop();

            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, "Exception in ChannelEntityUpdated", ex);
                connectorEventHelper.UpdateConnectorEvent(entityUpdatedConnectorEvent, ex.Message, -1, true);
            }
            finally
            {
                Configuration.ChannelStructureEntities = new List<StructureEntity>();
                Configuration.EntityIdAndType = new Dictionary<int, string>();
                Configuration.ChannelEntities = new Dictionary<int, Entity>();
            }

            Context.Log(LogLevel.Information, string.Format("Update done for channel {0}, took {1}!", channelId, businessHelper.GetElapsedTimeFormated(entityUpdatedStopWatch)));
            connectorEventHelper.UpdateConnectorEvent(entityUpdatedConnectorEvent, "ChannelEntityUpdated complete", 100);
        }

        public void ChannelEntityDeleted(int channelId, Entity deletedEntity)
        {
            int entityId = deletedEntity.Id;
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            BusinessHelper businessHelper = new BusinessHelper(Context);
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            if (Configuration.ModifyFilterBehavior)
            {
                Entity exists = Context.ExtensionManager.DataService.GetEntity(deletedEntity.Id, LoadLevel.Shallow);
                if (exists != null)
                {
                    Context.Log(LogLevel.Debug, string.Format("Ignored deleted for entity {0} in channel {1} becuase of ModifiedFilterBehavior", entityId, channelId));
                    return;
                }
            }

            Stopwatch deleteStopWatch = new Stopwatch();
            Context.Log(LogLevel.Debug, string.Format("Received entity deleted for entity {0} in channel {1}", entityId, channelId));
            ConnectorEvent entityDeletedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.ChannelEntityDeleted, string.Format("Received entity deleted for entity {0} in channel {1}", entityId, channelId), 0);

            try
            {
                Context.Log(LogLevel.Debug, string.Format("Received entity deleted for entity {0} in channel {1}", entityId, channelId));
                deleteStopWatch.Start();

                Entity channelEntity = this.InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(entityDeletedConnectorEvent, "Failed to initial ChannelEntityDeleted. Could not find the channel.", -1, true);
                    return;
                }

                new DeleteUtility(Configuration, Context).Delete(channelEntity, -1, deletedEntity, string.Empty);
                deleteStopWatch.Stop();
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, "Exception in ChannelEntityDeleted", ex);
                connectorEventHelper.UpdateConnectorEvent(entityDeletedConnectorEvent, ex.Message, -1, true);
                return;
            }
            finally
            {
                Configuration.EntityIdAndType = new Dictionary<int, string>();
            }

            Context.Log(LogLevel.Information, string.Format("Delete done for channel {0}, took {1}!", channelId, businessHelper.GetElapsedTimeFormated(deleteStopWatch)));
            connectorEventHelper.UpdateConnectorEvent(entityDeletedConnectorEvent, "ChannelEntityDeleted complete", 100);

            if (!entityDeletedConnectorEvent.IsError)
            {
                EpiApi epiApi = new EpiApi(Context);
                EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
                string channelName = epiMappingHelper.GetNameForEntity(Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.Shallow), Configuration, 100);
                epiApi.DeleteCompleted(channelName, DeleteCompletedEventType.EntitiyDeleted, Configuration);
            }
        }

        public void ChannelEntityFieldSetUpdated(int channelId, int entityId, string fieldSetId)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            this.ChannelEntityUpdated(channelId, entityId, string.Empty);
        }

        public void ChannelEntitySpecificationFieldAdded(int channelId, int entityId, string fieldName)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            this.ChannelEntityUpdated(channelId, entityId, string.Empty);
        }

        public void ChannelEntitySpecificationFieldUpdated(int channelId, int entityId, string fieldName)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            this.ChannelEntityUpdated(channelId, entityId, string.Empty);
        }

        public void ChannelLinkAdded(int channelId, int sourceEntityId, int targetEntityId, string linkTypeId, int? linkEntityId)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            Configuration.ChannelStructureEntities = new List<StructureEntity>();
            ChannelHelper channelHelper = new ChannelHelper(Context);
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            Context.Log(LogLevel.Debug, string.Format("Received link added for sourceEntityId {0} and targetEntityId {1} in channel {2}", sourceEntityId, targetEntityId, channelId));
            ConnectorEvent linkAddedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.ChannelLinkAdded, string.Format("Received link added for sourceEntityId {0} and targetEntityId {1} in channel {2}", sourceEntityId, targetEntityId, channelId), 0);

            bool resourceIncluded;
            Stopwatch linkAddedStopWatch = new Stopwatch();
            try
            {
                linkAddedStopWatch.Start();

                // NEW CODE
                Entity channelEntity = this.InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(linkAddedConnectorEvent, "Failed to initial ChannelLinkAdded. Could not find the channel.", -1, true);
                    return;
                }

                connectorEventHelper.UpdateConnectorEvent(linkAddedConnectorEvent, "Fetching channel entities...", 1);

                List<StructureEntity> existingEntitiesInChannel = channelHelper.GetStructureEntitiesForEntityInChannel(Configuration.ChannelId, targetEntityId);

                Entity targetEntity = Context.ExtensionManager.DataService.GetEntity(targetEntityId, LoadLevel.DataOnly);

                //Get Parents EntityStructure from Path
                List<StructureEntity> parents = new List<StructureEntity>();

                foreach (StructureEntity existingEntity in existingEntitiesInChannel)
                {
                    List<string> parentIds = existingEntity.Path.Split('/').ToList();
                    parentIds.Reverse();
                    parentIds.RemoveAt(0);

                    for (int i = 0; i < parentIds.Count - 1; i++)
                    {
                        int entityId = int.Parse(parentIds[i]);
                        int parentId = int.Parse(parentIds[i + 1]);

                        parents.AddRange(Context.ExtensionManager.ChannelService.GetAllStructureEntitiesForEntityWithParentInChannel(channelId, entityId, parentId));
                    }
                }

                List<StructureEntity> children = new List<StructureEntity>();

                foreach (StructureEntity existingEntity in existingEntitiesInChannel)
                {
                    string targetEntityPath = channelHelper.GetTargetEntityPath(existingEntity.EntityId, existingEntitiesInChannel, existingEntity.ParentId);
                    children.AddRange(Context.ExtensionManager.ChannelService.GetAllChannelStructureEntitiesFromPath(targetEntityPath));
                }

                Configuration.ChannelStructureEntities.AddRange(parents);
                Configuration.ChannelStructureEntities.AddRange(children);

                // Remove duplicates
                Configuration.ChannelStructureEntities =
                    Configuration.ChannelStructureEntities.GroupBy(x => x.EntityId).Select(x => x.First()).ToList();

                //Adding existing Entities. If it occurs more than one time in channel. We can not remove duplicates.
                Configuration.ChannelStructureEntities.AddRange(existingEntitiesInChannel);

                channelHelper.BuildEntityIdAndTypeDict(Configuration);

                connectorEventHelper.UpdateConnectorEvent(linkAddedConnectorEvent, "Done fetching channel entities", 10);

                new AddUtility(Configuration, Context).Add(
                    channelEntity,
                    linkAddedConnectorEvent,
                    out resourceIncluded);

                linkAddedStopWatch.Stop();
            }
            catch (Exception ex)
            {

                Context.Log(LogLevel.Error, "Exception in ChannelLinkAdded", ex);
                connectorEventHelper.UpdateConnectorEvent(linkAddedConnectorEvent, ex.Message, -1, true);
                return;
            }
            finally
            {
                Configuration.EntityIdAndType = new Dictionary<int, string>();
            }

            linkAddedStopWatch.Stop();

            Context.Log(LogLevel.Information, string.Format("ChannelLinkAdded done for channel {0}, took {1}!", channelId, linkAddedStopWatch.GetElapsedTimeFormated()));
            connectorEventHelper.UpdateConnectorEvent(linkAddedConnectorEvent, "ChannelLinkAdded complete", 100);

            if (!linkAddedConnectorEvent.IsError)
            {
                EpiApi epiApi = new EpiApi(Context);
                EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
                string channelName = epiMappingHelper.GetNameForEntity(Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.Shallow), Configuration, 100);
                epiApi.ImportUpdateCompleted(channelName, ImportUpdateCompletedEventType.LinkAdded, resourceIncluded, Configuration);
            }
        }

        public void ChannelLinkDeleted(int channelId, int sourceEntityId, int targetEntityId, string linkTypeId, int? linkEntityId)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            Configuration.ChannelStructureEntities = new List<StructureEntity>();
            Configuration.ChannelEntities = new Dictionary<int, Entity>();
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            Context.Log(LogLevel.Debug, string.Format("Received link deleted for sourceEntityId {0} and targetEntityId {1} in channel {2}", sourceEntityId, targetEntityId, channelId));
            ConnectorEvent linkDeletedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.ChannelLinkDeleted, string.Format("Received link deleted for sourceEntityId {0} and targetEntityId {1} in channel {2}", sourceEntityId, targetEntityId, channelId), 0);

            Stopwatch linkDeletedStopWatch = new Stopwatch();

            try
            {
                linkDeletedStopWatch.Start();

                Entity channelEntity = this.InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(linkDeletedConnectorEvent, "Failed to initial ChannelLinkDeleted. Could not find the channel.", -1, true);
                    return;
                }

                Entity targetEntity = Context.ExtensionManager.DataService.GetEntity(targetEntityId, LoadLevel.DataAndLinks);

                new DeleteUtility(Configuration, Context).Delete(channelEntity, sourceEntityId, targetEntity, linkTypeId);

                linkDeletedStopWatch.Stop();
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, "Exception in ChannelLinkDeleted", ex);
                connectorEventHelper.UpdateConnectorEvent(linkDeletedConnectorEvent, ex.Message, -1, true);
                return;
            }
            finally
            {
                Configuration.EntityIdAndType = new Dictionary<int, string>();
                Configuration.ChannelEntities = new Dictionary<int, Entity>();
            }

            linkDeletedStopWatch.Stop();

            Context.Log(LogLevel.Information, string.Format("ChannelLinkDeleted done for channel {0}, took {1}!", channelId, linkDeletedStopWatch.GetElapsedTimeFormated()));
            connectorEventHelper.UpdateConnectorEvent(linkDeletedConnectorEvent, "ChannelLinkDeleted complete", 100);

            if (!linkDeletedConnectorEvent.IsError)
            {
                EpiApi epiApi = new EpiApi(Context);
                EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
                string channelName = epiMappingHelper.GetNameForEntity(Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.Shallow), Configuration, 100);
                epiApi.DeleteCompleted(channelName, DeleteCompletedEventType.LinkDeleted, Configuration);
            }
        }

        public void ChannelLinkUpdated(int channelId, int sourceEntityId, int targetEntityId, string linkTypeId, int? linkEntityId)
        {
            if (channelId != Configuration.ChannelId)
            {
                return;
            }

            Configuration.ChannelStructureEntities = new List<StructureEntity>();
            ChannelHelper channelHelper = new ChannelHelper(Context);
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            Context.Log(LogLevel.Debug, string.Format("Received link update for sourceEntityId {0} and targetEntityId {1} in channel {2}", sourceEntityId, targetEntityId, channelId));
            ConnectorEvent linkUpdatedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.ChannelLinkAdded, string.Format("Received link update for sourceEntityId {0} and targetEntityId {1} in channel {2}", sourceEntityId, targetEntityId, channelId), 0);

            bool resourceIncluded;

            Stopwatch linkAddedStopWatch = new Stopwatch();
            try
            {
                linkAddedStopWatch.Start();

                // NEW CODE
                Entity channelEntity = this.InitiateChannelConfiguration(channelId);
                if (channelEntity == null)
                {
                    connectorEventHelper.UpdateConnectorEvent(
                        linkUpdatedConnectorEvent,
                        "Failed to initial ChannelLinkUpdated. Could not find the channel.",
                        -1,
                        true);
                    return;
                }

                connectorEventHelper.UpdateConnectorEvent(linkUpdatedConnectorEvent, "Fetching channel entities...", 1);

                List<StructureEntity> targetEntityStructure = channelHelper.GetEntityInChannelWithParent(Configuration.ChannelId, targetEntityId, sourceEntityId);

                StructureEntity parentStructureEntity = channelHelper.GetParentStructureEntity(Configuration.ChannelId, sourceEntityId, targetEntityId, targetEntityStructure);

                if (parentStructureEntity != null)
                {
                    Configuration.ChannelStructureEntities.Add(parentStructureEntity);

                    Configuration.ChannelStructureEntities.AddRange(
                        channelHelper.GetChildrenEntitiesInChannel(
                            parentStructureEntity.EntityId,
                            parentStructureEntity.Path));

                    channelHelper.BuildEntityIdAndTypeDict(Configuration);

                    connectorEventHelper.UpdateConnectorEvent(
                        linkUpdatedConnectorEvent,
                        "Done fetching channel entities",
                        10);

                    Entity targetEntity = Context.ExtensionManager.DataService.GetEntity(targetEntityId, LoadLevel.DataOnly);

                    new AddUtility(Configuration, Context).Add(channelEntity, linkUpdatedConnectorEvent, out resourceIncluded);
                }
                else
                {
                    linkAddedStopWatch.Stop();
                    Context.Log(LogLevel.Error, string.Format("Not possible to located source entity {0} in channel structure for target entity {1}", sourceEntityId, targetEntityId));
                    connectorEventHelper.UpdateConnectorEvent(linkUpdatedConnectorEvent, string.Format("Not possible to located source entity {0} in channel structure for target entity {1}", sourceEntityId, targetEntityId), -1, true);
                    return;
                }

                linkAddedStopWatch.Stop();
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, "Exception in ChannelLinkUpdated", ex);
                connectorEventHelper.UpdateConnectorEvent(linkUpdatedConnectorEvent, ex.Message, -1, true);
                return;
            }
            finally
            {
                Configuration.EntityIdAndType = new Dictionary<int, string>();
            }

            linkAddedStopWatch.Stop();

            Context.Log(LogLevel.Information, string.Format("ChannelLinkUpdated done for channel {0}, took {1}!", channelId, linkAddedStopWatch.GetElapsedTimeFormated()));
            connectorEventHelper.UpdateConnectorEvent(linkUpdatedConnectorEvent, "ChannelLinkUpdated complete", 100);

            if (!linkUpdatedConnectorEvent.IsError)
            {
                EpiApi epiApi = new EpiApi(Context);
                EpiMappingHelper epiMappingHelper = new EpiMappingHelper(Context);
                string channelName = epiMappingHelper.GetNameForEntity(Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.Shallow), Configuration, 100);
                epiApi.ImportUpdateCompleted(channelName, ImportUpdateCompletedEventType.LinkUpdated, resourceIncluded, Configuration);
            }
        }

        public void AssortmentCopiedInChannel(int channelId, int assortmentId, int targetId, string targetType)
        {

        }

        Dictionary<string, string> ICVLListener.DefaultSettings
        {
            get { return _defaultSettings; }
        }

        Dictionary<string, string> IChannelListener.DefaultSettings
        {
            get { return _defaultSettings; }
        }

        string ICVLListener.Test()
        {
            throw new NotImplementedException();
        }

        public void CVLValueCreated(string cvlId, string cvlValueKey)
        {
            Context.Log(LogLevel.Information, string.Format("CVL value created event received with key '{0}' from CVL with id {1}", cvlValueKey, cvlId));
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            ConnectorEvent cvlValueCreatedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.CVLValueCreated, string.Format("CVL value created event received with key '{0}' from CVL with id {1}", cvlValueKey, cvlId), 0);

            BusinessHelper businessHelper = new BusinessHelper(Context);
            try
            {
                CVLValue val = Context.ExtensionManager.ModelService.GetCVLValueByKey(cvlValueKey, cvlId);

                if (val != null)
                {
                    if (!businessHelper.CVLValues.Any(cv => cv.CVLId.Equals(cvlId) && cv.Key.Equals(cvlValueKey)))
                    {
                        businessHelper.CVLValues.Add(val);
                    }

                    string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");
                    new CvlUtility(Configuration, Context).AddCvl(cvlId, folderDateTime);
                }
                else
                {
                    Context.Log(LogLevel.Error, string.Format("Could not add CVL value with key {0} to CVL with id {1}", cvlValueKey, cvlId));
                    connectorEventHelper.UpdateConnectorEvent(cvlValueCreatedConnectorEvent, string.Format("Could not add CVL value with key {0} to CVL with id {1}", cvlValueKey, cvlId), -1, true);
                }
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, string.Format("Could not add CVL value with key {0} to CVL with id {1}", cvlValueKey, cvlId), ex);
                connectorEventHelper.UpdateConnectorEvent(cvlValueCreatedConnectorEvent, ex.Message, -1, true);
            }

            connectorEventHelper.UpdateConnectorEvent(cvlValueCreatedConnectorEvent, "CVLValueCreated complete", 100);

        }

        public void CVLValueUpdated(string cvlId, string cvlValueKey)
        {
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            ConnectorEvent cvlValueUpdatedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.CVLValueCreated, string.Format("CVL value updated for CVL {0} and key {1}", cvlId, cvlValueKey), 0);
            Context.Log(LogLevel.Debug, string.Format("CVL value updated for CVL {0} and key {1}", cvlId, cvlValueKey));

            BusinessHelper businessHelper = new BusinessHelper(Context);
            ChannelHelper channelHelper = new ChannelHelper(Context);

            try
            {
                CVLValue val = Context.ExtensionManager.ModelService.GetCVLValueByKey(cvlValueKey, cvlId);
                if (val != null)
                {
                    CVLValue cachedValue = businessHelper.CVLValues.FirstOrDefault(cv => cv.CVLId.Equals(cvlId) && cv.Key.Equals(cvlValueKey));
                    if (cachedValue == null)
                    {
                        return;
                    }

                    string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");
                    new CvlUtility(Configuration, Context).AddCvl(cvlId, folderDateTime);

                    if (Configuration.ActiveCVLDataMode == CVLDataMode.KeysAndValues || Configuration.ActiveCVLDataMode == CVLDataMode.Values)
                    {
                        List<FieldType> allFieldTypes = Context.ExtensionManager.ModelService.GetAllFieldTypes();
                        List<FieldType> allFieldsWithThisCvl = allFieldTypes.FindAll(ft => ft.CVLId == cvlId);
                        Query query = new Query
                        {
                            Join = Join.Or,
                            Criteria = new List<Criteria>()
                        };

                        foreach (FieldType fieldType in allFieldsWithThisCvl)
                        {
                            Criteria criteria = new Criteria
                            {
                                FieldTypeId = fieldType.Id,
                                Operator = Operator.Equal,
                                Value = cvlValueKey
                            };

                            query.Criteria.Add(criteria);
                        }

                        List<Entity> entitesWithThisCvlInPim = Context.ExtensionManager.DataService.Search(query, LoadLevel.Shallow);
                        if (entitesWithThisCvlInPim.Count == 0)
                        {
                            Context.Log(LogLevel.Debug, string.Format("CVL value updated complete"));

                            connectorEventHelper.UpdateConnectorEvent(cvlValueUpdatedConnectorEvent, "CVLValueUpdated complete, no action was needed", 100);
                            return;
                        }


                        List<StructureEntity> channelEntities = channelHelper.GetAllEntitiesInChannel(Configuration.ChannelId, Configuration.ExportEnabledEntityTypes);

                        List<Entity> entitesToUpdate = new List<Entity>();

                        foreach (Entity entity in entitesWithThisCvlInPim)
                        {
                            if (channelEntities.Any() && channelEntities.Exists(i => i.EntityId.Equals(entity.Id)))
                            {
                                entitesToUpdate.Add(entity);
                            }
                        }

                        foreach (Entity entity in entitesToUpdate)
                        {
                            this.ChannelEntityUpdated(Configuration.ChannelId, entity.Id, string.Empty);
                        }
                    }
                }
                else
                {
                    Context.Log(LogLevel.Error, string.Format("Could not update CVL value with key {0} for CVL with id {1}", cvlValueKey, cvlId));
                    connectorEventHelper.UpdateConnectorEvent(cvlValueUpdatedConnectorEvent, string.Format("Could not update CVL value with key {0} for CVL with id {1}", cvlValueKey, cvlId), -1, true);
                }
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, string.Format("Could not add CVL value {0} to CVL with id {1}", cvlValueKey, cvlId), ex);
                connectorEventHelper.UpdateConnectorEvent(cvlValueUpdatedConnectorEvent, ex.Message, -1, true);
            }

            Context.Log(LogLevel.Debug, string.Format("CVL value updated complete"));
            connectorEventHelper.UpdateConnectorEvent(cvlValueUpdatedConnectorEvent, "CVLValueUpdated complete", 100);
        }

        public void CVLValueDeleted(string cvlId, string cvlValueKey)
        {
            Context.Log(LogLevel.Information, string.Format("CVL value deleted event received with key '{0}' from CVL with id {1}", cvlValueKey, cvlId));
            ConnectorEventHelper connectorEventHelper = new ConnectorEventHelper(Context);

            ConnectorEvent cvlValueDeletedConnectorEvent = connectorEventHelper.InitiateConnectorEvent(ConnectorEventType.CVLValueDeleted, string.Format("CVL value deleted event received with key '{0}' from CVL with id {1}", cvlValueKey, cvlId), 0);
            BusinessHelper businessHelper = new BusinessHelper(Context);

            if (businessHelper.CVLValues.RemoveAll(cv => cv.CVLId.Equals(cvlId) && cv.Key.Equals(cvlValueKey)) < 1)
            {
                Context.Log(LogLevel.Error, string.Format("Could not remove CVL value with key {0} from CVL with id {1}", cvlValueKey, cvlId));
                connectorEventHelper.UpdateConnectorEvent(cvlValueDeletedConnectorEvent, string.Format("Could not remove CVL value with key {0} from CVL with id {1}", cvlValueKey, cvlId), -1, true);

                return;
            }

            connectorEventHelper.UpdateConnectorEvent(cvlValueDeletedConnectorEvent, "CVLValueDeleted complete", 100);
        }

        public void CVLValueDeletedAll(string cvlId)
        {

        }

        public inRiverContext Context
        {
            get; set;
        }

        private Entity InitiateChannelConfiguration(int channelId)
        {
            Entity channel = Context.ExtensionManager.DataService.GetEntity(channelId, LoadLevel.DataOnly);
            if (channel == null)
            {
                Context.Log(LogLevel.Error, "Could not find channel");
                return null;
            }
            ChannelHelper channelHelper = new ChannelHelper(Context);

            channelHelper.UpdateChannelSettings(channel, Configuration);
            return channel;
        }
    }
}
