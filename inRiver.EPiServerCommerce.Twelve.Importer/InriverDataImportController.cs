using Microsoft.WindowsAzure.Storage.Auth;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;
using EPiServer;
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Commerce.SpecializedProperties;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Framework.Blobs;
using EPiServer.Logging;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using inRiver.EPiServerCommerce.Interfaces;
using inRiver.EPiServerCommerce.Twelve.Importer.Services;
using inRiver.EPiServerCommernce.Twelve.Importer.ResourceModels;
using Mediachase.Commerce.Assets;
using Mediachase.Commerce.Catalog;
using Mediachase.Commerce.Catalog.Dto;
using Mediachase.Commerce.Catalog.ImportExport;
using Mediachase.Commerce.Catalog.Managers;
using Mediachase.Commerce.Catalog.Objects;
using Microsoft.WindowsAzure.Storage;

namespace inRiver.EPiServerCommerce.Twelve.Importer
{
    public class InriverDataImportController : SecuredApiController
    {
        private static readonly ILogger log = LogManager.GetLogger(typeof(InriverDataImportController));

        private readonly AzureFileManager azureFileManager = AzureFileManager.Instance;

        private readonly IContentRepository contentRepository;
        private readonly ICatalogSystem catalogSystem;
        private readonly IPermanentLinkMapper permanentLinkMapper;
        private readonly ReferenceConverter referenceConverter;
        private readonly IMedataDataService metaDataService;

        private static ZipArchive resourceArchive;

        private static string resourceZipFileNameInCloud;


        public InriverDataImportController(IContentRepository contentRepository, ICatalogSystem catalogSystem, IPermanentLinkMapper permanentLinkMapper, ReferenceConverter referenceConverter, IMedataDataService metaDataService)
        {
            this.contentRepository = contentRepository;
            this.catalogSystem = catalogSystem;
            this.permanentLinkMapper = permanentLinkMapper;
            this.referenceConverter = referenceConverter;
            this.metaDataService = metaDataService;
        }

        private bool RunICatalogImportHandlers
        {
            get
            {
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    string setting = ConfigurationManager.AppSettings["inRiver.RunICatalogImportHandlers"];
                    if (setting != null)
                    {
                        bool result;
                        if (bool.TryParse(setting, out result))
                        {
                            return result;
                        }

                        return true;
                    }

                    return true;
                }

                return true;
            }
        }

