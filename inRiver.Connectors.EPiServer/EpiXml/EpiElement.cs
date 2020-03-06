using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using inRiver.Connectors.EPiServer.Helpers;
using inRiver.EPiServerCommerce.CommerceAdapter.Helpers;
using inRiver.Remoting;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;

namespace inRiver.Connectors.EPiServer.EpiXml
{
    public class EpiElement
    {
        private readonly inRiverContext _context;

        private readonly EpiMappingHelper _epiMappingHelper;
        private readonly BusinessHelper _businessHelper;
        private readonly ChannelPrefixHelper _channelPrefixHelper;

        public EpiElement(inRiverContext context)
        {
            _context = context;
            _businessHelper = new BusinessHelper(context);
            _epiMappingHelper = new EpiMappingHelper(context);
            _channelPrefixHelper = new ChannelPrefixHelper(context);
        }

        public XElement InRiverEntityTypeToMetaClass(string name, string entityTypeName)
        {
            return new XElement(
                "MetaClass",
                new XElement("Namespace", "Mediachase.Commerce.Catalog.User"),
                new XElement("Name", name),
                new XElement("FriendlyName", name),
                new XElement("MetaClassType", "User"),
                new XElement("ParentClass", _epiMappingHelper.GetParentClassForEntityType(entityTypeName)),
                new XElement("TableName", _epiMappingHelper.GetTableNameForEntityType(entityTypeName, name)),
                new XElement("Description", "From inRiver"),
                new XElement("IsSystem", "False"),
                new XElement("IsAbstract", "False"),
                new XElement("FieldListChangedSqlScript"),
                new XElement("Tag"),
                new XElement("Attributes"));
        }

        public XElement InRiverFieldTypeToMetaField(FieldType fieldType, Configuration config)
        {
            return new XElement(
                "MetaField",
                new XElement("Namespace", "Mediachase.Commerce.Catalog"),
                new XElement("Name", _epiMappingHelper.GetEPiMetaFieldNameFromField(fieldType, config)),
                new XElement("FriendlyName", _epiMappingHelper.GetEPiMetaFieldNameFromField(fieldType, config)),
                new XElement("Description", "From inRiver"),
                new XElement("DataType", _epiMappingHelper.InRiverDataTypeToEpiType(fieldType, config)),
                new XElement("Length", _epiMappingHelper.GetMetaFieldLength(fieldType, config)),
                new XElement("AllowNulls", _businessHelper.GetAllowsNulls(fieldType, config)),
                new XElement("SaveHistory", "False"),
                new XElement("AllowSearch", _businessHelper.GetAllowsSearch(fieldType)),
                new XElement("MultiLanguageValue", _businessHelper.FieldTypeIsMultiLanguage(fieldType, config)),
                new XElement("IsSystem", "False"),
                new XElement("Tag"),
                _businessHelper.GetAttributeElements(fieldType, config),
                new XElement("OwnerMetaClass", fieldType.EntityTypeId));
        }

        public XElement EPiMustHaveMetaField(string name)
        {
            return new XElement(
                "MetaField",
                new XElement("Namespace", "Mediachase.Commerce.Catalog"),
                new XElement("Name", name),
                new XElement("FriendlyName", name),
                new XElement("Description", "From inRiver"),
                new XElement("DataType", "LongString"),
                new XElement("Length", 150),
                new XElement("AllowNulls", "True"),
                new XElement("SaveHistory", "False"),
                new XElement("AllowSearch", "True"),
                new XElement("MultiLanguageValue", "True"),
                new XElement("IsSystem", "False"),
                new XElement("Tag"),
                new XElement(
                    "Attributes",
                    new XElement("Attribute", new XElement("Key", "useincomparing"), new XElement("Value", "True"))));
        }

        public XElement EPiSpecificationField(string name)
        {
            return new XElement(
                "MetaField",
                new XElement("Namespace", "Mediachase.Commerce.Catalog"),
                new XElement("Name", name),
                new XElement("FriendlyName", name),
                new XElement("Description", "From inRiver"),
                new XElement("DataType", "LongHtmlString"),
                new XElement("Length", 65000),
                new XElement("AllowNulls", "True"),
                new XElement("SaveHistory", "False"),
                new XElement("AllowSearch", "True"),
                new XElement("MultiLanguageValue", "True"),
                new XElement("IsSystem", "False"),
                new XElement("Tag"),
                new XElement(
                    "Attributes",
                    new XElement("Attribute", new XElement("Key", "useincomparing"), new XElement("Value", "False"))));
        }

        public XElement CreateAssociationTypeElement(LinkType linkType)
        {
            return new XElement(
                "AssociationType",
                new XElement("TypeId", linkType.Id),
                new XElement("Description", linkType.Id));
        }

        public XElement CreateCatalogElement(Entity channel, Configuration config)
        {
            return new XElement(
                "Catalog",
                new XAttribute("name", _epiMappingHelper.GetNameForEntity(channel, config, 100)),
                new XAttribute("lastmodified", channel.LastModified.ToString(Configuration.DateTimeFormatString)),
                new XAttribute("startDate", _businessHelper.GetStartDateFromEntity(channel)),
                new XAttribute("endDate", _businessHelper.GetEndDateFromEntity(channel)),
                new XAttribute("defaultCurrency", config.ChannelDefaultCurrency),
                new XAttribute("weightBase", config.ChannelDefaultWeightBase),
                new XAttribute("defaultLanguage", config.ChannelDefaultLanguage.Name.ToLower()),
                new XAttribute("sortOrder", 0),
                new XAttribute("isActive", "True"),
                new XAttribute(
                    "languages",
                    string.Join(",", _businessHelper.CultureInfosToStringArray(config.LanguageMapping.Keys.ToArray()))));
        }

