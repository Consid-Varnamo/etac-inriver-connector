using inRiver.Connectors.EPiServer.Communication;
using inRiver.Connectors.EPiServer.Helpers;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace inRiver.Connectors.EPiServer.EpiXml
{
    public class EpiDocument
    {
        private readonly inRiverContext _context;
        private readonly EpiElement _epiElement;
        private readonly ChannelHelper _channelHelper;
        private readonly EpiMappingHelper _epiMappingHelper;
        private readonly Configuration _configuration;
        private readonly EpiApi _epiApi;

        public EpiDocument(inRiverContext context, Configuration configuration)
        {
            _context = context;
            _epiElement = new EpiElement(context);
            _channelHelper = new ChannelHelper(context);
            _epiMappingHelper = new EpiMappingHelper(context);
            _configuration = configuration;
            _epiApi = new EpiApi(context);
        }

        public XDocument CreateImportDocument(Entity channelEntity, XElement metaClasses, XElement associationTypes, Dictionary<string, List<XElement>> epiElementsFromStructure, Configuration config)
        {
            XElement catalogElement = _epiElement.CreateCatalogElement(channelEntity, config);
            if (catalogElement == null)
            {
                return null;
            }

            catalogElement.Add(
                new XElement("Sites", new XElement("Site", _channelHelper.GetChannelGuid(channelEntity, config).ToString())),
                new XElement("Nodes", new XAttribute("totalCount", epiElementsFromStructure["Nodes"].Count), epiElementsFromStructure["Nodes"]),
                new XElement("Entries", new XAttribute("totalCount", epiElementsFromStructure["Entries"].Count), epiElementsFromStructure["Entries"]),
                new XElement("Relations", new XAttribute("totalCount", epiElementsFromStructure["Relations"].Count), epiElementsFromStructure["Relations"].OrderByDescending(e => e.Name.LocalName)),
                new XElement("Associations", new XAttribute("totalCount", epiElementsFromStructure["Associations"].Count), epiElementsFromStructure["Associations"]));

            return CreateDocument(catalogElement, metaClasses, associationTypes, config);
        }

        public XDocument CreateUpdateDocument(Entity channelEntity, Entity updatedEntity, Configuration config)
        {
            int count = 0;
            List<XElement> skus = new List<XElement>();
            if (config.ItemsToSkus && updatedEntity.EntityType.Id == "Item")
            {
                skus = _epiElement.GenerateSkuItemElemetsFromItem(updatedEntity, config);
                count += skus.Count;
            }

            XElement updatedNode = null;
            XElement updatedEntry = null;

            if (updatedEntity.EntityType.Id == "ChannelNode")
            {
                string parentId = channelEntity.Id.ToString(CultureInfo.InvariantCulture);
                Link nodeLink = updatedEntity.Links.Find(l => l.Source.Id == channelEntity.Id);
                int sortOrder = 0;
                if (nodeLink != null)
                {
                    sortOrder = nodeLink.Index;
                }

                updatedNode = _epiElement.CreateNodeElement(updatedEntity, parentId, sortOrder, config);
            }
            else if (!(updatedEntity.EntityType.Id == "Item" && !config.UseThreeLevelsInCommerce && config.ItemsToSkus))
            {
                updatedEntry = _epiElement.InRiverEntityToEpiEntry(updatedEntity, config);
                Link specLink = updatedEntity.OutboundLinks.Find(l => l.Target.EntityType.Id == "Specification");
                if (specLink != null)
                {
                    XElement metaField = new XElement("MetaField", new XElement("Name", "SpecificationField"), new XElement("Type", "LongHtmlString"));
                    foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
                    {
                        string htmlData = _context.ExtensionManager.DataService.GetSpecificationAsHtml(specLink.Target.Id, updatedEntity.Id, culturePair.Value);
                        metaField.Add(new XElement("Data", new XAttribute("language", culturePair.Key.Name.ToLower()), new XAttribute("value", htmlData)));
                    }

                    XElement element = updatedEntry.Descendants().FirstOrDefault(f => f.Name == "MetaFields");
                    if (element != null)
                    {
                        element.Add(metaField);
                    }
                }

                count += 1;
            }

            XElement catalogElement = _epiElement.CreateCatalogElement(channelEntity, config);
            catalogElement.Add(
                new XElement("Sites", new XElement("Site", _channelHelper.GetChannelGuid(channelEntity, config).ToString())),
                new XElement("Nodes", updatedNode),
                new XElement("Entries", new XAttribute("totalCount", count), updatedEntry, skus),
                new XElement("Relations"),
                new XElement("Associations"));

            return CreateDocument(catalogElement, null, null, config);
        }

        public XDocument CreateDocument(XElement catalogElement, XElement metaClasses, XElement associationTypes, Configuration config)
        {
            var result =
                new XDocument(
                    new XElement(
                        "Catalogs",
                        new XAttribute("version", "1.0"),
                        new XElement("MetaDataScheme", metaClasses),
                        new XElement("Dictionaries", associationTypes),
                        catalogElement));

            return result;
        }

        /// <summary>
        /// Return a dictionary of XElements (Nodes, Entries, Relations and Associations)
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public Dictionary<string, List<XElement>> GetEPiElements(Configuration config)
        {
            Dictionary<string, List<XElement>> epiElements = InitiateEpiElements();
            FillElementList(epiElements, config);
            return epiElements;
        }

        public XElement GetAssociationTypes(Configuration config)
        {
            return new XElement("AssociationTypes", from lt in config.ExportEnabledLinkTypes select _epiElement.CreateAssociationTypeElement(lt));
        }

        /// <summary>
        /// Creates skeleton elements (Nodes, Entries, Relations and Associations)
        /// </summary>
        /// <returns>The dictionary of XElement</returns>
        private Dictionary<string, List<XElement>> InitiateEpiElements()
        {
            var result = new Dictionary<string, List<XElement>>
                             {
                                 { "Nodes", new List<XElement>() },
                                 { "Entries", new List<XElement>() },
                                 { "Relations", new List<XElement>() },
                                 { "Associations", new List<XElement>() }
                             };
            return result;
        }

        /// <summary>
        /// Fills skeleton with child elements (entity, nodes and relations)
        /// </summary>
        /// <param name="epiElements"></param>
        /// <param name="config"></param>
        private void FillElementList(Dictionary<string, List<XElement>> epiElements, Configuration config)
        {
            try
            {
                if (!epiElements["Nodes"].Any(e =>
                {
                    XElement element = e.Element("Code");
                    return element != null && element.Value.Equals(config.ChannelIdPrefix + "_inRiverAssociations");
                }))
                {
                    epiElements["Nodes"].Add(_epiElement.CreateAssociationNodeElement("inRiverAssociations", config));
                    _context.Log(LogLevel.Debug, string.Format("Added channelNode {0} to Nodes", "inRiverAssociations"));
                }

                List<string> addedEntities = new List<string>();
                List<string> addedNodes = new List<string>();
                List<string> addedRelations = new List<string>();

                int totalLoaded = 0;
                int batchSize = config.BatchSize;

                do
                {
                    var batch = config.ChannelStructureEntities.Skip(totalLoaded).Take(batchSize).ToList();

                    config.ChannelEntities = GetEntitiesInStructure(batch);

                    FillElements(batch, config, addedEntities, addedNodes, addedRelations, epiElements);

                    totalLoaded += batch.Count;

                    _context.Log(LogLevel.Debug, string.Format("fetched {0} of {1} total", totalLoaded, config.ChannelStructureEntities.Count));
                }
                while (config.ChannelStructureEntities.Count > totalLoaded);
            }
            catch (Exception ex)
            {
                _context.Log(LogLevel.Error, ex.Message, ex);
            }
        }

        private void FillElements(List<StructureEntity> structureEntitiesBatch, Configuration config, List<string> addedEntities, List<string> addedNodes, List<string> addedRelations, Dictionary<string, List<XElement>> epiElements)
        {
            int logCounter = 0;
            var channelPrefixHelper = new ChannelPrefixHelper(_context);


            Dictionary<string, LinkType> linkTypes = new Dictionary<string, LinkType>();

            foreach (LinkType linkType in config.LinkTypes)
            {
                if (!linkTypes.ContainsKey(linkType.Id))
                {
                    linkTypes.Add(linkType.Id, linkType);
                }
            }

            foreach (StructureEntity structureEntity in structureEntitiesBatch)
            {
                try
                {

                    if (structureEntity.EntityId == config.ChannelId)
                    {
                        continue;
                    }

                    logCounter++;

                    if (logCounter == 1000)
                    {
                        logCounter = 0;
                        _context.Log(LogLevel.Debug, "Generating catalog xml.");
                    }

                    int id = structureEntity.EntityId;

                    if (structureEntity.LinkEntityId.HasValue)
                    {
                        // Add the link entity

                        Entity linkEntity = null;

                        if (config.ChannelEntities.ContainsKey(structureEntity.LinkEntityId.Value))
                        {
                            linkEntity = config.ChannelEntities[structureEntity.LinkEntityId.Value];
                        }

                        if (linkEntity == null)
                        {
                            _context.Log(
                                LogLevel.Warning,
                                string.Format(
                                    "Link Entity with id {0} does not exist in system or ChannelStructure table is not in sync.",
                                    (int)structureEntity.LinkEntityId));
                            continue;
                        }

                        XElement entryElement = _epiElement.InRiverEntityToEpiEntry(linkEntity, config);

                        XElement codeElement = entryElement.Element("Code");
                        if (codeElement != null && !addedEntities.Contains(codeElement.Value))
                        {
                            epiElements["Entries"].Add(entryElement);
                            addedEntities.Add(codeElement.Value);

                            _context.Log(LogLevel.Debug, string.Format("Added Entity {0} to Entries", linkEntity.DisplayName));
                        }

                        if (!addedRelations.Contains(channelPrefixHelper.GetEPiCodeWithChannelPrefix(linkEntity.Id, config) + "_" + channelPrefixHelper.GetEPiCodeWithChannelPrefix("_inRiverAssociations", config)))
                        {
                            epiElements["Relations"].Add(
                                _epiElement.CreateNodeEntryRelationElement(
                                    "_inRiverAssociations",
                                    linkEntity.Id.ToString(CultureInfo.InvariantCulture),
                                    0,
                                    config));
                            addedRelations.Add(
                                channelPrefixHelper.GetEPiCodeWithChannelPrefix(linkEntity.Id, config) + "_"
                                + channelPrefixHelper.GetEPiCodeWithChannelPrefix("_inRiverAssociations", config));


                            _context.Log(LogLevel.Debug, string.Format("Added Relation for EntryCode {0}", channelPrefixHelper.GetEPiCodeWithChannelPrefix(linkEntity.Id, config)));
                        }
                    }

                    if (structureEntity.Type == "Resource")
                    {
                        continue;
                    }

                    Entity entity;

                    if (config.ChannelEntities.ContainsKey(id))
                    {
                        entity = config.ChannelEntities[id];
                    }
                    else
                    {
                        entity = _context.ExtensionManager.DataService.GetEntity(id, LoadLevel.DataOnly);

                        config.ChannelEntities.Add(id, entity);
                    }

                    if (entity == null)
                    {
                        //missmatch with entity data and ChannelStructure. 

                        config.ChannelEntities.Remove(id);
                        continue;
                    }

                    if (structureEntity.Type == "ChannelNode")
                    {
                        string parentId = structureEntity.ParentId.ToString(CultureInfo.InvariantCulture);

                        if (config.ChannelId.Equals(structureEntity.ParentId))
                        {
                            _epiApi.CheckAndMoveNodeIfNeeded(id.ToString(CultureInfo.InvariantCulture), config);
                        }

                        _context.Log(LogLevel.Debug, string.Format("Trying to add channelNode {0} to Nodes", id));

                        XElement nodeElement = epiElements["Nodes"].Find(e =>
                        {
                            XElement xElement = e.Element("Code");
                            return xElement != null && xElement.Value.Equals(channelPrefixHelper.GetEPiCodeWithChannelPrefix(entity.Id, config));
                        });

                        int linkIndex = structureEntity.SortOrder;

                        if (nodeElement == null)
                        {
                            epiElements["Nodes"].Add(_epiElement.CreateNodeElement(entity, parentId, linkIndex, config));
                            addedNodes.Add(channelPrefixHelper.GetEPiCodeWithChannelPrefix(entity.Id, config));

                            _context.Log(LogLevel.Debug, string.Format("Added channelNode {0} to Nodes", id));
                        }
                        else
                        {
                            XElement parentNode = nodeElement.Element("ParentNode");
                            if (parentNode != null && (parentNode.Value != config.ChannelId.ToString(CultureInfo.InvariantCulture) && parentId == config.ChannelId.ToString(CultureInfo.InvariantCulture)))
                            {
                                string oldParent = parentNode.Value;
                                parentNode.Value = config.ChannelId.ToString(CultureInfo.InvariantCulture);
                                parentId = oldParent;

                                XElement sortOrderElement = nodeElement.Element("SortOrder");
                                if (sortOrderElement != null)
                                {
                                    string oldSortOrder = sortOrderElement.Value;
                                    sortOrderElement.Value = linkIndex.ToString(CultureInfo.InvariantCulture);
                                    linkIndex = int.Parse(oldSortOrder);
                                }
                            }

                            if (!addedRelations.Contains(channelPrefixHelper.GetEPiCodeWithChannelPrefix(
                                        id.ToString(CultureInfo.InvariantCulture),
                                        config) + "_" + channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentId, config)))
                            {
                                // add relation
                                epiElements["Relations"].Add(
                                    _epiElement.CreateNodeRelationElement(
                                        parentId,
                                        id.ToString(CultureInfo.InvariantCulture),
                                        linkIndex,
                                        config));

                                addedRelations.Add(
                                    channelPrefixHelper.GetEPiCodeWithChannelPrefix(
                                        id.ToString(CultureInfo.InvariantCulture),
                                        config) + "_" + channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentId, config));

                                _context.Log(LogLevel.Debug, string.Format("Adding relation to channelNode {0}", id));
                            }

                        }

                        continue;
                    }

                    if (structureEntity.Type == "Item" && config.ItemsToSkus)
                    {
                        List<XElement> skus = _epiElement.GenerateSkuItemElemetsFromItem(entity, config);
                        foreach (XElement sku in skus)
                        {
                            XElement codeElement = sku.Element("Code");
                            if (codeElement != null && !addedEntities.Contains(codeElement.Value))
                            {
                                epiElements["Entries"].Add(sku);
                                addedEntities.Add(codeElement.Value);

                                _context.Log(LogLevel.Debug, string.Format("Added Item/SKU {0} to Entries", sku.Name.LocalName));
                            }
                        }
                    }

                    if ((structureEntity.Type == "Item" && config.ItemsToSkus && config.UseThreeLevelsInCommerce)
                        || !(structureEntity.Type == "Item" && config.ItemsToSkus))
                    {
                        XElement element = _epiElement.InRiverEntityToEpiEntry(entity, config);

                        StructureEntity specificationStructureEntity =
                            config.ChannelStructureEntities.FirstOrDefault(
                                s => s.ParentId.Equals(id) && s.Type.Equals("Specification"));

                        if (specificationStructureEntity != null)
                        {
                            XElement metaField = new XElement(
                                "MetaField",
                                new XElement("Name", "SpecificationField"),
                                new XElement("Type", "LongHtmlString"));
                            foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
                            {
                                string htmlData =
                                    _context.ExtensionManager.DataService.GetSpecificationAsHtml(
                                        specificationStructureEntity.EntityId,
                                        entity.Id,
                                        culturePair.Value);
                                metaField.Add(
                                    new XElement(
                                        "Data",
                                        new XAttribute("language", culturePair.Key.Name.ToLower()),
                                        new XAttribute("value", htmlData)));
                            }

                            XElement metaFieldsElement = element.Descendants().FirstOrDefault(f => f.Name == "MetaFields");
                            metaFieldsElement?.Add(metaField);
                        }

                        XElement codeElement = element.Element("Code");
                        if (codeElement != null && !addedEntities.Contains(codeElement.Value))
                        {
                            epiElements["Entries"].Add(element);
                            addedEntities.Add(codeElement.Value);

                            _context.Log(LogLevel.Debug, string.Format("Added Entity {0} to Entries", id));
                        }
                    }

                    List<StructureEntity> existingStructureEntities =
                        config.ChannelStructureEntities.FindAll(i => i.EntityId.Equals(id));

                    List<StructureEntity> filteredStructureEntities = new List<StructureEntity>();

                    foreach (StructureEntity se in existingStructureEntities)
                    {
                        if (!filteredStructureEntities.Exists(i => i.EntityId == se.EntityId && i.ParentId == se.ParentId))
                        {
                            filteredStructureEntities.Add(se);
                        }
                        else
                        {
                            if (se.LinkEntityId.HasValue)
                            {
                                if (!filteredStructureEntities.Exists(
                                        i =>
                                        i.EntityId == se.EntityId && i.ParentId == se.ParentId
                                        && (i.LinkEntityId != null && i.LinkEntityId == se.LinkEntityId)))
                                {
                                    filteredStructureEntities.Add(se);
                                }
                            }
                        }
                    }

                    foreach (StructureEntity existingStructureEntity in filteredStructureEntities)
                    {
                        //Parent.
                        LinkType linkType = null;

                        if (linkTypes.ContainsKey(existingStructureEntity.LinkTypeIdFromParent))
                        {
                            linkType = linkTypes[existingStructureEntity.LinkTypeIdFromParent];
                        }

                        if (linkType == null)
                        {
                            continue;
                        }

                        if (linkType.SourceEntityTypeId == "ChannelNode")
                        {
                            if (!addedRelations.Contains(channelPrefixHelper.GetEPiCodeWithChannelPrefix(id.ToString(CultureInfo.InvariantCulture), config) + "_" + channelPrefixHelper.GetEPiCodeWithChannelPrefix(existingStructureEntity.ParentId.ToString(CultureInfo.InvariantCulture), config)))
                            {
                                epiElements["Relations"].Add(_epiElement.CreateNodeEntryRelationElement(existingStructureEntity.ParentId.ToString(), existingStructureEntity.EntityId.ToString(), existingStructureEntity.SortOrder, config));

                                addedRelations.Add(channelPrefixHelper.GetEPiCodeWithChannelPrefix(id, config) + "_" + channelPrefixHelper.GetEPiCodeWithChannelPrefix(existingStructureEntity.ParentId, config));

                                _context.Log(LogLevel.Debug, string.Format("Added Relation for Source {0} and Target {1} for LinkTypeId {2}", existingStructureEntity.ParentId, existingStructureEntity.EntityId, linkType.Id));
                            }

                            continue;
                        }

                        List<string> skus = new List<string> { id.ToString(CultureInfo.InvariantCulture) };
                        string parent = null;

                        if (structureEntity.Type.Equals("Item") && config.ItemsToSkus)
                        {
                            skus = _epiElement.SkuItemIds(entity, config);
                            for (int i = 0; i < skus.Count; i++)
                            {
                                skus[i] = channelPrefixHelper.GetEPiCodeWithChannelPrefix(skus[i], config);
                            }

                            if (config.UseThreeLevelsInCommerce)
                            {
                                parent = structureEntity.EntityId.ToString(CultureInfo.InvariantCulture);
                                skus.Add(parent);
                            }
                        }

                        Entity linkEntity = null;

                        if (existingStructureEntity.LinkEntityId != null)
                        {
                            if (config.ChannelEntities.ContainsKey(existingStructureEntity.LinkEntityId.Value))
                            {
                                linkEntity = config.ChannelEntities[existingStructureEntity.LinkEntityId.Value];
                            }
                            else
                            {
                                linkEntity = _context.ExtensionManager.DataService.GetEntity(
                                existingStructureEntity.LinkEntityId.Value,
                                LoadLevel.DataOnly);

                                config.ChannelEntities.Add(linkEntity.Id, linkEntity);
                            }
                        }

                        foreach (string skuId in skus)
                        {
                            string channelPrefixAndSkuId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(skuId, config);

                            // prod -> item link, bundle, package or dynamic package => Relation
                            if (_epiMappingHelper.IsRelation(linkType.SourceEntityTypeId, linkType.TargetEntityTypeId, linkType.Index, config))
                            {
                                int parentNodeId = _channelHelper.GetParentChannelNode(structureEntity, config);
                                if (parentNodeId == 0)
                                {
                                    continue;
                                }

                                string channelPrefixAndParentNodeId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentNodeId, config);

                                if (!addedRelations.Contains(channelPrefixAndSkuId + "_" + channelPrefixAndParentNodeId))
                                {
                                    epiElements["Relations"].Add(
                                        _epiElement.CreateNodeEntryRelationElement(
                                            parentNodeId.ToString(CultureInfo.InvariantCulture),
                                            skuId,
                                            existingStructureEntity.SortOrder,
                                            config));
                                    addedRelations.Add(channelPrefixAndSkuId + "_" + channelPrefixAndParentNodeId);

                                    _context.Log(
                                        LogLevel.Debug,
                                        string.Format("Added Relation for EntryCode {0}", channelPrefixAndSkuId));
                                }

                                string channelPrefixAndParentStructureEntityId =
                                    channelPrefixHelper.GetEPiCodeWithChannelPrefix(
                                        existingStructureEntity.ParentId.ToString(CultureInfo.InvariantCulture),
                                        config);

                                if (parent != null && skuId != parent)
                                {
                                    string channelPrefixAndParent = channelPrefixHelper.GetEPiCodeWithChannelPrefix(parent, config);

                                    if (!addedRelations.Contains(channelPrefixAndSkuId + "_" + channelPrefixAndParent))
                                    {
                                        epiElements["Relations"].Add(_epiElement.CreateEntryRelationElement(parent, linkType.SourceEntityTypeId, skuId, existingStructureEntity.SortOrder, config));
                                        addedRelations.Add(channelPrefixAndSkuId + "_" + channelPrefixAndParent);

                                        _context.Log(
                                            LogLevel.Debug,
                                            string.Format("Added Relation for ChildEntryCode {0}", channelPrefixAndSkuId));
                                    }
                                }
                                else if (!addedRelations.Contains(string.Format("{0}_{1}", channelPrefixAndSkuId, channelPrefixAndParentStructureEntityId)))
                                {
                                    epiElements["Relations"].Add(_epiElement.CreateEntryRelationElement(existingStructureEntity.ParentId.ToString(CultureInfo.InvariantCulture), linkType.SourceEntityTypeId, skuId, existingStructureEntity.SortOrder, config));
                                    addedRelations.Add(string.Format("{0}_{1}", channelPrefixAndSkuId, channelPrefixAndParentStructureEntityId));

                                    _context.Log(
                                        LogLevel.Debug,
                                        string.Format("Added Relation for ChildEntryCode {0}", channelPrefixAndSkuId));
                                }
                            }
                            else
                            {
                                if (!addedRelations.Contains(string.Format("{0}_{1}", channelPrefixAndSkuId, channelPrefixHelper.GetEPiCodeWithChannelPrefix("_inRiverAssociations", config))))
                                {
                                    epiElements["Relations"].Add(_epiElement.CreateNodeEntryRelationElement("_inRiverAssociations", skuId, existingStructureEntity.SortOrder, config));
                                    addedRelations.Add(string.Format("{0}_{1}", channelPrefixAndSkuId, channelPrefixHelper.GetEPiCodeWithChannelPrefix("_inRiverAssociations", config)));

                                    _context.Log(LogLevel.Debug, string.Format("Added Relation for EntryCode {0}", channelPrefixAndSkuId));
                                }

                                if (!config.UseThreeLevelsInCommerce && config.ItemsToSkus && structureEntity.Type == "Item")
                                {
                                    string channelPrefixAndLinkEntityId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(existingStructureEntity.LinkEntityId, config);
                                    string associationName = _epiMappingHelper.GetAssociationName(existingStructureEntity, linkEntity, config);

                                    Entity source;

                                    if (config.ChannelEntities.ContainsKey(existingStructureEntity.ParentId))
                                    {
                                        source = config.ChannelEntities[existingStructureEntity.ParentId];
                                    }
                                    else
                                    {
                                        source = _context.ExtensionManager.DataService.GetEntity(
                                            existingStructureEntity.ParentId,
                                            LoadLevel.DataOnly);
                                        config.ChannelEntities.Add(source.Id, source);
                                    }

                                    List<string> sourceSkuIds = _epiElement.SkuItemIds(source, config);
                                    for (int i = 0; i < sourceSkuIds.Count; i++)
                                    {
                                        sourceSkuIds[i] = channelPrefixHelper.GetEPiCodeWithChannelPrefix(
                                            sourceSkuIds[i],
                                            config);
                                    }

                                    foreach (string sourceSkuId in sourceSkuIds)
                                    {
                                        bool exists;
                                        if (existingStructureEntity.LinkEntityId != null)
                                        {
                                            exists = epiElements["Associations"].Any(
                                                e =>
                                                {
                                                    XElement entryCode = e.Element("EntryCode");
                                                    XElement description = e.Element("Description");
                                                    return description != null && entryCode != null && entryCode.Value.Equals(sourceSkuId) && e.Elements("Association").Any(
                                                        e2 =>
                                                        {
                                                            XElement associatedEntryCode =
                                                                    e2.Element("EntryCode");
                                                            return associatedEntryCode != null
                                                                       && associatedEntryCode.Value
                                                                              .Equals(sourceSkuId);
                                                        }) && description.Value.Equals(channelPrefixAndLinkEntityId);
                                                });
                                        }
                                        else
                                        {
                                            exists = epiElements["Associations"].Any(
                                                e =>
                                                {
                                                    XElement entryCode = e.Element("EntryCode");
                                                    return entryCode != null && entryCode.Value.Equals(sourceSkuId) && e.Elements("Association").Any(
                                                        e2 =>
                                                        {
                                                            XElement associatedEntryCode = e2.Element("EntryCode");
                                                            return associatedEntryCode != null && associatedEntryCode.Value.Equals(sourceSkuId);
                                                        }) && e.Elements("Association").Any(
                                                                e3 =>
                                                                {
                                                                    XElement typeElement = e3.Element("Type");
                                                                    return typeElement != null && typeElement.Value.Equals(linkType.Id);
                                                                });
                                                });
                                        }

                                        if (!exists)
                                        {
                                            XElement existingAssociation;

                                            if (existingStructureEntity.LinkEntityId != null)
                                            {
                                                existingAssociation = epiElements["Associations"].FirstOrDefault(
                                                    a =>
                                                    {
                                                        XElement nameElement = a.Element("Name");
                                                        XElement entryCodeElement = a.Element("EntryCode");
                                                        XElement descriptionElement = a.Element("Description");
                                                        return descriptionElement != null && entryCodeElement != null && nameElement != null && nameElement.Value.Equals(
                                                            associationName) && entryCodeElement.Value.Equals(sourceSkuId) && descriptionElement.Value.Equals(channelPrefixAndLinkEntityId);
                                                    });
                                            }
                                            else
                                            {
                                                existingAssociation = epiElements["Associations"].FirstOrDefault(
                                                    a =>
                                                    {
                                                        XElement nameElement = a.Element("Name");
                                                        XElement entryCodeElement = a.Element("EntryCode");
                                                        return entryCodeElement != null && nameElement != null && nameElement.Value.Equals(
                                                            associationName) && entryCodeElement.Value.Equals(sourceSkuId);
                                                    });
                                            }

                                            XElement associationElement = new XElement(
                                                "Association",
                                                new XElement("EntryCode", skuId),
                                                new XElement("SortOrder", existingStructureEntity.SortOrder),
                                                new XElement("Type", linkType.Id));

                                            if (existingAssociation != null)
                                            {
                                                if (!existingAssociation.Descendants().Any(e => e.Name.LocalName == "EntryCode" && e.Value == skuId))
                                                {
                                                    existingAssociation.Add(associationElement);
                                                }
                                            }
                                            else
                                            {

                                                string description = existingStructureEntity.LinkEntityId == null
                                                                         ? linkType.Id
                                                                         : channelPrefixAndLinkEntityId;
                                                description = description ?? string.Empty;

                                                XElement catalogAssociation = new XElement(
                                                    "CatalogAssociation",
                                                    new XElement("Name", associationName),
                                                    new XElement("Description", description),
                                                    new XElement("SortOrder", existingStructureEntity.SortOrder),
                                                    new XElement("EntryCode", sourceSkuId),
                                                    associationElement);

                                                epiElements["Associations"].Add(catalogAssociation);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    string channelPrefixAndEntityId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(existingStructureEntity.EntityId.ToString(CultureInfo.InvariantCulture), config);
                                    string channelPrefixAndParentEntityId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(existingStructureEntity.ParentId.ToString(CultureInfo.InvariantCulture), config);

                                    string channelPrefixAndLinkEntityId = string.Empty;

                                    if (existingStructureEntity.LinkEntityId != null)
                                    {
                                        channelPrefixAndLinkEntityId = channelPrefixHelper.GetEPiCodeWithChannelPrefix(existingStructureEntity.LinkEntityId, config);
                                    }

                                    string associationName = _epiMappingHelper.GetAssociationName(existingStructureEntity, linkEntity, config);

                                    bool exists;
                                    if (existingStructureEntity.LinkEntityId != null)
                                    {
                                        exists = epiElements["Associations"].Any(
                                            e =>
                                            {
                                                XElement entryCodeElement = e.Element("EntryCode");
                                                XElement descriptionElement = e.Element("Description");
                                                return descriptionElement != null && entryCodeElement != null && entryCodeElement.Value.Equals(channelPrefixAndParentEntityId) && e.Elements("Association").Any(
                                                            e2 =>
                                                            {
                                                                XElement associatedEntryCode = e2.Element("EntryCode");
                                                                return associatedEntryCode != null && associatedEntryCode.Value.Equals(channelPrefixAndEntityId);
                                                            }) && descriptionElement.Value.Equals(channelPrefixAndLinkEntityId);
                                            });
                                    }
                                    else
                                    {
                                        exists = epiElements["Associations"].Any(
                                            e =>
                                            {
                                                XElement entryCodeElement = e.Element("EntryCode");
                                                return entryCodeElement != null && entryCodeElement.Value.Equals(channelPrefixAndParentEntityId) && e.Elements("Association").Any(
                                                    e2 =>
                                                    {
                                                        XElement associatedEntryCode = e2.Element("EntryCode");
                                                        return associatedEntryCode != null && associatedEntryCode.Value.Equals(channelPrefixAndEntityId);
                                                    }) && e.Elements("Association").Any(
                                                            e3 =>
                                                            {
                                                                XElement typeElement = e3.Element("Type");
                                                                return typeElement != null && typeElement.Value.Equals(linkType.Id);
                                                            });
                                            });
                                    }

                                    if (!exists)
                                    {
                                        XElement existingAssociation;

                                        if (existingStructureEntity.LinkEntityId != null)
                                        {
                                            existingAssociation = epiElements["Associations"].FirstOrDefault(
                                                a =>
                                                {
                                                    XElement nameElement = a.Element("Name");
                                                    XElement entryCodeElement = a.Element("EntryCode");
                                                    XElement descriptionElement = a.Element("Description");
                                                    return descriptionElement != null && entryCodeElement != null && nameElement != null && nameElement.Value.Equals(associationName) && entryCodeElement.Value.Equals(
                                                        channelPrefixAndParentEntityId) && descriptionElement.Value.Equals(channelPrefixAndLinkEntityId);
                                                });
                                        }
                                        else
                                        {
                                            existingAssociation = epiElements["Associations"].FirstOrDefault(
                                                a =>
                                                {
                                                    XElement nameElement = a.Element("Name");
                                                    XElement entryCodeElement = a.Element("EntryCode");
                                                    return entryCodeElement != null && nameElement != null && nameElement.Value.Equals(associationName) && entryCodeElement.Value.Equals(channelPrefixAndParentEntityId);
                                                });
                                        }

                                        if (existingAssociation != null)
                                        {
                                            XElement newElement = _epiElement.CreateAssociationElement(existingStructureEntity, config);

                                            if (!existingAssociation.Descendants().Any(
                                                         e =>
                                                         e.Name.LocalName == "EntryCode"
                                                         && e.Value == channelPrefixAndEntityId))
                                            {
                                                existingAssociation.Add(newElement);
                                            }
                                        }
                                        else
                                        {
                                            epiElements["Associations"].Add(
                                                _epiElement.CreateCatalogAssociationElement(
                                                    existingStructureEntity,
                                                    linkEntity,
                                                    config));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _context.Log(LogLevel.Error, ex.Message, ex);
                }
            }
        }

        private Dictionary<int, Entity> GetEntitiesInStructure(List<StructureEntity> batch)
        {
            Dictionary<int, Entity> channelEntites = new Dictionary<int, Entity>();

            try
            {
                foreach (EntityType entityType in _configuration.ExportEnabledEntityTypes)
                {
                    List<StructureEntity> structureEntities =
                        batch.FindAll(i => i.Type.Equals(entityType.Id));

                    List<int> ids = structureEntities.Select(structureEntity => structureEntity.EntityId).Distinct().ToList();

                    if (!ids.Any())
                    {
                        continue;
                    }

                    List<Entity> entities = _context.ExtensionManager.DataService.GetEntities(ids, LoadLevel.DataOnly);

                    foreach (Entity entity in entities)
                    {
                        if (!channelEntites.ContainsKey(entity.Id))
                        {
                            channelEntites.Add(entity.Id, entity);
                        }
                    }
                }

                List<int> linkEntityIds = (from channelEntity in batch
                                           where channelEntity.LinkEntityId.HasValue
                                           select channelEntity.LinkEntityId.Value).Distinct().ToList();


                List<Entity> linkEntities = _context.ExtensionManager.DataService.GetEntities(linkEntityIds, LoadLevel.DataOnly);

                foreach (Entity entity in linkEntities)
                {
                    if (!channelEntites.ContainsKey(entity.Id))
                    {
                        channelEntites.Add(entity.Id, entity);
                    }
                }

            }
            catch (Exception ex)
            {
                _context.Log(LogLevel.Error, "GetEntitiesInStructure ", ex);
            }


            return channelEntites;
        }
    }
}