        private bool RunIResourceImporterHandlers
        {
            get
            {
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    string setting = ConfigurationManager.AppSettings["inRiver.RunIResourceImporterHandlers"];
                    if (setting != null)
                    {
                        bool result;
                        if (bool.TryParse(setting, out result))
                        {
                            return result;
                        }

                        return true;
                    }

                    return true;
                }

                return true;
            }
        }

        private string GetLocalFilePath
        {
            get
            {
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    string setting = ConfigurationManager.AppSettings["inRiver.LocalFilePath"];
                    if (setting != null)
                    {
                        return setting;
                    }
                    return string.Empty;
                }
                return string.Empty;
            }
        }

        private bool RunIDeleteActionsHandlers
        {
            get
            {
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    string setting = ConfigurationManager.AppSettings["inRiver.RunIDeleteActionsHandlers"];
                    if (setting != null)
                    {
                        bool result;
                        if (bool.TryParse(setting, out result))
                        {
                            return result;
                        }

                        return true;
                    }

                    return true;
                }

                return true;
            }
        }

        private bool RunIInRiverEventsHandlers
        {
            get
            {
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    string setting = ConfigurationManager.AppSettings["inRiver.RunIInRiverEventsHandlers"];
                    if (setting != null)
                    {
                        bool result;
                        if (bool.TryParse(setting, out result))
                        {
                            return result;
                        }

                        return true;
                    }

                    return true;
                }

                return true;
            }
        }

        [HttpGet]
        public string IsImporting()
        {
            log.Debug("IsImporting");

            if (Singleton.Instance.IsImporting)
            {
                return "importing";
            }

            return Singleton.Instance.Message;
        }

        [HttpPost]
        public bool DeleteCatalogEntry([FromBody] string catalogEntryId)
        {
            log.Debug("DeleteCatalogEntry");
            List<IDeleteActionsHandler> importerHandlers = ServiceLocator.Current.GetAllInstances<IDeleteActionsHandler>().ToList();
            int entryId, metaClassId, catalogId;

            try
            {
                Entry entry = CatalogContext.Current.GetCatalogEntry(catalogEntryId);
                if (entry == null)
                {
                    string errMess = string.Format("Could not find catalog entry with id: {0}. No entry is deleted", catalogEntryId);
                    log.Error(errMess);
                    return false;
                }

                entryId = entry.CatalogEntryId;
                metaClassId = entry.MetaClassId;
                catalogId = entry.CatalogId;

                if (RunIDeleteActionsHandlers)
                {
                    foreach (IDeleteActionsHandler handler in importerHandlers)
                    {
                        handler.PreDeleteCatalogEntry(entryId, metaClassId, catalogId);
                    }
                }

                CatalogContext.Current.DeleteCatalogEntry(entry.CatalogEntryId, false);
            }
            catch (Exception ex)
            {
                string errMess = string.Format("Could not delete catalog entry with id: {0}, Exception:{1}", catalogEntryId, ex);
                log.Error(errMess);
                return false;
            }

            if (RunIDeleteActionsHandlers)
            {
                foreach (IDeleteActionsHandler handler in importerHandlers)
                {
                    handler.PostDeleteCatalogEntry(entryId, metaClassId, catalogId);
                }
            }

            return true;
        }

        [HttpPost]
        public bool DeleteCatalog([FromBody] int catalogId)
        {
            log.Debug("DeleteCatalog");
            List<IDeleteActionsHandler> importerHandlers = ServiceLocator.Current.GetAllInstances<IDeleteActionsHandler>().ToList();

            if (RunIDeleteActionsHandlers)
            {
                foreach (IDeleteActionsHandler handler in importerHandlers)
                {
                    handler.PreDeleteCatalog(catalogId);
                }
            }

            try
            {
                CatalogContext.Current.DeleteCatalog(catalogId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Could not delete catalog with id: {0}", catalogId), ex);
                return false;
            }

            if (RunIDeleteActionsHandlers)
            {
                foreach (IDeleteActionsHandler handler in importerHandlers)
                {
                    handler.PostDeleteCatalog(catalogId);
                }
            }

            return true;
        }

        [HttpPost]
        public bool DeleteCatalogNode([FromBody] string catalogNodeId)
        {
            log.Debug("DeleteCatalogNode");
            List<IDeleteActionsHandler> importerHandlers = ServiceLocator.Current.GetAllInstances<IDeleteActionsHandler>().ToList();
            int catalogId;
            int nodeId;
            try
            {
                CatalogNode cn = CatalogContext.Current.GetCatalogNode(catalogNodeId);
                if (cn == null || cn.CatalogNodeId == 0)
                {
                    string errMess = string.Format("Could not find catalog node with id: {0}. No node is deleted", catalogNodeId);
                    log.Error(errMess);
                    return false;
                }

                catalogId = cn.CatalogId;
                nodeId = cn.CatalogNodeId;
                if (RunIDeleteActionsHandlers)
                {
                    foreach (IDeleteActionsHandler handler in importerHandlers)
                    {
                        handler.PreDeleteCatalogNode(nodeId, catalogId);
                    }
                }

                CatalogContext.Current.DeleteCatalogNode(cn.CatalogNodeId, cn.CatalogId);
            }
            catch (Exception ex)
            {
                string errMess = string.Format("Could not delete catalogNode with id: {0}, Exception:{1}", catalogNodeId, ex);
                log.Error(errMess);
                return false;
            }

            if (RunIDeleteActionsHandlers)
            {
                foreach (IDeleteActionsHandler handler in importerHandlers)
                {
                    handler.PostDeleteCatalogNode(nodeId, catalogId);
                }
            }

            return true;
        }

        [HttpPost]
        public bool CheckAndMoveNodeIfNeeded([FromBody] string catalogNodeId)
        {
            log.Debug("CheckAndMoveNodeIfNeeded");
            try
            {
                CatalogNodeDto nodeDto = CatalogContext.Current.GetCatalogNodeDto(catalogNodeId);
                if (nodeDto.CatalogNode.Count > 0)
                {
                    // Node exists
                    if (nodeDto.CatalogNode[0].ParentNodeId != 0)
                    {
                        MoveNode(nodeDto.CatalogNode[0].Code, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                string errMess = string.Format("Could not CheckAndMoveNodeIfNeeded for catalogNode with id: {0}, Exception:{1}", catalogNodeId, ex);
                log.Error(errMess);
                return false;
            }

            return true;
        }

        [HttpPost]
        public bool UpdateLinkEntityData(LinkEntityUpdateData linkEntityUpdateData)
        {
            int catalogId = FindCatalogByName(linkEntityUpdateData.ChannelName);

            try
            {
                CatalogAssociationDto associationsDto2 = CatalogContext.Current.GetCatalogAssociationDtoByEntryCode(catalogId, linkEntityUpdateData.ParentEntryId);
                foreach (CatalogAssociationDto.CatalogEntryAssociationRow row in associationsDto2.CatalogEntryAssociation)
                {
                    if (row.CatalogAssociationRow.AssociationDescription == linkEntityUpdateData.LinkEntityIdString)
                    {
                        row.BeginEdit();
                        row.CatalogAssociationRow.AssociationName = linkEntityUpdateData.LinkEntryDisplayName;
                        row.AcceptChanges();
                    }
                }

                CatalogContext.Current.SaveCatalogAssociation(associationsDto2);
                return true;
            }
            catch (Exception ex)
            {
                string errMess = string.Format("Could not update LinkEntityData for entity with id:{0}, Exception:{1}", linkEntityUpdateData.LinkEntityIdString, ex);
                log.Error(errMess);
                return false;
            }
        }

        [HttpPost]
        public bool UpdateEntryRelations(UpdateEntryRelationData updateEntryRelationData)
        {
            try
            {
                int catalogId = FindCatalogByName(updateEntryRelationData.ChannelName);
                CatalogEntryDto ced = CatalogContext.Current.GetCatalogEntryDto(updateEntryRelationData.CatalogEntryIdString);
                CatalogEntryDto ced2 = CatalogContext.Current.GetCatalogEntryDto(updateEntryRelationData.ParentEntryId);
                string debugMess = string.Format("UpdateEntryRelations called for catalog {0} between {1} and {2}", catalogId, updateEntryRelationData.ParentEntryId, updateEntryRelationData.CatalogEntryIdString);
                log.Debug(debugMess);
                // See if channelnode
                CatalogNodeDto nodeDto = CatalogContext.Current.GetCatalogNodeDto(updateEntryRelationData.CatalogEntryIdString);
                if (nodeDto.CatalogNode.Count > 0)
                {
                    string debugMessage = string.Format("found {0} as a catalog node", updateEntryRelationData.CatalogEntryIdString);
                    log.Debug(debugMessage);
                    CatalogRelationDto rels = CatalogContext.Current.GetCatalogRelationDto(
                        catalogId,
                        nodeDto.CatalogNode[0].CatalogNodeId,
                        0,
                        string.Empty,
                        new CatalogRelationResponseGroup(CatalogRelationResponseGroup.ResponseGroup.CatalogNode));

                    foreach (CatalogRelationDto.CatalogNodeRelationRow row in rels.CatalogNodeRelation)
                    {
                        CatalogNode parentCatalogNode = CatalogContext.Current.GetCatalogNode(row.ParentNodeId);
                        if (updateEntryRelationData.RemoveFromChannelNodes.Contains(parentCatalogNode.ID))
                        {
                            row.Delete();
                            updateEntryRelationData.RemoveFromChannelNodes.Remove(parentCatalogNode.ID);
                        }
                    }

                    if (rels.HasChanges())
                    {
                        log.Debug("Relations between nodes has been changed, saving new catalog releations");
                        CatalogContext.Current.SaveCatalogRelationDto(rels);
                    }

                    CatalogNode parentNode = null;
                    if (nodeDto.CatalogNode[0].ParentNodeId != 0)
                    {
                        parentNode = CatalogContext.Current.GetCatalogNode(nodeDto.CatalogNode[0].ParentNodeId);
                    }

                    if ((updateEntryRelationData.RemoveFromChannelNodes.Contains(updateEntryRelationData.ChannelIdEpified) && nodeDto.CatalogNode[0].ParentNodeId == 0)
                        || (parentNode != null && updateEntryRelationData.RemoveFromChannelNodes.Contains(parentNode.ID)))
                    {
                        CatalogNode associationNode = CatalogContext.Current.GetCatalogNode(updateEntryRelationData.InRiverAssociationsEpified);

                        MoveNode(nodeDto.CatalogNode[0].Code, associationNode.CatalogNodeId);
                    }
                }

                if (ced.CatalogEntry.Count <= 0)
                {
                    string noCatMess = string.Format("No catalog entry with id {0} found, will not continue.", updateEntryRelationData.CatalogEntryIdString);
                    log.Debug(noCatMess);
                    return true;
                }

                if (updateEntryRelationData.RemoveFromChannelNodes.Count > 0)
                {
                    string nodeMess = string.Format("Look for removal from channel nodes, nr of possible nodes: {0}", updateEntryRelationData.RemoveFromChannelNodes.Count);
                    log.Debug(nodeMess);
                    CatalogRelationDto rel = CatalogContext.Current.GetCatalogRelationDto(catalogId, 0, ced.CatalogEntry[0].CatalogEntryId, string.Empty, new CatalogRelationResponseGroup(CatalogRelationResponseGroup.ResponseGroup.NodeEntry));

                    foreach (CatalogRelationDto.NodeEntryRelationRow row in rel.NodeEntryRelation)
                    {
                        CatalogNode catalogNode = CatalogContext.Current.GetCatalogNode(row.CatalogNodeId);
                        if (updateEntryRelationData.RemoveFromChannelNodes.Contains(catalogNode.ID))
                        {
                            row.Delete();
                        }
                    }

                    if (rel.HasChanges())
                    {
                        log.Debug("Relations between entries has been changed, saving new catalog releations");
                        CatalogContext.Current.SaveCatalogRelationDto(rel);
                    }
                }
                else
                {
                    string removeMess = string.Format(string.Format("{0} shall not be removed from node {1}", updateEntryRelationData.CatalogEntryIdString, updateEntryRelationData.ParentEntryId));
                    log.Debug(removeMess);
                }

                if (ced2.CatalogEntry.Count <= 0)
                {
                    return true;
                }

                if (!updateEntryRelationData.ParentExistsInChannelNodes)
                {
                    if (updateEntryRelationData.IsRelation)
                    {
                        log.Debug("Checking other relations");
                        CatalogRelationDto rel3 = CatalogContext.Current.GetCatalogRelationDto(catalogId, 0, ced2.CatalogEntry[0].CatalogEntryId, string.Empty, new CatalogRelationResponseGroup(CatalogRelationResponseGroup.ResponseGroup.CatalogEntry));
                        foreach (CatalogRelationDto.CatalogEntryRelationRow row in rel3.CatalogEntryRelation)
                        {
                            Entry childEntry = CatalogContext.Current.GetCatalogEntry(row.ChildEntryId);
                            if (childEntry.ID == updateEntryRelationData.CatalogEntryIdString)
                            {
                                string relationMess = string.Format("Relations between entries {0} and {1} has been removed, saving new catalog releations", row.ParentEntryId, row.ChildEntryId);
                                log.Debug(relationMess);
                                row.Delete();
                                CatalogContext.Current.SaveCatalogRelationDto(rel3);
                                break;
                            }
                        }
                    }
                    else
                    {
                        List<int> catalogAssociationIds = new List<int>();
                        log.Debug("Checking other associations");
                        CatalogAssociationDto associationsDto = CatalogContext.Current.GetCatalogAssociationDtoByEntryCode(catalogId, updateEntryRelationData.ParentEntryId);
                        foreach (CatalogAssociationDto.CatalogEntryAssociationRow row in associationsDto.CatalogEntryAssociation)
                        {
                            if (row.AssociationTypeId == updateEntryRelationData.LinkTypeId)
                            {
                                Entry childEntry = CatalogContext.Current.GetCatalogEntry(row.CatalogEntryId);
                                if (childEntry.ID == updateEntryRelationData.CatalogEntryIdString)
                                {
                                    if (updateEntryRelationData.LinkEntityIdsToRemove.Count == 0 || updateEntryRelationData.LinkEntityIdsToRemove.Contains(row.CatalogAssociationRow.AssociationDescription))
                                    {
                                        catalogAssociationIds.Add(row.CatalogAssociationId);
                                        string associationMess = string.Format("Removing association for {0}", row.CatalogEntryId);
                                        log.Debug(associationMess);
                                        row.Delete();
                                    }
                                }
                            }
                        }

                        if (associationsDto.HasChanges())
                        {
                            log.Debug("Saving updated associations");
                            CatalogContext.Current.SaveCatalogAssociation(associationsDto);
                        }

                        if (catalogAssociationIds.Count > 0)
                        {
                            foreach (int catalogAssociationId in catalogAssociationIds)
                            {
                                associationsDto = CatalogContext.Current.GetCatalogAssociationDtoByEntryCode(catalogId, updateEntryRelationData.ParentEntryId);
                                if (associationsDto.CatalogEntryAssociation.Count(r => r.CatalogAssociationId == catalogAssociationId) == 0)
                                {
                                    foreach (CatalogAssociationDto.CatalogAssociationRow assRow in associationsDto.CatalogAssociation)
                                    {
                                        if (assRow.CatalogAssociationId == catalogAssociationId)
                                        {
                                            assRow.Delete();
                                            string removeAssocMess = string.Format("Removing association with id {0} and sending update.", catalogAssociationId);
                                            log.Debug(removeAssocMess);
                                            CatalogContext.Current.SaveCatalogAssociation(associationsDto);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string warnMess = string.Format("Could not update entry relations catalog with id:{0} Exception:{1}", updateEntryRelationData.CatalogEntryIdString, ex);
                log.Information(warnMess);
                return false;
            }

            return true;
        }

        [HttpPost]
        public List<string> GetLinkEntityAssociationsForEntity(GetLinkEntityAssociationsForEntityData data)
        {
            List<string> ids = new List<string>();
            try
            {
                int catalogId = FindCatalogByName(data.ChannelName);

                foreach (string parentId in data.ParentIds)
                {
                    CatalogAssociationDto associationsDto2 = CatalogContext.Current.GetCatalogAssociationDtoByEntryCode(catalogId, parentId);
                    foreach (CatalogAssociationDto.CatalogEntryAssociationRow row in associationsDto2.CatalogEntryAssociation)
                    {
                        if (row.AssociationTypeId == data.LinkTypeId)
                        {
                            Entry childEntry = CatalogContext.Current.GetCatalogEntry(row.CatalogEntryId);

                            if (data.TargetIds.Contains(childEntry.ID))
                            {
                                if (!ids.Contains(row.CatalogAssociationRow.AssociationDescription))
                                {
                                    ids.Add(row.CatalogAssociationRow.AssociationDescription);
                                }
                            }
                        }
                    }

                    CatalogContext.Current.SaveCatalogAssociation(associationsDto2);
                }
            }
            catch (Exception e)
            {
                string errMess = string.Format("Could not GetLinkEntityAssociationsForEntity for parentIds: {0} exception:{1}", data.ParentIds, e);
                log.Error(errMess);
            }

            return ids;
        }

        [HttpGet]
        public string Get()
        {
            log.Debug("Hello from inRiver!");
            return "Hello from inRiver!";
        }

        [HttpPost]
        public string ImportCatalogXml([FromBody] string path)
        {
            Singleton.Instance.Message = "importing";
            Task importTask = Task.Run(
                () =>
                {
                    try
                    {
                        Singleton.Instance.Message = "importing";
                        Singleton.Instance.IsImporting = true;
                        Stream catalogXmlStream = GetCatalogFromSharedFolderByFileName(path);
                        List<ICatalogImportHandler> catalogImportHandlers =
                            ServiceLocator.Current.GetAllInstances<ICatalogImportHandler>().ToList();
                        if (catalogImportHandlers.Any() && RunICatalogImportHandlers)
                        {
                            ImportCatalogXmlWithHandlers(catalogXmlStream, catalogImportHandlers, path);
                        }
                        else
                        {
                            ImportCatalogXml(catalogXmlStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        Singleton.Instance.IsImporting = false;
                        string errorMess = "Catalog Import Failed for path - " + path + " with exception -" +
                                           ex.ToString();
                        log.Error(errorMess, ex);
                        Singleton.Instance.Message = "ERROR: " + ex.Message;
                    }

                    Singleton.Instance.IsImporting = false;
                    Singleton.Instance.Message = "Import Sucessful";
                });

            if (importTask.Status != TaskStatus.RanToCompletion)
            {
                return "importing";
            }

            return Singleton.Instance.Message;
        }

        [HttpPost]
        public bool ImportResources(InRiverImportResourceInformation resourceInformation)
        {
            if (resourceInformation == null)
            {
                string message = "Resource Import Failed. InRiverImportResourceInformation is null.";
                log.Error(message);

                return false;
            }

            resourceZipFileNameInCloud = resourceInformation.fileNameInCloud;
            List<Interfaces.InRiverImportResource> resources = resourceInformation.resources;
            string logMess = string.Empty;

            if (resources == null)
            {
                logMess = string.Format("Received resource list that is NULL");
                log.Debug(logMess);
                return false;
            }

            List<IInRiverImportResource> resourcesImport = resources.Cast<IInRiverImportResource>().ToList();

            logMess = string.Format("Received list of {0} resources to import", resourcesImport.Count());
            log.Debug(logMess);

            Task importTask = Task.Run(
                () =>
                {
                    try
                    {
                        Singleton.Instance.Message = "importing";
                        Singleton.Instance.IsImporting = true;

                        List<IResourceImporterHandler> importerHandlers =
                            ServiceLocator.Current.GetAllInstances<IResourceImporterHandler>().ToList();
                        if (RunIResourceImporterHandlers)
                        {
                            foreach (IResourceImporterHandler handler in importerHandlers)
                            {
                                log.Debug("Running PreResourceImportHandler " + handler.GetType().FullName);
                                handler.PreImport(resourcesImport);
                            }
                        }
                        
                        foreach (IInRiverImportResource resource in resources)
                        {
                            bool found = false;
                            int count = 0;

                            while (!found && count < 10 && resource.Action != "added")
                            {
                                count++;

                                if (contentRepository.TryGet(EntityIdToGuid(resource.ResourceId), out MediaData existingMediaData))
                                {
                                    found = true;
                                }
                                else
                                {
                                    string expMess = string.Format(
                                    "Waiting ({1}/10) for resource {0} to be ready.",
                                    resource.ResourceId,
                                    count);
                                    log.Debug(expMess);
                                    Thread.Sleep(500);
                                }  
                            }

                            string resourceMess = string.Format(
                                "Working with resource {0} from {1} with action: {2}",
                                resource.ResourceId,
                                resource.Path,
                                resource.Action);

                            log.Debug(resourceMess);

                            if (resource.Action == "added" || resource.Action == "updated")
                            {
                                ImportImageAndAttachToEntry(resource);
                            }
                            else if (resource.Action == "deleted")
                            {
                                string deleteActionMess = string.Format("Got delete action for resource id: {0}.", resource.ResourceId);

                                log.Debug(deleteActionMess);

                                HandleDelete(resource);
                            }
                            else if (resource.Action == "unlinked")
                            {
                                HandleUnlink(resource);
                            }
                            else
                            {
                                string unknownActionMess = string.Format(
                                    "Got unknown action for resource id: {0}, {1}",
                                    resource.ResourceId,
                                    resource.Action);
                                log.Debug(unknownActionMess);
                            }
                        }

                        string importLogMess = string.Format("Imported {0} resources", resources.Count());

                        log.Debug(importLogMess);

                        if (RunIResourceImporterHandlers)
                        {
                            foreach (IResourceImporterHandler handler in importerHandlers)
                            {
                                log.Debug("Running PostResourceImportHandler " + handler.GetType().FullName);
                                handler.PostImport(resourcesImport);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Singleton.Instance.IsImporting = false;
                        string expMess = string.Format("Resource Import Failed, Exception:{0}", ex);
                        log.Error(expMess);
                        Singleton.Instance.Message = "ERROR: " + ex.Message;
                    }

                    Singleton.Instance.Message = "Resource Import successful";
                    Singleton.Instance.IsImporting = false;
                });


            return importTask.Status != TaskStatus.RanToCompletion;
        }

        [HttpPost]
        public bool ImportUpdateCompleted(ImportUpdateCompletedData data)
        {
            try
            {
                if (RunIInRiverEventsHandlers)
                {
                    IEnumerable<IInRiverEventsHandler> eventsHandlers = ServiceLocator.Current.GetAllInstances<IInRiverEventsHandler>();
                    foreach (IInRiverEventsHandler handler in eventsHandlers)
                    {
                        handler.ImportUpdateCompleted(data.CatalogName, data.EventType, data.ResourcesIncluded);
                    }

                    string logMess = string.Format(
                        "*** ImportUpdateCompleted events with parameters CatalogName={0}, EventType={1}, ResourcesIncluded={2}",
                        data.CatalogName,
                        data.EventType,
                        data.ResourcesIncluded);
                    log.Debug(logMess);
                }

                return true;
            }
            catch (Exception exception)
            {
                log.Error(exception.ToString());
                return false;
            }
        }

        [HttpPost]
        public bool DeleteCompleted(DeleteCompletedData data)
        {
            try
            {
                if (RunIInRiverEventsHandlers)
                {
                    IEnumerable<IInRiverEventsHandler> eventsHandlers = ServiceLocator.Current.GetAllInstances<IInRiverEventsHandler>();
                    foreach (IInRiverEventsHandler handler in eventsHandlers)
                    {
                        handler.DeleteCompleted(data.CatalogName, data.EventType);
                    }

                    string logMess = string.Format("*** DeleteCompleted events with parameters CatalogName={0}, EventType={1}", data.CatalogName, data.EventType);
                    log.Debug(logMess);
                }

                return true;
            }
            catch (Exception exception)
            {
                log.Error(exception.ToString());
                return false;
            }
        }

        private static Stream GetResourceByFileName(string resourceName, string filenameInCloudStorage)
        {
            if (resourceArchive == null)
            {
                resourceArchive = GetArchiveFromSharedFolder(filenameInCloudStorage, ConfigurationManager.AppSettings["inRiver.StorageAccountResourcesDirectoryReference"]);
            }

            ZipArchiveEntry resourcEntry = resourceArchive.Entries.First(entry =>
                entry.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));

            return resourcEntry?.Open();

        }

        private static Stream GetCatalogFromSharedFolderByFileName(string filenameInCloudStorage)
        {
            ZipArchive archive = GetArchiveFromSharedFolder(filenameInCloudStorage,
                ConfigurationManager.AppSettings["inRiver.StorageAccountCatalogDirectoryReference"]);
            ZipArchiveEntry xml = archive.Entries.First();

            return xml?.Open();
        }

        private static ZipArchive GetArchiveFromSharedFolder(string filenameInCloudStorage, string directoryInCloudStorage)
        {
            if (string.IsNullOrEmpty(ConfigurationManager.AppSettings["inRiver.StorageAccountName"]) ||
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["inRiver.StorageAccountKey"]) ||
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["inRiver.StorageAccountShareReference"]) ||
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["inRiver.StorageAccountCatalogDirectoryReference"]))
            {
                return null;
            }

            StorageCredentials cred = new StorageCredentials(ConfigurationManager.AppSettings["inRiver.StorageAccountName"], ConfigurationManager.AppSettings["inRiver.StorageAccountKey"]);
            CloudStorageAccount storageAccount = new CloudStorageAccount(cred, true);

            Microsoft.WindowsAzure.Storage.File.CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            Microsoft.WindowsAzure.Storage.File.CloudFileShare share = fileClient.GetShareReference(ConfigurationManager.AppSettings["inRiver.StorageAccountShareReference"]);
            share.CreateIfNotExists();

            Microsoft.WindowsAzure.Storage.File.CloudFileDirectory root = share.GetRootDirectoryReference();
            Microsoft.WindowsAzure.Storage.File.CloudFileDirectory dir = root.GetDirectoryReference(directoryInCloudStorage);
            Microsoft.WindowsAzure.Storage.File.CloudFile cloudFile = dir.GetFileReference(filenameInCloudStorage);

            MemoryStream ms = new MemoryStream();
            cloudFile.DownloadToStream(ms);

            ms.Position = 0;
            return new ZipArchive(ms);
        }

        internal static int FindCatalogByName(string name)
        {
            try
            {
                CatalogDto d = CatalogContext.Current.GetCatalogDto();
                foreach (CatalogDto.CatalogRow catalog in d.Catalog)
                {
                    if (name.Equals(catalog.Name))
                    {
                        return catalog.CatalogId;
                    }
                }

                return -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// Returns a reference to the inRiver Resource folder. It will be created if it
        /// does not already exist.
        /// </summary>
        /// <remarks>
        /// The folder structure will be: /globalassets/inRiver/Resources/...
        /// </remarks>
        protected ContentReference GetInRiverResourceFolder()
        {
            ContentReference rootInRiverFolder =
                ContentFolderCreator.CreateOrGetFolder(SiteDefinition.Current.GlobalAssetsRoot, "inRiver");
            ContentReference resourceRiverFolder =
                ContentFolderCreator.CreateOrGetFolder(rootInRiverFolder, "Resources");
            return resourceRiverFolder;
        }

        private void MoveNode(string nodeCode, int newParent)
        {
            CatalogNodeDto catalogNodeDto = CatalogContext.Current.GetCatalogNodeDto(
                nodeCode,
                new CatalogNodeResponseGroup(CatalogNodeResponseGroup.ResponseGroup.CatalogNodeFull));

            // Move node to new parent
            string logMess = string.Format(string.Format("Move {0} to new parent ({1}).", nodeCode, newParent));
            log.Debug(logMess);
            catalogNodeDto.CatalogNode[0].ParentNodeId = newParent;
            CatalogContext.Current.SaveCatalogNode(catalogNodeDto);
        }

        private void ImportCatalogXml(Stream catalogXmlStream)
        {
            log.Information("Starting importing the xml into EPiServer Commerce.");
            CatalogImportExport cie = new CatalogImportExport();
            cie.ImportExportProgressMessage += ProgressHandler;
            cie.Import(catalogXmlStream, true);
            log.Information("Done importing the xml into EPiServer Commerce.");
        }

        private void ImportCatalogXmlWithHandlers(Stream catalogXml, List<ICatalogImportHandler> catalogImportHandlers, string path)
        {
            // Read catalog xml to allow handlers to work on it
            // NOTE! If it is very large, it might consume alot of memory.
            // The catalog xml import reads in chunks, so we might impose
            // a memory problem here for the really large catalogs.
            // The benefit outweighs the problem.

            try
            {

                XDocument catalogDoc = XDocument.Load(catalogXml);

                if (catalogImportHandlers.Any())
                {
                    foreach (ICatalogImportHandler handler in catalogImportHandlers)
                    {
                        try
                        {
                            string logMess = string.Format("Preimport handler: {0}", handler.GetType().FullName);
                            log.Debug(logMess);
                            handler.PreImport(catalogDoc);
                        }
                        catch (Exception e)
                        {
                            string logMess = string.Format("Failed to run PreImport on " + handler.GetType().FullName, e);
                            log.Error(logMess);
                        }
                    }
                }


                // The handlers might have changed the xml, so we pass it on
                MemoryStream stream = new MemoryStream();
                catalogDoc.Save(stream);
                catalogXml.Dispose();

                catalogDoc = null;
                stream.Position = 0;

                CatalogImportExport cie = new CatalogImportExport();
                cie.ImportExportProgressMessage += ProgressHandler;
                cie.Import(stream, true);

                if (stream.Position > 0)
                {
                    stream.Position = 0;
                }

                catalogDoc = XDocument.Load(stream);
                stream.Dispose();
                //fs.Dispose();

                if (catalogImportHandlers.Any())
                {
                    foreach (ICatalogImportHandler handler in catalogImportHandlers)
                    {
                        try
                        {
                            string logMess = string.Format("Postimport handler: {0}", handler.GetType().FullName);
                            log.Debug(logMess);
                            handler.PostImport(catalogDoc);
                        }
                        catch (Exception e)
                        {
                            string logMess = string.Format("Failed to run PostImport on " + handler.GetType().FullName, e);
                            log.Error(logMess);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                string logMess = string.Format("Error in ImportCatalogXmlWithHandlers", exception);
                log.Error(logMess);
                throw;
            }
        }

        private void ProgressHandler(object source, ImportExportEventArgs args)
        {
            string message = args.Message;
            double progress = args.CompletedPercentage;
            log.Debug(string.Format("{0}", message));
        }

        private void HandleUnlink(IInRiverImportResource inriverResource)
        {
            if (contentRepository.TryGet(EntityIdToGuid(inriverResource.ResourceId), out MediaData mediaData))
            {
                foreach (EntryCode entryCode in inriverResource.EntryCodes)
                {
                    ContentReference contentReference = referenceConverter.GetContentLink(entryCode.Code);

                    if (contentRepository.TryGet(contentReference, out ProductContent product))
                    {
                        CommerceMedia assetToRemove = product.CommerceMediaCollection.First(asset => asset.AssetLink == mediaData.ContentLink);

                        ProductContent clone = product.CreateWritableClone<ProductContent>();

                        log.Information($"Removing asset with id {assetToRemove.AssetLink.ID} from product {product.Name}");

                        clone.CommerceMediaCollection.Remove(assetToRemove);
                        int count = 0;

                        foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection.OrderBy(asset => asset.SortOrder))
                        {
                            commerceMedia.SortOrder = count;
                            count++;
                        }

                        contentRepository.Save(clone, SaveAction.Publish, AccessLevel.NoAccess);
                    }
                    else if (contentRepository.TryGet(contentReference, out NodeContent node))
                    {
                        CommerceMedia assetToRemove = node.CommerceMediaCollection.First(asset => asset.AssetLink == mediaData.ContentLink);

                        NodeContent clone = node.CreateWritableClone<NodeContent>();

                        log.Information($"Removing asset with id {assetToRemove.AssetLink.ID} from node {node.Name}");

                        clone.CommerceMediaCollection.Remove(assetToRemove);

                        int count = 0;

                        foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection.OrderBy(asset => asset.SortOrder))
                        {
                            commerceMedia.SortOrder = count;
                            count++;
                        }

                        contentRepository.Save(clone, SaveAction.Publish, AccessLevel.NoAccess);
                    }
                }
            }
            else
            {
                string logMess = string.Format("Didn't find resource with Resource ID: {0}, can't unlink", inriverResource.ResourceId);
                log.Debug(logMess);
            }    
        }

        private void HandleDelete(IInRiverImportResource inriverResource)
        {
            //get mediaData
            if (contentRepository.TryGet(EntityIdToGuid(inriverResource.ResourceId), out MediaData mediaData))
            {
                List<IDeleteActionsHandler> importerHandlers = ServiceLocator.Current.GetAllInstances<IDeleteActionsHandler>().ToList();

                if (RunIDeleteActionsHandlers)
                {
                    foreach (IDeleteActionsHandler handler in importerHandlers)
                    {
                        handler.PreDeleteResource(inriverResource);
                    }
                }

                //Get all references to mediadata
                IEnumerable<ReferenceInformation> references = contentRepository.GetReferencesToContent(mediaData.ContentLink, false);

                foreach (ReferenceInformation reference in references)
                {
                    if (contentRepository.TryGet(reference.OwnerID, out ProductContent product))
                    {
                        CommerceMedia assetToRemove = product.CommerceMediaCollection.First(asset => asset.AssetLink == mediaData.ContentLink);

                        log.Information($"Removing asset with id {assetToRemove.AssetLink.ID} from {product.Name}");

                        ProductContent clone = product.CreateWritableClone<ProductContent>();

                        clone.CommerceMediaCollection.Remove(assetToRemove);
                        int count = 0;

                        foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection.OrderBy(asset => asset.SortOrder))
                        {
                            commerceMedia.SortOrder = count;
                            count++;
                        }

                        contentRepository.Save(clone, SaveAction.Publish, AccessLevel.NoAccess);
                    }
                    else if (contentRepository.TryGet(reference.OwnerID, out NodeContent node))
                    {
                        CommerceMedia assetToRemove = node.CommerceMediaCollection.First(asset => asset.AssetLink == mediaData.ContentLink);
                        NodeContent clone = node.CreateWritableClone<NodeContent>();

                        log.Information($"Removing asset with id {assetToRemove.AssetLink.ID} from {node.Name}");
                        
                        clone.CommerceMediaCollection.Remove(assetToRemove);

                        int count = 0;

                        foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection.OrderBy(asset => asset.SortOrder))
                        {
                            commerceMedia.SortOrder = count;
                            count++;
                        }

                        contentRepository.Save(clone, SaveAction.Publish, AccessLevel.NoAccess);
                    }
                }

                contentRepository.Delete(mediaData.ContentLink, true, AccessLevel.NoAccess);

                if (RunIDeleteActionsHandlers)
                {
                    foreach (IDeleteActionsHandler handler in importerHandlers)
                    {
                        handler.PostDeleteResource(inriverResource);
                    }
                }
            }
            else
            {
                string logMess = string.Format("Didn't find resource with Resource ID: {0}, can't Delete", inriverResource.ResourceId);
                log.Debug(logMess);
            }
        }

        private void ImportImageAndAttachToEntry(IInRiverImportResource inriverResource)
        {
            //Transform resourceId to guid
            Guid entityGuid = EntityIdToGuid(inriverResource.ResourceId);

            string assetGroup = GetAssetGroup(inriverResource);

            log.Debug($"Resource with Resource ID: {inriverResource.ResourceId} added to group {assetGroup}");

            //Check if image exist
            if (contentRepository.TryGet(entityGuid, out MediaData existingMediaData))
            {
                string logMess = string.Format("Found existing resource with Resource ID: {0}", inriverResource.ResourceId);
                log.Debug(logMess);


                log.Debug($"Updateing metadata for resource with reasource id {inriverResource.ResourceId}");
                //Update Metadata
                UpdateMetaData(existingMediaData, inriverResource);

                // TODO: Check if we need to do this on update 
                if (inriverResource.Action == "added")
                {
                    //Add resurce to entry or node not new
                    AddLinksFromMediaToCodes(existingMediaData, inriverResource.EntryCodes, assetGroup);
                }
            }
            else
            {
                string logMess = string.Format("Didn't find resource with Resource ID: {0}", inriverResource.ResourceId);
                log.Debug(logMess);

                //Create file
                existingMediaData = CreateNewFile(out ContentReference contentReference, inriverResource);
                
                //Not exsting 
                AddLinksFromMediaToCodes(existingMediaData, inriverResource.EntryCodes, assetGroup);
            }
        }

        private static string GetAssetGroup(IInRiverImportResource inriverResource)
        {
            ResourceMetaField mainCategory = inriverResource.MetaFields.FirstOrDefault(metafield => metafield.Id == "ResourceMainCategory");
            ResourceMetaField subCategory = inriverResource.MetaFields.FirstOrDefault(metafield => metafield.Id == "ResourceSubCategory");

            if (mainCategory?.Values?.FirstOrDefault()?.Data != null && subCategory?.Values?.FirstOrDefault()?.Data != null)
            {
                return $"{mainCategory.Values.First().Data}|{subCategory.Values.First().Data}";
            }

            return "default";
        }

        private void AddLinksFromMediaToCodes(MediaData mediaData, List<EntryCode> entryCodes, string assetGroup)
        {
            foreach (EntryCode entryCode in entryCodes)
            {
                ContentReference contentReference = referenceConverter.GetContentLink(entryCode.Code);

                if (contentRepository.TryGet(contentReference, out ProductContent productContent))
                {
                    AddOrUpdateSortOrderForProduct(productContent, mediaData, entryCode, assetGroup);
                }
                else if (contentRepository.TryGet(contentReference, out NodeContent node))
                {
                    AddOrUpdateSortOrderForNode(node, mediaData, entryCode, assetGroup);
                }
                else
                {
                    string logMess = string.Format("Could not find entry with code: {0}, can't create link", entryCode.Code);
                    log.Debug(logMess);
                }
            }
        }

        private void AddOrUpdateSortOrderForProduct(ProductContent productContent, MediaData mediaData, EntryCode entryCode, string assetGroup)
        {
            CommerceMedia existingMedia = productContent.CommerceMediaCollection.SingleOrDefault(asset => asset.AssetLink.ID == mediaData.ContentLink.ID);

            if (existingMedia == null)
            {
                //New CommerceMedia for current productContet
                ProductContent clone = productContent.CreateWritableClone<ProductContent>();

                foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection)
                {
                    if (commerceMedia.SortOrder >= entryCode.SortOrder)
                    {
                        commerceMedia.SortOrder++;
                    }
                }

                CommerceMedia media = new CommerceMedia(mediaData.ContentLink, "episerver.core.icontentmedia", assetGroup, entryCode.SortOrder);
                clone.CommerceMediaCollection.Insert(entryCode.SortOrder, media);
                log.Information($"Created Commerce media with id {media.AssetLink.ID}, sortorder {media.SortOrder}, Product content {productContent.Name}");

                contentRepository.Save(clone, SaveAction.Patch, AccessLevel.NoAccess);
            }
            else if (existingMedia.SortOrder != entryCode.SortOrder)
            {
                ProductContent clone = productContent.CreateWritableClone<ProductContent>();
                int oldSortOrder = existingMedia.SortOrder;
                int newSortOrder = entryCode.SortOrder;

                //Update sortorder for all affected media
                foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection)
                {
                    if (existingMedia.AssetLink.ID == commerceMedia.AssetLink.ID)
                    {
                        commerceMedia.SortOrder = newSortOrder;
                        commerceMedia.GroupName = assetGroup;
                        log.Information($"Uppdated Commerce media with id {commerceMedia.AssetLink.ID}, new sortorder {newSortOrder} old sortorder {oldSortOrder}, Product content {productContent.Name}");
                    }
                    // Medias sortorder is less then old sortorder and greater or equals new sortorder
                    else if (commerceMedia.SortOrder < oldSortOrder && commerceMedia.SortOrder >= newSortOrder)
                    {
                        commerceMedia.SortOrder++;
                        log.Debug($"Updated sortorder for {commerceMedia.AssetLink.ID}, new sortorder {commerceMedia.SortOrder}");
                    }
                    // Medias sortOrder is more then old sortOrder but less then new sortorder 
                    else if (commerceMedia.SortOrder > oldSortOrder && commerceMedia.SortOrder < newSortOrder)
                    {
                        commerceMedia.SortOrder--;
                        log.Debug($"Updated sortorder for {commerceMedia.AssetLink.ID}, new sortorder {commerceMedia.SortOrder}");
                    }
                }
                contentRepository.Save(clone, SaveAction.Patch, AccessLevel.NoAccess);
            }
            else if (existingMedia.GroupName != assetGroup)
            {
                ProductContent clone = productContent.CreateWritableClone<ProductContent>();
                CommerceMedia commerceMedia = clone.CommerceMediaCollection.Single(media => media.AssetLink.ID == existingMedia.AssetLink.ID);

                contentRepository.Save(clone, SaveAction.Patch, AccessLevel.NoAccess);
            }
        }

        private void AddOrUpdateSortOrderForNode(NodeContent node, MediaData mediaData, EntryCode entryCode, string assetGroup)
        {
            CommerceMedia existingMedia = node.CommerceMediaCollection.SingleOrDefault(asset => asset.AssetLink.ID == mediaData.ContentLink.ID);

            if (existingMedia == null)
            {
                //New CommerceMedia for current node
                NodeContent clone = node.CreateWritableClone<NodeContent>();

                foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection)
                {
                    if (commerceMedia.SortOrder >= entryCode.SortOrder)
                    {
                        commerceMedia.SortOrder++;
                    }
                }
                
                CommerceMedia media = new CommerceMedia(mediaData.ContentLink, "episerver.core.icontentmedia", assetGroup, entryCode.SortOrder);
                clone.CommerceMediaCollection.Insert(entryCode.SortOrder, media);
                log.Information($"Created Commerce media with id {media.AssetLink.ID}, sortorder {media.SortOrder}, node {node.Name}");

                contentRepository.Save(clone, SaveAction.Patch, AccessLevel.NoAccess);
            }
            else if (existingMedia.SortOrder != entryCode.SortOrder)
            {
                NodeContent clone = node.CreateWritableClone<NodeContent>();
                int oldSortOrder = existingMedia.SortOrder;
                int newSortOrder = entryCode.SortOrder;

                //Update sortorder for all affected media
                foreach (CommerceMedia commerceMedia in clone.CommerceMediaCollection)
                {
                    if (existingMedia.AssetLink.ID == commerceMedia.AssetLink.ID)
                    {
                        commerceMedia.SortOrder = newSortOrder;
                        commerceMedia.GroupName = assetGroup;
                        log.Information($"Uppdated Commerce media with id {commerceMedia.AssetLink.ID}, new sortorder {newSortOrder} old sortorder {oldSortOrder}, node {node.Name}");
                    }
                    // Medias sortorder is less then old sortorder and greater or equals new sortorder
                    else if (commerceMedia.SortOrder < oldSortOrder && commerceMedia.SortOrder >= newSortOrder)
                    {
                        commerceMedia.SortOrder++;
                        log.Debug($"Updated sortorder for {commerceMedia.AssetLink.ID}, new sortorder {commerceMedia.SortOrder}");
                    }
                    // Medias sortOrder is more then old sortOrder but less then new sortorder 
                    else if (commerceMedia.SortOrder > oldSortOrder && commerceMedia.SortOrder < newSortOrder)
                    {
                        commerceMedia.SortOrder--;
                        log.Debug($"Updated sortorder for {commerceMedia.AssetLink.ID}, new sortorder {commerceMedia.SortOrder}");
                    }
                }

                contentRepository.Save(clone, SaveAction.Patch, AccessLevel.NoAccess);
            }
            else if (existingMedia.GroupName != assetGroup)
            {
                NodeContent clone = node.CreateWritableClone<NodeContent>();
                CommerceMedia commerceMedia = clone.CommerceMediaCollection.Single(media => media.AssetLink.ID == existingMedia.AssetLink.ID);

                contentRepository.Save(clone, SaveAction.Patch, AccessLevel.NoAccess);
            }
        }

        private void UpdateMetaData(MediaData resource, IInRiverImportResource updatedResource)
        {
            MediaData editableMediaData = (MediaData)resource.CreateWritableClone();
            ResourceMetaField updateResourceFileId = updatedResource.MetaFields.FirstOrDefault(m => m.Id == "ResourceFileId");

            string updateResourceFileIdData = updateResourceFileId?.Values.First().Data;

            if (int.TryParse(updateResourceFileIdData, out int resourceFileId) 
                && resourceFileId == ((IInRiverResource)resource).ResourceFileId)
            {
                // Update binary information
                IBlobFactory blobFactory = ServiceLocator.Current.GetInstance<IBlobFactory>();
                IUrlSegmentGenerator urlSegmentGenerator = ServiceLocator.Current.GetInstance<IUrlSegmentGenerator>();

                string ext = Path.GetExtension(updatedResource.Path);
                string fileName = Path.GetFileName(updatedResource.Path);
                Stream azureResourceStream = GetResourceByFileName(fileName, resourceZipFileNameInCloud);

                if (azureResourceStream == null)
                {
                    throw new FileNotFoundException("File could not be imported", updatedResource.Path);
                }

                // need to explicitly read the stream  from Azure as copyTo was not copying content.
                BinaryReader binReader = new BinaryReader(azureResourceStream);

                if (binReader.BaseStream.CanSeek)
                {
                    binReader.BaseStream.Position = 0;
                }

                const int bufferSize = 4096;

                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[bufferSize];
                    int count;

                    while ((count = binReader.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        ms.Write(buffer, 0, count);
                    }

                    byte[] importedImage = ms.ToArray();

                    // Create a blob in the binary container (folder)
                    Blob blob = blobFactory.CreateBlob(editableMediaData.BinaryDataContainer, ext);

                    if (importedImage.Length > 0)
                    {
                        // write all the bytes from imported file from Azure file store
                        blob.WriteAllBytes(importedImage);
                    }

                    // Assign to file and publish changes
                    editableMediaData.BinaryData = blob;
                }

                string rawFilename = null;

                if (updatedResource.MetaFields.Any(f => f.Id == "ResourceFilename"))
                {
                    // Change the filename.
                    rawFilename = updatedResource.MetaFields.First(f => f.Id == "ResourceFilename").Values[0].Data;
                }
                else if (updatedResource.MetaFields.Any(f => f.Id == "ResourceFileId"))
                {
                    // Change the fileId.
                    rawFilename = updatedResource.MetaFields.First(f => f.Id == "ResourceFileId").Values[0].Data;
                }

                editableMediaData.RouteSegment = urlSegmentGenerator.Create(rawFilename);
            }

            metaDataService.UpdateResourceProperties((IInRiverResource)editableMediaData, updatedResource);

            contentRepository.Save(editableMediaData, SaveAction.Publish, AccessLevel.NoAccess);
        }

        private MediaData CreateNewFile(out ContentReference contentReference, IInRiverImportResource inriverResource)
        {
            IContentRepository repository = contentRepository;
            IBlobFactory blobFactory = ServiceLocator.Current.GetInstance<IBlobFactory>();
            ContentMediaResolver mediaDataResolver = ServiceLocator.Current.GetInstance<ContentMediaResolver>();
            IContentTypeRepository contentTypeRepository = ServiceLocator.Current.GetInstance<IContentTypeRepository>();
            Stream azureResourceStream = null;
            byte[] importedImage = new byte[0];
            bool resourceWithoutFile = false;
            ResourceMetaField resourceFileId = inriverResource.MetaFields.FirstOrDefault(m => m.Id == "ResourceFileId");

            if (resourceFileId == null || string.IsNullOrEmpty(resourceFileId.Values.First().Data))
            {
                resourceWithoutFile = true;
            }

            log.Debug("CreateNewFile - resourceWithoutFile - " + resourceWithoutFile.ToString());

            string fileExtension = string.Empty;
            string fileName = string.Empty;
            FileInfo fileInfo = null;

            if (resourceWithoutFile)
            {
                fileExtension = "url";
            }
            else
            {
                string filePath = inriverResource.Path;
                fileExtension = Path.GetExtension(filePath);
                fileName = Path.GetFileName(filePath);
                azureResourceStream = GetResourceByFileName(fileName, resourceZipFileNameInCloud);

                if (azureResourceStream == null)
                {
                    throw new FileNotFoundException("File could not be imported", inriverResource.Path);
                }

                // need to explicitly read the stream  from Azure as copyTo was not copying content.
                BinaryReader binReader = new BinaryReader(azureResourceStream);
                const int bufferSize = 4096;

                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[bufferSize];
                    int count;
                    while ((count = binReader.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        ms.Write(buffer, 0, count);
                    }

                    importedImage = ms.ToArray();
                }
            }

            ContentType contentType = null;
            IEnumerable<Type> mediaTypes = mediaDataResolver.ListAllMatching(fileExtension);

            foreach (Type type in mediaTypes)
            {
                if (type.GetInterfaces().Contains(typeof(IInRiverResource)) && type.Name == "ImageFile")
                {
                    contentType = contentTypeRepository.Load(type);
                    break;
                }
            }

            if (contentType == null)
            {
                contentType = contentTypeRepository.Load(typeof(InRiverGenericMedia));
            }
            // Get new empty file data instance in the media folder for inRiver Resource
            // TODO: Place resource inside a sub folder, but we need to organize the folder structure.
            MediaData newFile = repository.GetDefault<MediaData>(GetInRiverResourceFolder(), contentType.ID);

            if (resourceWithoutFile)
            {
                // find name
                ResourceMetaField resourceName = inriverResource.MetaFields.FirstOrDefault(m => m.Id == "ResourceName");
                if (resourceName != null && !string.IsNullOrEmpty(resourceName.Values.First().Data))
                {
                    newFile.Name = resourceName.Values.First().Data;
                }
                else
                {
                    newFile.Name = inriverResource.ResourceId.ToString(CultureInfo.InvariantCulture);
                }
            }
            else
            {
                newFile.Name = fileName;
            }
            // This cannot fail
            IInRiverResource resource = (IInRiverResource)newFile;
            if (resourceFileId != null && fileInfo != null)
            {
                resource.ResourceFileId = int.Parse(resourceFileId.Values.First().Data);
            }
            resource.EntityId = inriverResource.ResourceId;
            try
            {
                metaDataService.UpdateResourceProperties(resource, inriverResource);
            }
            catch (Exception exception)
            {
                string errMess = string.Format("Error when running UpdateResourceProperties for resource {0} with contentType {1}: {2}", inriverResource.ResourceId, contentType.Name, exception.Message);
                log.Error(errMess);
            }

            if (!resourceWithoutFile)
            {
                // Create a blob in the binary container (folder)
                Blob blob = blobFactory.CreateBlob(newFile.BinaryDataContainer, fileExtension);
                if (importedImage.Length > 0)
                {
                    // write all the bytes from imported file from Azure file store
                    blob.WriteAllBytes(importedImage);
                }
                else
                {
                    using (Stream s = blob.OpenWrite())
                    {
                        FileStream fileStream = File.OpenRead(fileInfo.FullName);
                        fileStream.CopyTo(s);
                    }
                }

                // Assign to file and publish changes
                newFile.BinaryData = blob;
            }
            newFile.ContentGuid = EntityIdToGuid(inriverResource.ResourceId);
            try
            {
                contentReference = repository.Save(newFile, SaveAction.Publish, AccessLevel.NoAccess);
                return newFile;
            }
            catch (Exception exception)
            {
                string errMess = string.Format("Error when calling Save: " + exception.Message);
                log.Error(errMess);
                contentReference = null;
                return newFile;
            }
        }

        private Guid EntityIdToGuid(int entityId)
        {
            return new Guid(string.Format("00000000-0000-0000-0000-00{0:0000000000}", entityId));
        }
    }
}