        public XElement CreateNodeElement(Entity entity, string parentId, int sortOrder, Configuration config)
        {
            return new XElement(
                "Node",
                new XElement("Name", _epiMappingHelper.GetNameForEntity(entity, config, 100)),
                new XElement("StartDate", _businessHelper.GetStartDateFromEntity(entity)),
                new XElement("EndDate", _businessHelper.GetEndDateFromEntity(entity)),
                new XElement("IsActive", !entity.EntityType.IsLinkEntityType),
                new XElement("SortOrder", sortOrder),
                new XElement("DisplayTemplate", _businessHelper.GetDisplayTemplateEntity(entity)),
                new XElement("Guid", GetChannelEntityGuid(config.ChannelId, entity.Id)),
                new XElement("Code", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(entity.Id, config)),
                new XElement(
                    "MetaData",
                    new XElement("MetaClass", new XElement("Name", GetMetaClassForEntity(entity))),
                    new XElement(
                        "MetaFields",
                        GetDisplayXXElement(entity.DisplayName, "DisplayName", config),
                        GetDisplayXXElement(entity.DisplayDescription, "DisplayDescription", config),
                        from f in entity.Fields
                        where !f.IsEmpty() && !_epiMappingHelper.SkipField(f.FieldType, config)
                        select InRiverFieldToMetaField(f, config))),
                new XElement(
                    "ParentNode",
                    string.IsNullOrEmpty(parentId) ? null : _channelPrefixHelper.GetEPiCodeWithChannelPrefix(parentId, config)),
                CreateSEOInfoElement(entity, config));
        }

        public XElement CreateAssociationNodeElement(string name, Configuration config)
        {
            return new XElement(
                "Node",
                new XElement("Name", name),
                new XElement("StartDate", DateTime.UtcNow.ToString("u")),
                new XElement("EndDate", DateTime.UtcNow.AddYears(100).ToString("u")),
                new XElement("IsActive", "True"),
                new XElement("SortOrder", 999),
                new XElement("DisplayTemplate", string.Empty),
                new XElement("Code", config.ChannelIdPrefix + "_inRiverAssociations"),
                new XElement("Guid", new Guid(config.ChannelId, 0, 0, new byte[8])),
                new XElement(
                    "MetaData",
                    new XElement("MetaClass", new XElement("Name", "ChannelNode")),
                    new XElement("MetaFields")),
                new XElement("ParentNode", null),
                new XElement("SeoInfo"));
        }

        public XElement CreateSEOInfoElement(Entity entity, Configuration config)
        {
            XElement seoInfo = new XElement("SeoInfo");
            foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
            {
                string uri = _businessHelper.GetSeoUriFromEntity(entity, culturePair.Value, config);
                string title = _businessHelper.GetSeoTitleFromEntity(entity, culturePair.Value);
                string description = _businessHelper.GetSeoDescriptionFromEntity(entity, culturePair.Value);
                string keywords = _businessHelper.GetSeoKeywordsFromEntity(entity, culturePair.Value);
                string urisegment = _businessHelper.GetSeoUriSegmentFromEntity(entity, culturePair.Value, config);

                if (string.IsNullOrEmpty(uri) && string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description)
                    && string.IsNullOrEmpty(keywords) && string.IsNullOrEmpty(urisegment))
                {
                    continue;
                }

                seoInfo.Add(
                    new XElement(
                        "Seo",
                        new XElement("LanguageCode", culturePair.Key.Name.ToLower()),
                        new XElement("Uri", uri),
                        new XElement("Title", title),
                        new XElement("Description", description),
                        new XElement("Keywords", keywords),
                        new XElement("UriSegment", urisegment)));
            }

            return seoInfo;
        }

