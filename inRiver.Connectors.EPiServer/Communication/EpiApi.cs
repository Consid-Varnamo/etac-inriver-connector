using inRiver.Connectors.EPiServer.Helpers;
using inRiver.EPiServerCommerce.CommerceAdapter.Helpers;
using inRiver.EPiServerCommerce.Interfaces;
using inRiver.EPiServerCommerce.Interfaces.Enums;
using inRiver.EPiServerCommerce.MediaPublisher;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace inRiver.Connectors.EPiServer.Communication
{
    public class EpiApi
    {
        private readonly inRiverContext _context;
        private readonly ChannelPrefixHelper _channelPrefixHelper;
        private readonly BusinessHelper _businessHelper;
        public EpiApi(inRiverContext context)
        {
            _context = context;
            _channelPrefixHelper = new ChannelPrefixHelper(context);
            _businessHelper = new BusinessHelper(context);
        }

        public void DeleteCatalog(int catalogId, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    RestEndpoint<int> endpoint = new RestEndpoint<int>(config.Settings, "DeleteCatalog", this._context);
                    endpoint.Post(catalogId);
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Error, $"Failed to delete catalog with id: {catalogId}", exception);
                }
            }
        }

        public void DeleteCatalogNode(int catalogNodeId, int catalogId, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    string catalogNode = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(catalogNodeId, config);
                    RestEndpoint<string> endpoint = new RestEndpoint<string>(config.Settings, "DeleteCatalogNode", this._context);
                    endpoint.Post(catalogNode);
                }
                catch (Exception ex)
                {
                    _context.Log(LogLevel.Error, string.Format("Failed to delete catalogNode with id: {0} for channel: {1}", catalogNodeId, catalogId), ex);
                }
            }
        }

        public void DeleteCatalogEntry(string entityId, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    string catalogEntryId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(entityId, config);
                    RestEndpoint<string> endpoint = new RestEndpoint<string>(config.Settings, "DeleteCatalogEntry", this._context);
                    endpoint.Post(catalogEntryId);
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Error, string.Format("Failed to delete catalog entry based on entity id: {0}", entityId), exception);
                }
            }
        }

        public void UpdateLinkEntityData(Entity linkEntity, int channelId, Entity channelEntity, Configuration config, string parentId)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    string channelName = _businessHelper.GetDisplayNameFromEntity(channelEntity, config, -1);

                    string parentEntryId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentId, config);
                    string linkEntityIdString = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(linkEntity.Id, config);

                    string dispName = linkEntity.EntityType.Id + '_' + _businessHelper.GetDisplayNameFromEntity(linkEntity, config, -1).Replace(' ', '_');

                    LinkEntityUpdateData dataToSend = new LinkEntityUpdateData
                    {
                        ChannelName = channelName,
                        LinkEntityIdString = linkEntityIdString,
                        LinkEntryDisplayName = dispName,
                        ParentEntryId = parentEntryId
                    };

                    RestEndpoint<LinkEntityUpdateData> endpoint = new RestEndpoint<LinkEntityUpdateData>(config.Settings, "UpdateLinkEntityData", this._context);
                    endpoint.Post(dataToSend);
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Error, string.Format("Failed to update data for link entity with id:{0}", linkEntity.Id), exception);
                }
            }
        }

        public List<string> GetLinkEntityAssociationsForEntity(string linkType, int channelId, Entity channelEntity, Configuration config, List<string> parentIds, List<string> targetIds)
        {
            lock (SingletonEPiLock.Instance)
            {
                List<string> ids = new List<string>();
                try
                {
                    string channelName = _businessHelper.GetDisplayNameFromEntity(channelEntity, config, -1);

                    for (int i = 0; i < targetIds.Count; i++)
                    {
                        targetIds[i] = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(targetIds[i], config);
                    }

                    for (int i = 0; i < parentIds.Count; i++)
                    {
                        parentIds[i] = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentIds[i], config);
                    }

                    GetLinkEntityAssociationsForEntityData dataToSend = new GetLinkEntityAssociationsForEntityData
                    {
                        ChannelName = channelName,
                        LinkTypeId = linkType,
                        ParentIds = parentIds,
                        TargetIds = targetIds
                    };

                    RestEndpoint<GetLinkEntityAssociationsForEntityData> endpoint = new RestEndpoint<GetLinkEntityAssociationsForEntityData>(config.Settings, "GetLinkEntityAssociationsForEntity", this._context);
                    ids = endpoint.PostWithStringListAsReturn(dataToSend);
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Warning, string.Format("Failed to get link entity associations for entity"), exception);
                }

                return ids;
            }
        }

        public void CheckAndMoveNodeIfNeeded(string nodeId, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    string entryNodeId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(nodeId, config);

                    RestEndpoint<string> endpoint = new RestEndpoint<string>(config.Settings, "CheckAndMoveNodeIfNeeded", this._context);
                    endpoint.Post(entryNodeId);
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Warning, string.Format("Failed when calling the interface function: CheckAndMoveNodeIfNeeded"), exception);
                }
            }
        }

        public void UpdateEntryRelations(string catalogEntryId, int channelId, Entity channelEntity, Configuration config, string parentId, Dictionary<string, bool> shouldExistInChannelNodes, string linkTypeId, List<string> linkEntityIdsToRemove)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    string channelName = _businessHelper.GetDisplayNameFromEntity(channelEntity, config, -1);
                    List<string> removeFromChannelNodes = new List<string>();
                    foreach (KeyValuePair<string, bool> shouldExistInChannelNode in shouldExistInChannelNodes)
                    {
                        if (!shouldExistInChannelNode.Value)
                        {
                            removeFromChannelNodes.Add(
                                _channelPrefixHelper.GetEPiCodeWithChannelPrefix(shouldExistInChannelNode.Key, config));
                        }
                    }

                    var channelMappingHelper = new EpiMappingHelper(_context);

                    string parentEntryId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentId, config);
                    string catalogEntryIdString = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(catalogEntryId, config);
                    string channelIdEpified = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(channelId, config);
                    string inriverAssociationsEpified = _channelPrefixHelper.GetEPiCodeWithChannelPrefix("_inRiverAssociations", config);
                    bool relation = channelMappingHelper.IsRelation(linkTypeId, config);
                    bool parentExistsInChannelNodes = shouldExistInChannelNodes.Keys.Contains(parentId);

                    UpdateEntryRelationData updateEntryRelationData = new UpdateEntryRelationData
                    {
                        ParentEntryId = parentEntryId,
                        CatalogEntryIdString = catalogEntryIdString,
                        ChannelIdEpified = channelIdEpified,
                        ChannelName = channelName,
                        RemoveFromChannelNodes = removeFromChannelNodes,
                        LinkEntityIdsToRemove = linkEntityIdsToRemove,
                        InRiverAssociationsEpified = inriverAssociationsEpified,
                        LinkTypeId = linkTypeId,
                        IsRelation = relation,
                        ParentExistsInChannelNodes = parentExistsInChannelNodes
                    };

                    RestEndpoint<UpdateEntryRelationData> endpoint =
                        new RestEndpoint<UpdateEntryRelationData>(config.Settings, "UpdateEntryRelations", this._context);
                    endpoint.Post(updateEntryRelationData);
                }
                catch (Exception exception)
                {
                    string parentEntryId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentId, config);
                    string childEntryId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(catalogEntryId, config);
                    _context.Log(
                        LogLevel.Error,
                        string.Format("Failed to update entry relations between parent entry id {0} and child entry id {1} in catalog with id {2}", parentEntryId, childEntryId, catalogEntryId),
                        exception);
                }
            }
        }

        public bool StartImportIntoEpiServerCommerce(string /*filePath, */fileNameInCloud, Guid guid, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    RestEndpoint<string> endpoint = new RestEndpoint<string>(config.Settings, "ImportCatalogXml", this._context);
                    string result = endpoint.Post(fileNameInCloud);
                    _context.Log(LogLevel.Debug, string.Format("Import catalog returned: {0}", result));
                    return true;
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Error, string.Format("Failed to import catalog xml file {0}.", fileNameInCloud), exception);
                    _context.Log(LogLevel.Error, exception.ToString());

                    return false;
                }
            }
        }

        public bool StartAssetImportIntoEpiServerCommerce(string filePathInCloud, string baseFilePpath, Configuration config)
        {
            return StartAssetImportIntoEpiServerCommerce(new[] { filePathInCloud }, baseFilePpath, config);
        }

        public bool StartAssetImportIntoEpiServerCommerce(IEnumerable<string> filePathsInCloud, string baseFilePpath, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                bool isSuccess = true;
                foreach (string filePathInCloud in filePathsInCloud)
                {
                    try
                    {
                        string fileNameInClould = filePathInCloud.Split('/').Last();

                        _context.Log(LogLevel.Debug, string.Format("2. Create importer for {0}", fileNameInClould));

                        Importer ri = new Importer(_context);

                        _context.Log(LogLevel.Debug, "3. Execute ImportResources");
                        ri.ImportResources(fileNameInClould, baseFilePpath, config.Id);

                        _context.Log(LogLevel.Information, string.Format("Resource file {0} imported to EPi Server Commerce.", fileNameInClould));
                    }
                    catch (Exception exception)
                    {
                        _context.Log(LogLevel.Error, string.Format("Failed to import resource file {0}.", filePathInCloud), exception);
                        isSuccess = false;
                    }
                }
                return isSuccess;
            }
        }

        public bool ImportUpdateCompleted(string catalogName, ImportUpdateCompletedEventType eventType, bool resourceIncluded, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    RestEndpoint<ImportUpdateCompletedData> endpoint = new RestEndpoint<ImportUpdateCompletedData>(config.Settings, "ImportUpdateCompleted", this._context);
                    ImportUpdateCompletedData data = new ImportUpdateCompletedData
                    {
                        CatalogName = catalogName,
                        EventType = eventType,
                        ResourcesIncluded = resourceIncluded
                    };
                    string result = endpoint.Post(data);
                    _context.Log(LogLevel.Debug, string.Format("ImportUpdateCompleted returned: {0}", result));
                    return true;
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Error, string.Format("Failed to fire import update completed for catalog {0}.", catalogName), exception);
                    return false;
                }
            }
        }

        public bool DeleteCompleted(string catalogName, DeleteCompletedEventType eventType, Configuration config)
        {
            lock (SingletonEPiLock.Instance)
            {
                try
                {
                    RestEndpoint<DeleteCompletedData> endpoint = new RestEndpoint<DeleteCompletedData>(config.Settings, "DeleteCompleted", this._context);
                    DeleteCompletedData data = new DeleteCompletedData
                    {
                        CatalogName = catalogName,
                        EventType = eventType
                    };
                    string result = endpoint.Post(data);
                    _context.Log(LogLevel.Debug, string.Format("DeleteCompleted returned: {0}", result));
                    return true;
                }
                catch (Exception exception)
                {
                    _context.Log(LogLevel.Error, string.Format("Failed to fire DeleteCompleted for catalog {0}.", catalogName), exception);
                    return false;
                }
            }
        }

        public void SendHttpPost(Configuration config, string filepath)
        {
            if (string.IsNullOrEmpty(config.HttpPostUrl))
            {
                return;
            }

            // TBD - This code needs to refer to azure file, instead of local filepath. Need to be investigated and fixed
            // REF: BUG - 1932 - EPI connector - HTTP_POST_URL implementation needs to use Azure path

            //try
            //{
            //    string uri = config.HttpPostUrl;
            //    using (WebClient client = new WebClient())
            //    {
            //        client.UploadFileAsync(new Uri(uri), "POST", @filepath);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _context.Log(LogLevel.Error, "Exception in SendHttpPost", ex);
            //}
        }
    }
}
