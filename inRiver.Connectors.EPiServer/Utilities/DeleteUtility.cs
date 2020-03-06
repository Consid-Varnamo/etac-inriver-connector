using inRiver.Connectors.EPiServer.Communication;
using inRiver.Connectors.EPiServer.Enums;
using inRiver.Connectors.EPiServer.EpiXml;
using inRiver.Connectors.EPiServer.Helpers;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace inRiver.Connectors.EPiServer.Utilities
{
    public class DeleteUtility
    {
        private Configuration DeleteUtilConfig { get; set; }

        private static inRiverContext _context;
        private readonly EpiApi _epiApi;
        private readonly EpiElement _epiElement;
        private readonly ChannelPrefixHelper _channelPrefixHelper;

        public DeleteUtility(Configuration deleteUtilConfig, inRiverContext context)
        {
            DeleteUtilConfig = deleteUtilConfig;
            _context = context;
            _epiApi = new EpiApi(context);
            _epiElement = new EpiElement(context);
            _channelPrefixHelper = new ChannelPrefixHelper(context);
        }

        public void Delete(Entity channelEntity, int parentEntityId, Entity targetEntity, string linkTypeId, List<int> productParentIds = null)
        {
            var channelHelper = new ChannelHelper(_context);
            string channelIdentifier = channelHelper.GetChannelIdentifier(channelEntity);
            string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");

            channelHelper.BuildEntityIdAndTypeDict(DeleteUtilConfig);


            if (!DeleteUtilConfig.ChannelEntities.ContainsKey(targetEntity.Id))
            {
                DeleteUtilConfig.ChannelEntities.Add(targetEntity.Id, targetEntity);
            }

            string resourceZipFile = string.Format("resource_{0}.zip", folderDateTime);

            if (_context.ExtensionManager.ChannelService.EntityExistsInChannel(channelEntity.Id, targetEntity.Id))
            {
                var existingEntities = _context.ExtensionManager.ChannelService.GetAllStructureEntitiesForEntityInChannel(
                                                                            channelEntity.Id,
                                                                            targetEntity.Id);

                Entity parentEnt = _context.ExtensionManager.DataService.GetEntity(parentEntityId, LoadLevel.DataOnly);

                if (parentEnt != null)
                {
                    if (!DeleteUtilConfig.ChannelEntities.ContainsKey(parentEnt.Id))
                    {
                        DeleteUtilConfig.ChannelEntities.Add(parentEnt.Id, parentEnt);
                    }
                    if (targetEntity.EntityType.Id == "Resource")
                    {
                        DeleteResource(
                            targetEntity,
                            parentEnt,
                            channelIdentifier,
                            folderDateTime,
                            resourceZipFile);
                    }
                    else
                    {
                        DeleteEntityThatStillExistInChannel(
                            channelEntity,
                            targetEntity,
                            parentEnt,
                            linkTypeId,
                            existingEntities,
                            channelIdentifier,
                            folderDateTime,
                            resourceZipFile);

                    }
                }
                else
                {
                    _context.Log(LogLevel.Information, $"Unable to find entity {parentEntityId} (already deleted?)");
                }
            }
            else
            {
                //DeleteEntity
                DeleteEntity(
                    channelEntity,
                    parentEntityId,
                    targetEntity,
                    linkTypeId,
                    channelIdentifier,
                    folderDateTime,
                    productParentIds);
            }
        }

        private void DeleteEntityThatStillExistInChannel(Entity channelEntity, Entity targetEntity, Entity parentEnt, string linkTypeId, List<StructureEntity> existingEntities, string channelIdentifier, string folderDateTime, string resourceZipFile)
        {
            var channelHelper = new ChannelHelper(_context);

            Dictionary<string, Dictionary<string, bool>> entitiesToUpdate = new Dictionary<string, Dictionary<string, bool>>();

            var channelNodes = _context.ExtensionManager.ChannelService.GetAllChannelStructureEntitiesForType(channelEntity.Id, "ChannelNode").ToList();

            if (!channelNodes.Any() && parentEnt.EntityType.Id == "Channel")
            {
                channelNodes.Add(_context.ExtensionManager.ChannelService.GetAllStructureEntitiesForEntityInChannel(channelEntity.Id, parentEnt.Id).First());
            }

            List<string> linkEntityIds = new List<string>();
            if (channelHelper.LinkTypeHasLinkEntity(linkTypeId))
            {
                DeleteUtilConfig.ChannelStructureEntities = channelHelper.GetAllEntitiesInChannel(
                                                                    channelEntity.Id,
                                                                    DeleteUtilConfig.ExportEnabledEntityTypes);

                List<StructureEntity> newEntityNodes = channelHelper.FindEntitiesElementInStructure(DeleteUtilConfig.ChannelStructureEntities, parentEnt.Id, targetEntity.Id, linkTypeId);

                List<string> pars = new List<string>();
                if (parentEnt.EntityType.Id == "Item" && DeleteUtilConfig.ItemsToSkus)
                {
                    pars = _epiElement.SkuItemIds(parentEnt, DeleteUtilConfig);

                    if (DeleteUtilConfig.UseThreeLevelsInCommerce)
                    {
                        pars.Add(parentEnt.Id.ToString(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    pars.Add(parentEnt.Id.ToString(CultureInfo.InvariantCulture));
                }

                List<string> targets = new List<string>();
                if (targetEntity.EntityType.Id == "Item" && DeleteUtilConfig.ItemsToSkus)
                {
                    targets = _epiElement.SkuItemIds(targetEntity, DeleteUtilConfig);

                    if (DeleteUtilConfig.UseThreeLevelsInCommerce)
                    {
                        targets.Add(targetEntity.Id.ToString(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    targets.Add(targetEntity.Id.ToString(CultureInfo.InvariantCulture));
                }

                linkEntityIds = _epiApi.GetLinkEntityAssociationsForEntity(linkTypeId, channelEntity.Id, channelEntity, DeleteUtilConfig, pars, targets);

                linkEntityIds.RemoveAll(i => newEntityNodes.Any(n => i == _channelPrefixHelper.GetEPiCodeWithChannelPrefix(n.ParentId, DeleteUtilConfig)));
            }

            // Add the removed entity element together with all the underlying entity elements
            List<XElement> elementList = new List<XElement>();
            foreach (StructureEntity existingEntity in existingEntities)
            {
                XElement copyOfElement = new XElement(existingEntity.Type + "_" + existingEntity.EntityId);
                if (elementList.All(p => p.Name.LocalName != copyOfElement.Name.LocalName))
                {
                    elementList.Add(copyOfElement);
                }

                if (DeleteUtilConfig.ChannelEntities.ContainsKey(existingEntity.EntityId))
                {
                    foreach (Link outboundLinks in DeleteUtilConfig.ChannelEntities[existingEntity.EntityId].OutboundLinks)
                    {
                        XElement copyOfDescendant = new XElement(outboundLinks.Target.EntityType.Id + "_" + outboundLinks.Target.Id);
                        if (elementList.All(p => p.Name.LocalName != copyOfDescendant.Name.LocalName))
                        {
                            elementList.Add(copyOfDescendant);
                        }
                    }
                }
            }

            List<XElement> updatedElements = elementList;

            foreach (XElement element in updatedElements)
            {
                string elementEntityType = element.Name.LocalName.Split('_')[0];
                string elementEntityId = element.Name.LocalName.Split('_')[1];

                Dictionary<string, bool> shouldExsistInChannelNodes = channelHelper.ShouldEntityExistInChannelNodes(int.Parse(elementEntityId), channelNodes, channelEntity.Id);

                if (elementEntityType == "Link")
                {
                    continue;
                }

                if (elementEntityType == "Item" && DeleteUtilConfig.ItemsToSkus)
                {
                    Entity deletedEntity = null;

                    try
                    {
                        deletedEntity = _context.ExtensionManager.DataService.GetEntity(
                            int.Parse(elementEntityId),
                            LoadLevel.DataOnly);
                    }
                    catch (Exception ex)
                    {
                        _context.Log(LogLevel.Warning, "Error when getting entity:" + ex);
                    }

                    if (deletedEntity != null)
                    {
                        List<XElement> skus = _epiElement.GenerateSkuItemElemetsFromItem(deletedEntity, DeleteUtilConfig);
                        foreach (XElement sku in skus)
                        {
                            XElement skuCode = sku.Element("Code");
                            if (skuCode != null && !entitiesToUpdate.ContainsKey(skuCode.Value))
                            {
                                entitiesToUpdate.Add(skuCode.Value, shouldExsistInChannelNodes);
                            }
                        }
                    }

                    if (!DeleteUtilConfig.UseThreeLevelsInCommerce)
                    {
                        continue;
                    }
                }

                if (!entitiesToUpdate.ContainsKey(elementEntityId))
                {
                    entitiesToUpdate.Add(elementEntityId, shouldExsistInChannelNodes);
                }
            }

            List<string> parents = new List<string> { parentEnt.Id.ToString(CultureInfo.InvariantCulture) };
            if (parentEnt.EntityType.Id == "Item")
            {
                if (DeleteUtilConfig.ItemsToSkus)
                {
                    parents = _epiElement.SkuItemIds(parentEnt, DeleteUtilConfig);

                    if (DeleteUtilConfig.UseThreeLevelsInCommerce)
                    {
                        parents.Add(parentEnt.Id.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            XDocument updateXml = new XDocument(new XElement("xml", new XAttribute("action", "updated")));
            if (updateXml.Root != null)
            {
                List<XElement> parentElements = channelHelper.GetParentXElements(parentEnt, DeleteUtilConfig);
                foreach (var parentElement in parentElements)
                {
                    updateXml.Root.Add(parentElement);
                }
            }

            foreach (KeyValuePair<string, Dictionary<string, bool>> entityIdToUpdate in entitiesToUpdate)
            {
                foreach (string parentId in parents)
                {
                    _epiApi.UpdateEntryRelations(entityIdToUpdate.Key, channelEntity.Id, channelEntity, DeleteUtilConfig, parentId, entityIdToUpdate.Value, linkTypeId, linkEntityIds);
                }

                updateXml.Root?.Add(new XElement("entry", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(entityIdToUpdate.Key, DeleteUtilConfig)));
            }


            if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, updateXml, DeleteUtilConfig, folderDateTime))
            {
                _context.Log(LogLevel.Information, "Failed to zip and upload the catalog file to azure from delete utility DeleteEntityThatStillExistInChannel() method");
            }

            _context.Log(LogLevel.Debug, "catalog saved");
        }

        private void DeleteResource(Entity targetEntity, Entity parentEnt, string channelIdentifier, string folderDateTime, string resourceZipFile)
        {
            var resource = new Resources(_context);
            XDocument doc = resource.HandleResourceUnlink(targetEntity, parentEnt, DeleteUtilConfig);
            DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Resources, doc, DeleteUtilConfig, folderDateTime/*, _context*/);

            if (DeleteUtilConfig.ActivePublicationMode.Equals(PublicationMode.Automatic))
            {
                _context.Log(LogLevel.Debug, "Starting automatic import!");

                if (_epiApi.StartAssetImportIntoEpiServerCommerce(
                                    DeleteUtilConfig.ResourceNameInCloud, /*Path.Combine(this.DeleteUtilConfig.ResourcesRootPath, folderDateTime, "Resources.xml")*/
                                    Path.Combine(DeleteUtilConfig.ResourcesRootPath, folderDateTime),
                                    DeleteUtilConfig))
                {
                    _epiApi.SendHttpPost(DeleteUtilConfig, Path.Combine(DeleteUtilConfig.ResourcesRootPath, folderDateTime, resourceZipFile));
                }
            }
        }

        private void DeleteEntity(Entity channelEntity, int parentEntityId, Entity targetEntity, string linkTypeId, string channelIdentifier, string folderDateTime, List<int> productParentIds = null)
        {
            var channelHelper = new ChannelHelper(_context);

            XElement removedElement = new XElement(targetEntity.EntityType.Id + "_" + targetEntity.Id);

            List<XElement> deletedElements = new List<XElement>();


            deletedElements.Add(removedElement);

            XDocument deleteXml = new XDocument(new XElement("xml", new XAttribute("action", "deleted")));
            Entity parentEntity = _context.ExtensionManager.DataService.GetEntity(parentEntityId, LoadLevel.DataOnly);

            if (parentEntity != null && !DeleteUtilConfig.ChannelEntities.ContainsKey(parentEntity.Id))
            {
                DeleteUtilConfig.ChannelEntities.Add(parentEntity.Id, parentEntity);
            }

            List<XElement> parentElements = channelHelper.GetParentXElements(parentEntity, DeleteUtilConfig);
            foreach (var parentElement in parentElements)
            {
                deleteXml.Root?.Add(parentElement);
            }

            deletedElements = deletedElements.GroupBy(elem => elem.Name.LocalName).Select(grp => grp.First()).ToList();

            var resources = new Resources(_context);

            foreach (XElement deletedElement in deletedElements)
            {
                if (!deletedElement.Name.LocalName.Contains('_'))
                {
                    continue;
                }

                string deletedElementEntityType = deletedElement.Name.LocalName.Split('_')[0];
                int deletedElementEntityId;
                int.TryParse(deletedElement.Name.LocalName.Split('_')[1], out deletedElementEntityId);

                if (deletedElementEntityType == "Link")
                {
                    continue;
                }

                List<string> deletedResources = new List<string>();

                switch (deletedElementEntityType)
                {
                    case "Channel":
                        _epiApi.DeleteCatalog(deletedElementEntityId, DeleteUtilConfig);
                        deletedResources = channelHelper.GetResourceIds(deletedElement, DeleteUtilConfig);
                        break;
                    case "ChannelNode":
                        _epiApi.DeleteCatalogNode(deletedElementEntityId, channelEntity.Id, DeleteUtilConfig);

                        deleteXml.Root?.Add(new XElement("entry", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(deletedElementEntityId, DeleteUtilConfig)));

                        Entity channelNode = targetEntity.Id == deletedElementEntityId
                                                 ? targetEntity
                                                 : _context.ExtensionManager.DataService.GetEntity(
                                                     deletedElementEntityId,
                                                     LoadLevel.DataAndLinks);

                        if (channelNode == null)
                        {
                            break;
                        }

                        if (deletedElement.Elements().Any())
                        {
                            foreach (XElement linkElement in deletedElement.Elements())
                            {
                                foreach (XElement entityElement in linkElement.Elements())
                                {
                                    string elementEntityId = entityElement.Name.LocalName.Split('_')[1];

                                    Entity child = _context.ExtensionManager.DataService.GetEntity(int.Parse(elementEntityId), LoadLevel.DataAndLinks);
                                    Delete(channelEntity, targetEntity.Id, child, linkTypeId);
                                }
                            }
                        }
                        else
                        {
                            foreach (Link link in targetEntity.OutboundLinks)
                            {
                                Entity child = _context.ExtensionManager.DataService.GetEntity(link.Target.Id, LoadLevel.DataAndLinks);

                                Delete(channelEntity, targetEntity.Id, child, link.LinkType.Id);
                            }
                        }

                        deletedResources = channelHelper.GetResourceIds(deletedElement, DeleteUtilConfig);
                        break;
                    case "Item":
                        deletedResources = channelHelper.GetResourceIds(deletedElement, DeleteUtilConfig);
                        if ((DeleteUtilConfig.ItemsToSkus && DeleteUtilConfig.UseThreeLevelsInCommerce) || !DeleteUtilConfig.ItemsToSkus)
                        {
                            _epiApi.DeleteCatalogEntry(deletedElementEntityId.ToString(CultureInfo.InvariantCulture), DeleteUtilConfig);

                            deleteXml.Root?.Add(new XElement("entry", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(deletedElementEntityId, DeleteUtilConfig)));
                        }

                        if (DeleteUtilConfig.ItemsToSkus)
                        {
                            // delete skus if exist
                            List<string> entitiesToDelete = new List<string>();

                            Entity deletedEntity = null;

                            try
                            {
                                deletedEntity = _context.ExtensionManager.DataService.GetEntity(
                                    deletedElementEntityId,
                                    LoadLevel.DataOnly);
                            }
                            catch (Exception ex)
                            {
                                _context.Log(LogLevel.Warning, "Error when getting entity:" + ex);
                            }

                            if (deletedEntity != null)
                            {
                                List<XElement> skus = _epiElement.GenerateSkuItemElemetsFromItem(deletedEntity, DeleteUtilConfig);

                                foreach (XElement sku in skus)
                                {
                                    XElement skuCodElement = sku.Element("Code");
                                    if (skuCodElement != null)
                                    {
                                        entitiesToDelete.Add(skuCodElement.Value);
                                    }
                                }
                            }

                            foreach (string entityIdToDelete in entitiesToDelete)
                            {
                                _epiApi.DeleteCatalogEntry(entityIdToDelete, DeleteUtilConfig);

                                deleteXml.Root?.Add(new XElement("entry", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(entityIdToDelete, DeleteUtilConfig)));
                            }
                        }

                        break;
                    case "Resource":
                        deletedResources = new List<string> { _channelPrefixHelper.GetEPiCodeWithChannelPrefix(deletedElementEntityId, DeleteUtilConfig) };
                        break;

                    case "Product":
                        _epiApi.DeleteCatalogEntry(deletedElementEntityId.ToString(CultureInfo.InvariantCulture), DeleteUtilConfig);
                        deletedResources = channelHelper.GetResourceIds(deletedElement, DeleteUtilConfig);

                        deleteXml.Root?.Add(new XElement("entry", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(deletedElementEntityId, DeleteUtilConfig)));

                        Entity delEntity = _context.ExtensionManager.DataService.GetEntity(
                            deletedElementEntityId,
                            LoadLevel.DataAndLinks);

                        if (delEntity == null)
                        {
                            break;
                        }

                        foreach (Link link in delEntity.OutboundLinks)
                        {
                            if (link.Target.EntityType.Id == "Product")
                            {
                                if (productParentIds != null && productParentIds.Contains(link.Target.Id))
                                {
                                    _context.Log(LogLevel.Information, string.Format("Entity with id {0} has already been deleted, break the chain to avoid circular relations behaviors (deadlocks)", link.Target.Id));
                                    continue;
                                }

                                if (productParentIds == null)
                                {
                                    productParentIds = new List<int>();
                                }

                                productParentIds.Add(delEntity.Id);
                            }

                            Entity child = _context.ExtensionManager.DataService.GetEntity(link.Target.Id, LoadLevel.DataAndLinks);

                            Delete(channelEntity, delEntity.Id, child, link.LinkType.Id, productParentIds);
                        }

                        break;
                    default:

                        _epiApi.DeleteCatalogEntry(deletedElementEntityId.ToString(CultureInfo.InvariantCulture), DeleteUtilConfig);
                        deletedResources = channelHelper.GetResourceIds(deletedElement, DeleteUtilConfig);

                        deleteXml.Root?.Add(new XElement("entry", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(deletedElementEntityId, DeleteUtilConfig)));

                        Entity prodEntity;
                        if (targetEntity.Id == deletedElementEntityId)
                        {
                            prodEntity = targetEntity;
                        }
                        else
                        {
                            prodEntity = _context.ExtensionManager.DataService.GetEntity(
                                deletedElementEntityId,
                                LoadLevel.DataAndLinks);
                        }

                        if (prodEntity == null)
                        {
                            break;
                        }

                        foreach (Link link in prodEntity.OutboundLinks)
                        {
                            if (link.Target.EntityType.Id == "Product")
                            {
                                if (productParentIds != null && productParentIds.Contains(link.Target.Id))
                                {
                                    _context.Log(LogLevel.Information, string.Format("Entity with id {0} has already been deleted, break the chain to avoid circular relations behaviors (deadlocks)", link.Target.Id));
                                    continue;
                                }

                                if (productParentIds == null)
                                {
                                    productParentIds = new List<int>();
                                }

                                productParentIds.Add(prodEntity.Id);
                            }

                            Entity child = _context.ExtensionManager.DataService.GetEntity(link.Target.Id, LoadLevel.DataAndLinks);

                            Delete(channelEntity, parentEntityId, child, link.LinkType.Id);
                        }

                        break;
                }

                foreach (string resourceId in deletedResources)
                {
                    string resourceIdWithoutPrefix = resourceId.Substring(DeleteUtilConfig.ChannelIdPrefix.Length);

                    int resourceIdAsInt;

                    if (Int32.TryParse(resourceIdWithoutPrefix, out resourceIdAsInt))
                    {
                        if (_context.ExtensionManager.ChannelService.EntityExistsInChannel(channelEntity.Id, resourceIdAsInt))
                        {
                            deletedResources.Remove(resourceId);
                        }
                    }
                }

                if (deletedResources != null && deletedResources.Count != 0)
                {
                    XDocument resDoc = resources.HandleResourceDelete(deletedResources);
                    string folderDateTime2 = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");

                    string zipFileDelete = string.Format(
                        "resource_{0}{1}.zip",
                        folderDateTime2,
                        deletedElementEntityId);

                    DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Resources, resDoc, DeleteUtilConfig, folderDateTime2);
                    string zipDeleteFileNameInCloud = DeleteUtilConfig.ResourceNameInCloud;

                    foreach (string resourceIdString in deletedResources)
                    {
                        int resourceId = int.Parse(resourceIdString);
                        bool sendUnlinkResource = false;
                        string zipFileUnlink = string.Empty;
                        string zipUnlinkFileNameInCloud = string.Empty;

                        Entity resource = _context.ExtensionManager.DataService.GetEntity(resourceId, LoadLevel.DataOnly);
                        if (resource != null)
                        {
                            // Only do this when removing an link (unlink)
                            Entity parentEnt = _context.ExtensionManager.DataService.GetEntity(parentEntityId, LoadLevel.DataOnly);
                            var unlinkDoc = resources.HandleResourceUnlink(resource, parentEnt, DeleteUtilConfig);

                            zipFileUnlink = string.Format("resource_{0}{1}.zip", folderDateTime, deletedElementEntityId);
                            DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Resources, unlinkDoc, DeleteUtilConfig, folderDateTime);
                            zipUnlinkFileNameInCloud = DeleteUtilConfig.ResourceNameInCloud;
                        }

                        _context.Log(LogLevel.Debug, "Resources saved!");

                        if (DeleteUtilConfig.ActivePublicationMode.Equals(PublicationMode.Automatic))
                        {
                            _context.Log(LogLevel.Debug, "Starting automatic import!");

                            if (sendUnlinkResource && _epiApi.StartAssetImportIntoEpiServerCommerce(
                                                                    zipUnlinkFileNameInCloud/*Path.Combine(this.DeleteUtilConfig.ResourcesRootPath, folderDateTime, "Resources.xml")*/,
                                                                    Path.Combine(DeleteUtilConfig.ResourcesRootPath, folderDateTime),
                                                                    DeleteUtilConfig))
                            {
                                _epiApi.SendHttpPost(DeleteUtilConfig, Path.Combine(DeleteUtilConfig.ResourcesRootPath, folderDateTime, zipFileUnlink));
                            }

                            if (_epiApi.StartAssetImportIntoEpiServerCommerce(
                                            zipDeleteFileNameInCloud/*Path.Combine(this.DeleteUtilConfig.ResourcesRootPath, folderDateTime2, "Resources.xml")*/,
                                            Path.Combine(DeleteUtilConfig.ResourcesRootPath, folderDateTime2),
                                            DeleteUtilConfig))
                            {
                                _epiApi.SendHttpPost(DeleteUtilConfig, Path.Combine(DeleteUtilConfig.ResourcesRootPath, folderDateTime2, zipFileDelete));
                            }
                        }
                    }
                }
            }

            if (deleteXml.Root != null && deleteXml.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "entry") != null)
            {

                if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, deleteXml, DeleteUtilConfig, folderDateTime))
                {
                    _context.Log(LogLevel.Information, "Failed to zip and upload the catalog file to azure from delete utility DeleteEntity() method");
                }

                _context.Log(LogLevel.Debug, "catalog saved");
            }
        }

        public void DeleteLinkEntity(Entity channelEntity, int linkEntityId)
        {
            XDocument deleteXml = new XDocument(new XElement("xml", new XAttribute("action", "deleted")));

            var channelHelper = new ChannelHelper(_context);

            if (deleteXml.Root != null && deleteXml.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "entry") != null)
            {
                string channelIdentifier = channelHelper.GetChannelIdentifier(channelEntity);
                string folderDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss.fff");


                if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, deleteXml, DeleteUtilConfig, folderDateTime))
                {
                    _context.Log(LogLevel.Information, "Failed to zip and upload the catalog file to azure from delete utility DeleteLinkEntity() method");
                }
                _context.Log(LogLevel.Debug, "catalog saved");
            }
        }
    }
}