        // <Inventory>
        //  <AllowBackorder>True</AllowBackorder>
        //  <AllowPreorder>True</AllowPreorder>
        //  <BackorderAvailabilityDate>2020-01-04 02:00:00Z</BackorderAvailabilityDate>
        //  <BackorderQuantity>6</BackorderQuantity>
        //  <InStockQuantity>10</InStockQuantity>
        //  <InventoryStatus>1</InventoryStatus>
        //  <PreorderAvailabilityDate>2010-09-01 16:00:00Z</PreorderAvailabilityDate>
        //  <PreorderQuantity>4</PreorderQuantity>
        //  <ReorderMinQuantity>3</ReorderMinQuantity>
        //  <ReservedQuantity>2</ReservedQuantity>
        // </Inventory>
        public XElement CreateInventoryInfoElement(Entity entity, Configuration config)
        {
            if (!config.ExportInventoryData)
            {
                return new XElement("Inventory");
            }

            var channelHelper = new ChannelHelper(_context);

            XElement inventoryInfo = new XElement("Inventory");

            string allowBackorder = channelHelper.GetEntityAllowBackorder(entity, config).ToString();
            string allowPreorder = channelHelper.GetEntityAllowPreorder(entity, config).ToString();
            string backorderAvailabilityDate = channelHelper.GetEntityBackorderAvailabilityDate(entity, config).ToString("u");
            string backorderQuantity = channelHelper.GetEntityBackorderQuantity(entity, config).ToString(CultureInfo.InvariantCulture);
            string instockQuantity = channelHelper.GetEntityInStockQuantity(entity, config).ToString(CultureInfo.InvariantCulture);
            string inventoryStatus = channelHelper.GetEntityInventoryStatus(entity, config).ToString(CultureInfo.InvariantCulture);
            string preorderAvailabilityDate = channelHelper.GetEntityPreorderAvailabilityDate(entity, config).ToString("u");
            string preorderQuantity = channelHelper.GetEntityPreorderQuantity(entity, config).ToString(CultureInfo.InvariantCulture);
            string reorderMinQuantity = channelHelper.GetEntityReorderMinQuantity(entity, config).ToString(CultureInfo.InvariantCulture);
            string reservedQuantity = channelHelper.GetEntityReservedQuantity(entity, config).ToString(CultureInfo.InvariantCulture);

            inventoryInfo.Add(
                new XElement("AllowBackorder", allowBackorder),
                new XElement("AllowPreorder", allowPreorder),
                new XElement("BackorderAvailabilityDate", backorderAvailabilityDate),
                new XElement("BackorderQuantity", backorderQuantity),
                new XElement("InStockQuantity", instockQuantity),
                new XElement("InventoryStatus", inventoryStatus),
                new XElement("PreorderAvailabilityDate", preorderAvailabilityDate),
                new XElement("PreorderQuantity", preorderQuantity),
                new XElement("ReorderMinQuantity", reorderMinQuantity),
                new XElement("ReservedQuantity", reservedQuantity));

            return inventoryInfo;
        }

        // <Prices>
        //  <Price>
        //    <MarketId>DEFAULT</MarketId>
        //    <CurrencyCode>USD</CurrencyCode>
        //    <PriceTypeId>0</PriceTypeId>
        //    <PriceCode/>
        //    <ValidFrom>1900-01-01 00:00:00Z</ValidFrom>
        //    <ValidUntil/>
        //    <MinQuantity>0.000000000</MinQuantity>
        //    <UnitPrice>1000.0000</UnitPrice>
        //  </Price>
        // </Prices>
        public XElement CreatePriceInfoElement(Entity entity, Configuration config)
        {
            if (!config.ExportPricingData)
            {
                return new XElement("Prices");
            }

            var channelHelper = new ChannelHelper(_context);


            XElement priceInfo = new XElement("Prices");

            string marketId = channelHelper.GetEntityMarketId(entity, config);
            string currencyCode = channelHelper.GetEntityCurrencyCode(entity, config);
            string priceTypeId = channelHelper.GetEntityPriceTypeId(entity, config).ToString(CultureInfo.InvariantCulture);
            string priceCode = channelHelper.GetEntityPriceCode(entity, config);
            string validFrom = channelHelper.GetEntityValidFrom(entity, config).ToString("u");
            string validUntil = channelHelper.GetEntityValidUntil(entity, config).ToString("u");
            string minQuantity = channelHelper.GetEntityMinQuantity(entity, config).ToString(CultureInfo.InvariantCulture);
            string unitPrice = channelHelper.GetEntityUnitPrice(entity, config).ToString(CultureInfo.InvariantCulture);

            priceInfo.Add(
                new XElement(
                    "Price",
                    new XElement("MarketId", marketId),
                    new XElement("CurrencyCode", currencyCode),
                    new XElement("PriceTypeId", priceTypeId),
                    new XElement("PriceCode", priceCode),
                    new XElement("ValidFrom", validFrom),
                    new XElement("ValidUntil", validUntil),
                    new XElement("MinQuantity", minQuantity),
                    new XElement("UnitPrice", unitPrice)));
            return priceInfo;
        }

        public XElement InRiverEntityToEpiEntry(Entity entity, Configuration config, string skuId = "")
        {
            int skuIdNumber;
            XElement guidElement;
            XElement metaClassElement;
            if (!string.IsNullOrEmpty(skuId) && int.TryParse(skuId, out skuIdNumber))
            {
                guidElement = new XElement("Guid", GetChannelEntityGuid(config.ChannelId, skuIdNumber));
                metaClassElement = new XElement("MetaClass", new XElement("Name", GetMetaClassForEntity(entity, true)));
            }
            else
            {
                guidElement = new XElement("Guid", GetChannelEntityGuid(config.ChannelId, entity.Id));
                metaClassElement = new XElement("MetaClass", new XElement("Name", GetMetaClassForEntity(entity)));
            }

            return new XElement(
                "Entry",
                new XElement("Name", _epiMappingHelper.GetNameForEntity(entity, config, 100)),
                new XElement("StartDate", _businessHelper.GetStartDateFromEntity(entity)),
                new XElement("EndDate", _businessHelper.GetEndDateFromEntity(entity)),
                new XElement("IsActive", "True"),
                new XElement("DisplayTemplate", _businessHelper.GetDisplayTemplateEntity(entity)),
                new XElement("Code", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(entity.Id, config)),
                new XElement("EntryType", _epiMappingHelper.GetEntryType(entity.EntityType.Id, config)),
                guidElement,
                new XElement(
                    "MetaData",
                    metaClassElement,
                    new XElement(
                        "MetaFields",
                        GetDisplayXXElement(_epiMappingHelper.GetDisplayNameField(entity, config), "DisplayName", config),
                        GetDisplayXXElement(entity.DisplayDescription, "DisplayDescription", config),
                        from f in entity.Fields
                        where UseField(entity, f) && !_epiMappingHelper.SkipField(f.FieldType, config)
                        select InRiverFieldToMetaField(f, config))),
                        CreateSEOInfoElement(entity, config),
                        CreateInventoryInfoElement(entity, config),
                        CreatePriceInfoElement(entity, config));


        }

        private Guid GetChannelEntityGuid(int channelId, int entityId)
        {
            var concatIds = channelId.ToString().PadLeft(16, '0') + entityId.ToString().PadLeft(16, '0');
            return new Guid(concatIds);
        }

        public XElement InRiverFieldToMetaField(Field field, Configuration config)
        {
            XElement metaField = new XElement(
                "MetaField",
                new XElement("Name", _epiMappingHelper.GetEPiMetaFieldNameFromField(field.FieldType, config)),
                new XElement("Type", _epiMappingHelper.InRiverDataTypeToEpiType(field.FieldType, config)));

            if (field.FieldType.DataType.Equals(DataType.CVL))
            {
                metaField.Add(_businessHelper.GetCVLValues(field, config));
            }
            else
            {
                if (field.FieldType.DataType.Equals(DataType.LocaleString))
                {
                    LocaleString ls = field.Data as LocaleString;
                    if (!field.IsEmpty())
                    {
                        foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
                        {
                            if (ls != null)
                            {
                                metaField.Add(
                                    new XElement(
                                        "Data",
                                        new XAttribute("language", culturePair.Key.Name.ToLower()),
                                        new XAttribute("value", ls[culturePair.Value])));
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
                        {
                            metaField.Add(
                                new XElement(
                                    "Data",
                                    new XAttribute("language", culturePair.Key.Name.ToLower()),
                                    new XAttribute("value", string.Empty)));
                        }
                    }
                }
                else
                {
                    metaField.Add(
                        new XElement(
                            "Data",
                            new XAttribute("language", config.ChannelDefaultLanguage.Name.ToLower()),
                            new XAttribute("value", _businessHelper.GetFieldDataAsString(field, config))));
                }
            }

            if (field.FieldType.Settings.ContainsKey("EPiDataType"))
            {
                if (field.FieldType.Settings["EPiDataType"] == "ShortString")
                {
                    foreach (XElement dataElement in metaField.Descendants().Where(e => e.Attribute("value") != null))
                    {
                        int lenght = dataElement.Attribute("value").Value.Length;

                        int defaultLength = 150;
                        if (field.FieldType.Settings.ContainsKey("MetaFieldLength"))
                        {
                            if (!int.TryParse(field.FieldType.Settings["MetaFieldLength"], out defaultLength))
                            {
                                defaultLength = 150;
                            }
                        }

                        if (lenght > defaultLength)
                        {
                            _context.Log(LogLevel.Error,
                                string.Format("Field {0} for entity {1} has a longer value [{2}] than defined by MetaFieldLength [{3}]", field.FieldType.Id, field.EntityId, lenght, defaultLength));
                        }
                    }
                }
            }

            return metaField;
        }

        public XElement CreateSimpleMetaFieldElement(string name, string value, Configuration config)
        {
            return new XElement(
                "MetaField",
                new XElement("Name", name),
                new XElement("Type", "ShortString"),
                new XElement(
                    "Data",
                    new XAttribute("language", config.ChannelDefaultLanguage.Name.ToLower()),
                    new XAttribute("value", value)));
        }

        [Obsolete]
        public XElement CreateNodeEntryRelationElement(Link link, Configuration config)
        {
            return CreateNodeEntryRelationElement(link.Source.Id.ToString(CultureInfo.InvariantCulture), link.Target.Id.ToString(CultureInfo.InvariantCulture), link.Index, config);
        }

        public XElement CreateNodeEntryRelationElement(string sourceId, string targetId, int sortOrder, Configuration config, Dictionary<int, Entity> channelEntities = null)
        {
            return new XElement(
                "NodeEntryRelation",
                new XElement("EntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(targetId, config)),
                new XElement("NodeCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(sourceId, config)),
                new XElement("SortOrder", sortOrder));
        }

        public XElement CreateNodeRelationElement(string sourceId, string targetId, int sortOrder, Configuration config)
        {
            return new XElement(
                "NodeRelation",
                new XElement("ChildNodeCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(targetId, config)),
                new XElement("ParentNodeCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(sourceId, config)),
                new XElement("SortOrder", sortOrder));
        }

        [Obsolete]
        public XElement CreateEntryRelationElement(Link link, Configuration config)
        {
            return CreateEntryRelationElement(link.Source.Id.ToString(CultureInfo.InvariantCulture), link.Source.EntityType.Id, link.Target.Id.ToString(CultureInfo.InvariantCulture), link.Index, config);
        }

        public XElement CreateEntryRelationElement(string sourceId, string parentEntityType, string targetId, int sortOrder, Configuration config, Dictionary<int, Entity> channelEntities = null)
        {
            string relationType = "ProductVariation";

            if (!string.IsNullOrEmpty(parentEntityType))
            {
                string sourceType = _epiMappingHelper.GetEntryType(parentEntityType, config);

                // Change it if needed.
                switch (sourceType)
                {
                    case "Package":
                    case "DynamicPackage":
                        relationType = "PackageEntry";
                        break;
                    case "Bundle":
                        relationType = "BundleEntry";
                        break;
                }
            }

            return new XElement(
                "EntryRelation",
                new XElement("ParentEntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(sourceId, config)),
                new XElement("ChildEntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(targetId, config)),
                new XElement("RelationType", relationType),
                new XElement("Quantity", 0),
                new XElement("GroupName", "default"),
                new XElement("SortOrder", sortOrder));
        }

        [Obsolete]
        public XElement CreateCatalogAssociationElement(Link link, Configuration config)
        {
            // Unique Name with no spaces required for EPiServer Commerce
            string name = _epiMappingHelper.GetAssociationName(link, config);
            string description = link.LinkEntity == null ? link.LinkType.Id : _channelPrefixHelper.GetEPiCodeWithChannelPrefix(link.LinkEntity.Id, config);
            description = description ?? string.Empty;

            return new XElement(
                "CatalogAssociation",
                new XElement("Name", name),
                new XElement("Description", description),
                new XElement("SortOrder", link.Index),
                new XElement("EntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(link.Source.Id, config)),
                CreateAssociationElement(link, config));
        }

        public XElement CreateCatalogAssociationElement(StructureEntity structureEntity, Entity linkEntity, Configuration config, Dictionary<int, Entity> channelEntities = null)
        {
            // Unique Name with no spaces required for EPiServer Commerce
            string name = _epiMappingHelper.GetAssociationName(structureEntity, linkEntity, config);
            string description = structureEntity.LinkEntityId == null ? structureEntity.LinkTypeIdFromParent : _channelPrefixHelper.GetEPiCodeWithChannelPrefix(structureEntity.LinkEntityId.Value, config);
            description = description ?? string.Empty;

            return new XElement(
                "CatalogAssociation",
                new XElement("Name", name),
                new XElement("Description", description),
                new XElement("SortOrder", structureEntity.SortOrder),
                new XElement("EntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(structureEntity.ParentId, config)),
                CreateAssociationElement(structureEntity, config));
        }

        [Obsolete]
        public XElement CreateAssociationElement(Link link, Configuration config)
        {
            return new XElement(
                "Association",
                new XElement("EntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(link.Target.Id, config)),
                new XElement("SortOrder", link.Index),
                    new XElement("Type", link.LinkType.Id));
        }

        public XElement CreateAssociationElement(StructureEntity structureEntity, Configuration config)
        {
            return new XElement(
                "Association",
                new XElement("EntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(structureEntity.EntityId, config)),
                new XElement("SortOrder", structureEntity.SortOrder),
                    new XElement("Type", structureEntity.LinkTypeIdFromParent));
        }

        public XElement GetLinkItemFields(Entity linkEntity, Configuration config)
        {
            return new XElement(
                "LinkItemMetaFields",
                from Field f in linkEntity.Fields where !_epiMappingHelper.SkipField(f.FieldType, config) && !f.IsEmpty() select InRiverFieldToMetaField(f, config));
        }

        public XElement CreateResourceElement(Entity resource, string action, Configuration config, Dictionary<int, Entity> parentEntities = null, int imageCount = 0)
        {
            string resourceFileId = "-1";
            Field resourceFileIdField = resource.GetField("ResourceFileId");
            if (resourceFileIdField != null && !resourceFileIdField.IsEmpty())
            {
                resourceFileId = resource.GetField("ResourceFileId").Data.ToString();
            }

            Dictionary<string, int?> parents = new Dictionary<string, int?>();

            string resourceId = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(resource.Id, config);
            resourceId = resourceId.Replace("_", string.Empty);

            if (action == "unlinked")
            {
                var resourceParents = config.ChannelEntities.Where(i => !i.Key.Equals(resource.Id));

                foreach (KeyValuePair<int, Entity> resourceParent in resourceParents)
                {
                    List<string> ids = new List<string> { resourceParent.Value.Id.ToString(CultureInfo.InvariantCulture) };

                    if (config.ItemsToSkus && resourceParent.Value.EntityType.Id == "Item")
                    {
                        List<string> skuIds = SkuItemIds(resourceParent.Value, config);

                        foreach (string skuId in skuIds)
                        {
                            ids.Add(skuId);
                        }

                        if (config.UseThreeLevelsInCommerce == false)
                        {
                            ids.Remove(resourceParent.Value.Id.ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    foreach (string id in ids)
                    {
                        if (!parents.ContainsKey(id))
                        {
                            Link productResourceLink = GetResourceOutBoundLink(resourceParent.Value, resource, "ProductResourceMedia");

                            if (productResourceLink == null)
                            {
                                productResourceLink = GetResourceOutBoundLink(resourceParent.Value, resource, "ProductResourceDocuments");

                                parents.Add(id, productResourceLink?.Index + imageCount);
                            }
                            else
                            {
                                parents.Add(id, productResourceLink.Index);
                            } 
                        }
                    }
                }
            }
            else
            {
                List<StructureEntity> allResourceLocations = config.ChannelStructureEntities.FindAll(i => i.EntityId.Equals(resource.Id));

                List<Link> links = new List<Link>();

                foreach (Link inboundLink in resource.InboundLinks)
                {
                    if (allResourceLocations.Exists(i => i.ParentId.Equals(inboundLink.Source.Id)))
                    {
                        links.Add(inboundLink);
                    }
                }

                foreach (Link link in links)
                {
                    Entity linkedEntity = link.Source;
                    List<string> ids = new List<string> { linkedEntity.Id.ToString(CultureInfo.InvariantCulture) };
                    if (config.ItemsToSkus && linkedEntity.EntityType.Id == "Item")
                    {
                        List<string> skuIds = SkuItemIds(linkedEntity, config);
                        foreach (string skuId in skuIds)
                        {
                            ids.Add(skuId);
                        }

                        if (config.UseThreeLevelsInCommerce == false)
                        {
                            ids.Remove(linkedEntity.Id.ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    foreach (string id in ids)
                    {
                        if (!parents.ContainsKey(id))
                        {
                            int parentId = default(int);

                            if (int.TryParse(id, out parentId))
                            {
                                Link productResourceMediaLink = null;
                                List<Link> outboundMediaLinks = _context.ExtensionManager.DataService.GetOutboundLinksForEntityAndLinkType(parentId, "ProductResourceMedia");
                                productResourceMediaLink = outboundMediaLinks?.SingleOrDefault(outboundLink => outboundLink.Target.Id == resource.Id);

                                if (productResourceMediaLink == null)
                                {
                                    List<Link> outboundDocumentLinks = _context.ExtensionManager.DataService.GetOutboundLinksForEntityAndLinkType(parentId, "ProductResourceDocuments");
                                    productResourceMediaLink = outboundDocumentLinks?.SingleOrDefault(outboundLink => outboundLink.Target.Id == resource.Id);

                                    int? index = productResourceMediaLink?.Index + imageCount;

                                    parents.Add(id, index);

                                    if (productResourceMediaLink != null)
                                    {
                                        _context?.Log(LogLevel.Debug, string.Format("Episerver found document with index {0}", index));
                                    }
                                }
                                else
                                {
                                    _context?.Log(LogLevel.Debug, string.Format("Episerver found image with index {0}", productResourceMediaLink.Index));
                                    parents.Add(id, productResourceMediaLink.Index);
                                }
                            }
                            else
                            {
                                parents.Add(id, null);
                            }
                        }
                    }
                }

                if (parents.Any() && parentEntities != null)
                {
                    List<int> nonExistingIds =
                        (from id in parents.Keys where !parentEntities.ContainsKey(int.Parse(id)) select int.Parse(id))
                            .ToList();

                    if (nonExistingIds.Any())
                    {
                        foreach (Entity entity in _context.ExtensionManager.DataService.GetEntities(nonExistingIds, LoadLevel.DataOnly))
                        {
                            if (!parentEntities.ContainsKey(entity.Id))
                            {
                                parentEntities.Add(entity.Id, entity);
                            }
                        }
                    }
                }
            }

            var resources = new Resources(_context);

            return new XElement(
                "Resource",
                new XAttribute("id", resourceId),
                new XAttribute("action", action),
                new XElement(
                    "ResourceFields",
                    resource.Fields.Where(field => !_epiMappingHelper.SkipField(field.FieldType, config))
                        .Select(field => InRiverFieldToMetaField(field, config))),
                resources.GetInternalPathsInZip(resource, config),
                new XElement(
                    "ParentEntries",
                    parents.Select(
                        (parent, index) =>
                        new XElement("EntryCode", _channelPrefixHelper.GetEPiCodeWithChannelPrefix(parent.Key, config), new XAttribute("SortOrder",  parent.Value ?? index + imageCount)))));
        }

        public XElement CreateResourceMetaFieldsElement(EntityType resourceType, Configuration config)
        {
            return new XElement(
                "ResourceMetaFields",
                resourceType.FieldTypes.Select(
                    fieldtype =>
                    new XElement(
                        "ResourceMetaField",
                        new XElement("FieldName", _epiMappingHelper.GetEPiMetaFieldNameFromField(fieldtype, config)),
                        new XElement("FriendlyName", _epiMappingHelper.GetEPiMetaFieldNameFromField(fieldtype, config)),
                        new XElement("Description", _epiMappingHelper.GetEPiMetaFieldNameFromField(fieldtype, config)),
                        new XElement("FieldType", _epiMappingHelper.InRiverDataTypeToEpiType(fieldtype, config)),
                        new XElement("Format", "Text"),
                        new XElement("MaximumLength", _epiMappingHelper.GetMetaFieldLength(fieldtype, config)),
                        new XElement("AllowNulls", _businessHelper.GetAllowsNulls(fieldtype, config)),
                        new XElement("UniqueValue", fieldtype.Unique))));
        }

        public XElement GetMetaClassesFromFieldSets(Configuration config)
        {
            List<XElement> metaClasses = new List<XElement>();
            List<XElement> metafields = new List<XElement>();

            XElement diaplyNameElement = EPiMustHaveMetaField("DisplayName");
            XElement displayDescriptionElement = EPiMustHaveMetaField("DisplayDescription");
            XElement specification = EPiSpecificationField("SpecificationField");
            bool addSpec = false;

            foreach (EntityType entityType in config.ExportEnabledEntityTypes)
            {
                if (entityType.LinkTypes.Find(a => a.TargetEntityTypeId == "Specification") != null && entityType.Id != "Specification")
                {
                    specification.Add(new XElement("OwnerMetaClass", entityType.Id));
                    foreach (FieldSet fieldSet in entityType.FieldSets)
                    {
                        string name = entityType.Id + "_" + fieldSet.Id;
                        specification.Add(new XElement("OwnerMetaClass", name));
                    }

                    addSpec = true;
                }

                Dictionary<string, List<XElement>> fieldTypesFieldSets = new Dictionary<string, List<XElement>>();
                metaClasses.Add(InRiverEntityTypeToMetaClass(entityType.Id, entityType.Id));
                foreach (FieldSet fieldset in entityType.FieldSets)
                {
                    string name = entityType.Id + "_" + fieldset.Id;
                    metaClasses.Add(InRiverEntityTypeToMetaClass(name, entityType.Id));
                    foreach (string fieldTypeName in fieldset.FieldTypes)
                    {
                        if (!fieldTypesFieldSets.ContainsKey(fieldTypeName))
                        {
                            fieldTypesFieldSets.Add(fieldTypeName, new List<XElement> { new XElement("OwnerMetaClass", name) });
                        }
                        else
                        {
                            fieldTypesFieldSets[fieldTypeName].Add(new XElement("OwnerMetaClass", name));
                        }
                    }

                    diaplyNameElement.Add(new XElement("OwnerMetaClass", name));
                    displayDescriptionElement.Add(new XElement("OwnerMetaClass", name));
                }

                diaplyNameElement.Add(new XElement("OwnerMetaClass", entityType.Id));
                displayDescriptionElement.Add(new XElement("OwnerMetaClass", entityType.Id));
                foreach (FieldType fieldType in entityType.FieldTypes)
                {
                    if (_epiMappingHelper.SkipField(fieldType, config))
                    {
                        continue;
                    }

                    XElement metaField = InRiverFieldTypeToMetaField(fieldType, config);

                    if (fieldTypesFieldSets.ContainsKey(fieldType.Id))
                    {
                        foreach (XElement element in fieldTypesFieldSets[fieldType.Id])
                        {
                            metaField.Add(element);
                        }
                    }
                    else
                    {
                        foreach (FieldSet fieldSet in entityType.FieldSets)
                        {
                            string name = entityType.Id + "_" + fieldSet.Id;
                            metaField.Add(new XElement("OwnerMetaClass", name));
                        }
                    }

                    if (fieldType.DataType.Equals(DataType.CVL))
                    {
                        metaField.Add(_epiMappingHelper.GetDictionaryValues(fieldType, config));
                    }

                    if (metafields.Any(mf =>
                    {
                        XElement nameElement = mf.Element("Name");
                        return nameElement != null && nameElement.Value.Equals(_epiMappingHelper.GetEPiMetaFieldNameFromField(fieldType, config));
                    }))
                    {
                        XElement existingMetaField = metafields.FirstOrDefault(mf =>
                        {
                            XElement nameElement = mf.Element("Name");
                            return nameElement != null && nameElement.Value.Equals(_epiMappingHelper.GetEPiMetaFieldNameFromField(fieldType, config));
                        });
                        if (existingMetaField != null)
                        {
                            var movefields = metaField.Elements("OwnerMetaClass");
                            existingMetaField.Add(movefields);
                        }
                    }
                    else
                    {
                        metafields.Add(metaField);
                    }
                }
            }

            metafields.Add(diaplyNameElement);
            metafields.Add(displayDescriptionElement);
            if (addSpec)
            {
                metafields.Add(specification);
            }

            return new XElement("MetaDataPlusBackup", new XAttribute("version", "1.0"), metaClasses.ToArray(), metafields.ToArray());
        }

        public List<XElement> GenerateSkuItemElemetsFromItem(Entity item, Configuration configuration)
        {
            XDocument skuDoc = SkuFieldToDocument(item, configuration);
            if (skuDoc.Root == null || skuDoc.Element("SKUs") == null)
            {
                return new List<XElement>();
            }

            Link specLink = item.OutboundLinks.Find(l => l.Target.EntityType.Id == "Specification");
            XElement specificationMetaField = null;
            if (specLink != null)
            {
                specificationMetaField = new XElement(
                    "MetaField",
                    new XElement("Name", "SpecificationField"),
                    new XElement("Type", "LongHtmlString"));
                foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in configuration.LanguageMapping)
                {
                    string htmlData = _context.ExtensionManager.DataService.GetSpecificationAsHtml(
                        specLink.Target.Id,
                        item.Id,
                        culturePair.Value);
                    specificationMetaField.Add(
                        new XElement(
                            "Data",
                            new XAttribute("language", culturePair.Key.Name.ToLower()),
                            new XAttribute("value", htmlData)));
                }
            }

            List<XElement> skuElements = new List<XElement>();
            XElement skuElement = skuDoc.Element("SKUs");
            if (skuElement != null)
            {
                foreach (XElement sku in skuElement.Elements())
                {
                    string id = sku.Attribute("id").Value;
                    if (string.IsNullOrEmpty(id))
                    {
                        _context.Log(
                            LogLevel.Information,
                            string.Format("Could not find the id for the SKU data for item: {0}", item.Id));
                        continue;
                    }

                    XElement itemElement = InRiverEntityToEpiEntry(item, configuration, id);
                    XElement nameElement = sku.Element("Name");
                    if (nameElement != null)
                    {
                        string name = (!string.IsNullOrEmpty(nameElement.Value)) ? nameElement.Value : id;
                        XElement itemElementName = itemElement.Element("Name");
                        if (itemElementName != null)
                        {
                            itemElementName.Value = name;
                        }
                    }

                    XElement codeElement = itemElement.Element("Code");
                    if (codeElement != null)
                    {
                        codeElement.Value = _channelPrefixHelper.GetEPiCodeWithChannelPrefix(id, configuration);
                    }

                    XElement entryTypeElement = itemElement.Element("EntryType");
                    if (entryTypeElement != null)
                    {
                        entryTypeElement.Value = "Variation";
                    }

                    XElement skuDataElement = sku.Element(Configuration.SKUData);
                    if (skuDataElement != null)
                    {
                        foreach (XElement skuData in skuDataElement.Elements())
                        {
                            XElement metaDataElement = itemElement.Element("MetaData");
                            if (metaDataElement != null && metaDataElement.Element("MetaFields") != null)
                            {
                                // ReSharper disable once PossibleNullReferenceException
                                metaDataElement.Element("MetaFields").Add(CreateSimpleMetaFieldElement(skuData.Name.LocalName, skuData.Value, configuration));
                            }
                        }
                    }

                    if (specificationMetaField != null)
                    {
                        XElement metaDataElement = itemElement.Element("MetaData");
                        if (metaDataElement != null && metaDataElement.Element("MetaFields") != null)
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            metaDataElement.Element("MetaFields").Add(specificationMetaField);
                        }
                    }

                    skuElements.Add(itemElement);
                }
            }

            return skuElements;
        }

        public XDocument SkuFieldToDocument(Entity item, Configuration configuration)
        {
            Field skuField = item.GetField(Configuration.SKUFieldName);
            if (skuField == null || skuField.Data == null)
            {
                XElement itemElement = InRiverEntityToEpiEntry(item, configuration);
                _context.Log(
                    LogLevel.Information,
                    string.Format("Could not find SKU data for item: {0}", item.Id));
                return new XDocument(itemElement);
            }

            return XDocument.Parse(skuField.Data.ToString());
        }

        public List<string> SkuItemIds(Entity item, Configuration configuration)
        {
            Field skuField = item.GetField(Configuration.SKUFieldName);
            if (skuField == null || skuField.IsEmpty())
            {
                return new List<string> { item.Id.ToString(CultureInfo.InvariantCulture) };
            }

            XDocument skuDoc = SkuFieldToDocument(item, configuration);

            XElement skusElement = skuDoc.Element("SKUs");
            if (skusElement != null)
            {
                return
                    (from skuElement in skusElement.Elements()
                     where skuElement.HasAttributes
                     select skuElement.Attribute("id").Value).ToList();
            }

            return new List<string>();
        }


        private Link GetResourceOutBoundLink(Entity productEntity, Entity resourceEntity, string linkTypeId)
        {
            Link ProductResourceLink = null;

            if (productEntity.LoadLevel != LoadLevel.DataAndLinks)
            {
                List<Link> mediaLinks = _context.ExtensionManager.DataService.GetOutboundLinksForEntityAndLinkType(productEntity.Id, linkTypeId);

                ProductResourceLink = mediaLinks?.SingleOrDefault(link => link.Target.Id == resourceEntity.Id);
            }
            else
            {
                ProductResourceLink = productEntity.Links?.SingleOrDefault(link => link.Target.Id == resourceEntity.Id && link.LinkType.Id == linkTypeId);
            }

            return ProductResourceLink;
        }


        // ReSharper disable once InconsistentNaming
        private XElement GetDisplayXXElement(Field displayField, string name, Configuration config)
        {
            if (displayField == null || displayField.IsEmpty())
            {
                return new XElement(
                    "MetaField",
                    new XElement("Name", name),
                    new XElement("Type", "LongHtmlString"),
                    new XElement(
                        "Data",
                        new XAttribute("language", config.ChannelDefaultLanguage.Name.ToLower()),
                        new XAttribute("value", string.Empty)));
            }

            XElement element = InRiverFieldToMetaField(displayField, config);
            XElement nameElement = element.Element("Name");
            if (nameElement != null)
            {
                nameElement.Value = name;
            }

            XElement typeElement = element.Element("Type");
            if (typeElement != null)
            {
                typeElement.Value = "LongHtmlString";
            }

            return element;
        }

        private bool UseField(Entity entity, Field field)
        {
            if (!field.FieldType.ExcludeFromDefaultView)
            {
                return true;
            }

            List<FieldSet> otherFieldSets = entity.EntityType.FieldSets.Where(fs => !fs.Id.Equals(entity.FieldSetId)).ToList();
            if (otherFieldSets.Count == 0)
            {
                return true;
            }

            FieldSet fieldSet = entity.EntityType.FieldSets.Find(fs => fs.Id.Equals(entity.FieldSetId));
            if (fieldSet != null)
            {
                if (fieldSet.FieldTypes.Contains(field.FieldType.Id))
                {
                    return true;
                }
            }

            foreach (FieldSet fs in otherFieldSets)
            {
                if (fs.FieldTypes.Contains(field.FieldType.Id))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetMetaClassForEntity(Entity entity, bool skuItem = false)
        {
            string result;

            if (skuItem)
            {
                result = entity.EntityType.Id + "_SKU_";
            }
            else
            {
                result = entity.EntityType.Id;
            }

            if (!string.IsNullOrEmpty(entity.FieldSetId) && entity.EntityType.FieldSets.Any(fs => fs.Id == entity.FieldSetId))
            {
                if (skuItem)
                {
                    result = entity.EntityType.Id + "_SKU_" + entity.FieldSetId;
                }
                else
                {
                    result = entity.EntityType.Id + "_" + entity.FieldSetId;
                }
            }

            return result;
        }
    }
}
