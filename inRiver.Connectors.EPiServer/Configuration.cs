using inRiver.Connectors.EPiServer.Enums;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace inRiver.Connectors.EPiServer
{
    public class Configuration
    {
        private static readonly string[] ExportDisabledEntityTypes = { "Channel", "Assortment", "Resource", "Task", "Section", "Publication" };

        private static List<EntityType> exportEnabledEntityTypes;

        private readonly Dictionary<string, string> settings;

        private readonly List<string> epiFieldsIninRiver;

        private bool? modifyFilterBehavior;

        private Dictionary<CultureInfo, CultureInfo> languageMapping;

        private Dictionary<string, string> epiNameMapping;

        private bool? useThreeLevelsInCommerce;

        private CultureInfo channelDefaultLanguage;

        private string channelDefaultCurrency;

        private bool? exportInventoryData;

        private bool? exportPricingData;

        private Dictionary<string, string> epiCodeMapping;

        private string channelWeightBase;

        private string channelIdPrefix = string.Empty;

        private Dictionary<string, string> channelMimeTypeMappings = new Dictionary<string, string>();

        private bool? channelAllowBackorder;

        private bool? channelAllowPreorder;

        private DateTime? channelBackorderAvailabilityDate;

        private int? channelBackorderQuantity;

        private int? channelInStockQuantity;

        private int? channelInventoryStatus;

        private DateTime? channelPreorderAvailabilityDate;

        private int? channelPreorderQuantity;

        private int? channelReorderMinQuantity;

        private int? channelReservedQuantity;

        private string channelMarketId;

        private string channelCurrencyCode;

        private int? channelPriceTypeId;

        private string channelPriceCode;

        private DateTime? channelValidFrom;

        private DateTime? channelValidUntil;

        private double? channelMinQuantity;

        private double? channelUnitPrice;

        private Dictionary<string, string> resourceConfiugurationExtensions;

        private List<LinkType> exportEnabledLinkTypes;

        private bool itemsToSkus;

        private HashSet<string> excludedFields;

        private int batchsize;

        private bool? enableEPIEndPoint;

        private inRiverContext _context;

        private IEnumerable<string> epiMetaFieldAttributes;

        public Configuration(inRiverContext context)
        {
            _context = context;
            settings = context.Settings;
            LinkTypes = new List<LinkType>(context.ExtensionManager.ModelService.GetAllLinkTypes());
            epiFieldsIninRiver = new List<string> { "startdate", "enddate", "displaytemplate", "seodescription", "seokeywords", "seotitle", "seouri", "skus" };
            ChannelStructureEntities = new List<StructureEntity>();
            ChannelEntities = new Dictionary<int, Entity>();

            context.Log(LogLevel.Debug, $"A new instance of {GetType().FullName} was created.");
        }

        public List<EntityType> ExportEnabledEntityTypes
        {
            get
            {
                return exportEnabledEntityTypes ?? (exportEnabledEntityTypes = (from entityType in _context.ExtensionManager.ModelService.GetAllEntityTypes()
                                                                                where !ExportDisabledEntityTypes.Contains(entityType.Id)
                                                                                select entityType).ToList());
            }
        }

        public static string DateTimeFormatString
        {
            get
            {
                return "yyyy-MM-dd HH:mm:ss";
            }
        }

        public static string ExportFileName
        {
            get
            {
                return "Catalog.xml";
            }
        }

        public static string DeletefolderDateTime
        {
            get
            {
                return "Deleted";
            }
        }

        public static string MimeType
        {
            get
            {
                return "ResourceMimeType";
            }
        }

        public static string OriginalDisplayConfiguration
        {
            get
            {
                return "Original";
            }
        }

        public static string CVLKeyDelimiter
        {
            get
            {
                return "||";
            }
        }

        public static string CVLKeyValueDelimiter
        {
            get
            {
                return "|;";
            }
        }

        public static string CVLLocaleStringDelimiter
        {
            get
            {
                return "|,";
            }
        }

        public static string EPiCommonField
        {
            get
            {
                return "EPiMetaFieldName";
            }
        }

        #region SKU Related

        public static string SKUFieldName
        {
            get
            {
                return "SKUs";
            }
        }

        public static string SKUData
        {
            get
            {
                return "Data";
            }
        }

        #endregion

        public XDocument MappingDocument { get; set; }

        public string Id { get; private set; }

        public List<LinkType> LinkTypes { get; set; }

        public int ChannelId
        {
            get
            {
                if (!settings.ContainsKey("CHANNEL_ID"))
                {
                    return 0;
                }

                return int.Parse(settings["CHANNEL_ID"]);
            }
        }

        public int EpiMajorVersion
        {
            get
            {
                if (!settings.ContainsKey("EPI_MAJOR_VERSION"))
                {
                    return 8;
                }

                return int.Parse(settings["EPI_MAJOR_VERSION"]);
            }
        }

        public bool ModifyFilterBehavior
        {
            get
            {
                if (modifyFilterBehavior == null)
                {
                    if (!settings.ContainsKey("MODIFY_FILTER_BEHAVIOR"))
                    {
                        modifyFilterBehavior = false;
                        return false;
                    }

                    string value = settings["MODIFY_FILTER_BEHAVIOR"];
                    if (!string.IsNullOrEmpty(value))
                    {
                        modifyFilterBehavior = bool.Parse(value);
                    }
                    else
                    {
                        modifyFilterBehavior = false;
                    }
                }

                return (bool)modifyFilterBehavior;
            }
        }

        public string PublicationsRootPath
        {
            get
            {
                if (!settings.ContainsKey("PUBLISH_FOLDER"))
                {
                    return @"C:\temp\Publish\Epi";
                }

                return settings["PUBLISH_FOLDER"];
            }
        }

        public string HttpPostUrl
        {
            get
            {
                if (!settings.ContainsKey("HTTP_POST_URL"))
                {
                    return string.Empty;
                }

                return settings["HTTP_POST_URL"];
            }
        }

        public Dictionary<CultureInfo, CultureInfo> LanguageMapping
        {
            get
            {
                if (languageMapping == null)
                {
                    if (!settings.ContainsKey("LANGUAGE_MAPPING"))
                    {
                        return new Dictionary<CultureInfo, CultureInfo>();
                    }

                    string mappingXml = settings["LANGUAGE_MAPPING"];

                    Dictionary<CultureInfo, CultureInfo> languageMapping2 = new Dictionary<CultureInfo, CultureInfo>();

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(mappingXml);

                    List<CultureInfo> allLanguages = _context.ExtensionManager.UtilityService.GetAllLanguages();

                    if (doc.DocumentElement != null)
                    {
                        foreach (XmlNode languageNode in doc.DocumentElement)
                        {
                            XmlElement epiLanguage = languageNode["epi"];
                            XmlElement inriverLanguage = languageNode["inriver"];
                            if (epiLanguage != null && inriverLanguage != null)
                            {
                                CultureInfo epiCi = new CultureInfo(epiLanguage.InnerText);
                                CultureInfo pimCi = new CultureInfo(inriverLanguage.InnerText);

                                if (!allLanguages.Exists(ci => ci.LCID == pimCi.LCID))
                                {
                                    throw new Exception(
                                        string.Format(
                                            "ERROR: Mapping Language incorrect, {0} is not a valid pim culture info",
                                            inriverLanguage.InnerText));
                                }

                                languageMapping2.Add(epiCi, pimCi);
                            }
                            else
                            {
                                throw new Exception("ERROR: Mapping language is missing.");
                            }
                        }
                    }

                    languageMapping = languageMapping2;
                }

                return languageMapping;
            }

            set
            {
                languageMapping = value;
            }
        }

        public Dictionary<string, string> EpiNameMapping
        {
            get
            {
                if (epiNameMapping == null)
                {
                    if (!settings.ContainsKey("EPI_NAME_FIELDS"))
                    {
                        epiNameMapping = new Dictionary<string, string>();

                        return epiNameMapping;
                    }

                    string value = settings["EPI_NAME_FIELDS"];

                    epiNameMapping = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(value))
                    {
                        List<FieldType> fieldTypes = _context.ExtensionManager.ModelService.GetAllFieldTypes();

                        string[] values = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string val in values)
                        {
                            if (string.IsNullOrEmpty(val))
                            {
                                continue;
                            }

                            FieldType fieldType = fieldTypes.FirstOrDefault(fT => fT.Id.ToLower() == val.ToLower());
                            if (fieldType != null && !epiNameMapping.ContainsKey(fieldType.EntityTypeId))
                            {
                                epiNameMapping.Add(fieldType.EntityTypeId, fieldType.Id);
                            }
                        }
                    }
                }

                return epiNameMapping;
            }
        }

        public string ResourcesRootPath
        {
            get
            {
                if (!settings.ContainsKey("PUBLISH_FOLDER_RESOURCES"))
                {
                    return @"C:\temp\Publish\Epi\Resources";
                }

                return settings["PUBLISH_FOLDER_RESOURCES"];
            }
        }

        public bool UseThreeLevelsInCommerce
        {
            get
            {
                if (useThreeLevelsInCommerce == null)
                {
                    if (!settings.ContainsKey("USE_THREE_LEVELS_IN_COMMERCE"))
                    {
                        useThreeLevelsInCommerce = false;
                        return false;
                    }

                    string value = settings["USE_THREE_LEVELS_IN_COMMERCE"];

                    if (!string.IsNullOrEmpty(value))
                    {
                        useThreeLevelsInCommerce = bool.Parse(value);
                    }
                    else
                    {
                        useThreeLevelsInCommerce = false;
                    }
                }

                return (bool)useThreeLevelsInCommerce;
            }
        }

        public CultureInfo ChannelDefaultLanguage
        {
            get
            {
                return channelDefaultLanguage ?? (channelDefaultLanguage = new CultureInfo("en-us"));
            }

            set
            {
                channelDefaultLanguage = value;
            }
        }

        public string ChannelDefaultCurrency
        {
            get
            {
                if (string.IsNullOrEmpty(channelDefaultCurrency))
                {
                    channelDefaultCurrency = "usd";
                }

                return channelDefaultCurrency;
            }

            set
            {
                channelDefaultCurrency = value;
            }
        }

        public bool ExportInventoryData
        {
            get
            {
                if (exportInventoryData == null)
                {
                    if (!settings.ContainsKey("EXPORT_INVENTORY_DATA"))
                    {
                        exportInventoryData = false;
                        return false;
                    }

                    string value = settings["EXPORT_INVENTORY_DATA"];

                    if (!string.IsNullOrEmpty(value))
                    {
                        exportInventoryData = bool.Parse(value);
                    }
                    else
                    {
                        exportInventoryData = false;
                    }
                }

                return (bool)exportInventoryData;
            }
        }

        public bool ExportPricingData
        {
            get
            {
                if (exportPricingData == null)
                {
                    if (!settings.ContainsKey("EXPORT_PRICING_DATA"))
                    {
                        exportPricingData = false;
                        return false;
                    }

                    string value = settings["EXPORT_PRICING_DATA"];

                    if (!string.IsNullOrEmpty(value))
                    {
                        exportPricingData = bool.Parse(value);
                    }
                    else
                    {
                        exportPricingData = false;
                    }
                }

                return (bool)exportPricingData;
            }
        }

        public Dictionary<string, string> EpiCodeMapping
        {
            get
            {
                if (epiCodeMapping == null)
                {
                    if (!settings.ContainsKey("EPI_CODE_FIELDS"))
                    {
                        epiCodeMapping = new Dictionary<string, string>();

                        return epiCodeMapping;
                    }

                    string value = settings["EPI_CODE_FIELDS"];

                    epiCodeMapping = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(value))
                    {
                        List<FieldType> fieldTypes = _context.ExtensionManager.ModelService.GetAllFieldTypes();

                        string[] values = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string val in values)
                        {
                            if (string.IsNullOrEmpty(val))
                            {
                                continue;
                            }

                            FieldType fieldType = fieldTypes.FirstOrDefault(fT => fT.Id.ToLower() == val.ToLower());
                            if (fieldType != null && !epiCodeMapping.ContainsKey(fieldType.EntityTypeId))
                            {
                                epiCodeMapping.Add(fieldType.EntityTypeId, fieldType.Id);
                            }
                        }
                    }
                }

                return epiCodeMapping;
            }
        }
        /// <summary>
        /// Retuns FieldType settings that should be sent as attributes to Epi.
        /// If no values then returns an empty IEnumerable 
        /// </summary>
        public IEnumerable<string> EpiMetaFieldAttributes
        {
            get
            {
                string metaFieldNames = null;

                if (epiMetaFieldAttributes == null
                    && settings.TryGetValue("EPI_META_FIELD_ATTRIBUTES", out metaFieldNames)
                    && !string.IsNullOrWhiteSpace(metaFieldNames))
                {
                    if (metaFieldNames.Contains(","))
                    {
                        epiMetaFieldAttributes = metaFieldNames.Replace(" ", string.Empty).Split(',');
                    }
                    else
                    {
                        epiMetaFieldAttributes = new string[1] { metaFieldNames };
                    }
                }

                if (epiMetaFieldAttributes == null)
                {
                    epiMetaFieldAttributes = Enumerable.Empty<string>();
                }

                return epiMetaFieldAttributes;
            }
        }

        public List<StructureEntity> ChannelStructureEntities { get; set; }

        public Dictionary<int, Entity> ChannelEntities { get; set; }

        public string ChannelDefaultWeightBase
        {
            get
            {
                if (string.IsNullOrEmpty(channelWeightBase))
                {
                    channelWeightBase = "lbs";
                }

                return channelWeightBase;
            }

            set
            {
                channelWeightBase = value;
            }
        }

        public string ChannelIdPrefix
        {
            get
            {
                return channelIdPrefix;
            }

            set
            {
                channelIdPrefix = value;
            }
        }

        public Dictionary<string, string> ChannelMimeTypeMappings
        {
            get
            {
                return channelMimeTypeMappings;
            }

            set
            {
                channelMimeTypeMappings = value;
            }
        }

        public bool ChannelAllowBackorder
        {
            get
            {
                return channelAllowBackorder ?? true;
            }

            set
            {
                channelAllowBackorder = value;
            }
        }

        public bool ChannelAllowPreorder
        {
            get
            {
                return channelAllowPreorder ?? true;
            }

            set
            {
                channelAllowPreorder = value;
            }
        }

        public DateTime ChannelBackorderAvailabilityDate
        {
            get
            {
                return channelBackorderAvailabilityDate ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            set
            {
                channelBackorderAvailabilityDate = value;
            }
        }

        public int ChannelBackorderQuantity
        {
            get
            {
                return channelBackorderQuantity ?? 6;
            }

            set
            {
                channelBackorderQuantity = value;
            }
        }

        public int ChannelInStockQuantity
        {
            get
            {
                return channelInStockQuantity ?? 10;
            }

            set
            {
                channelInStockQuantity = value;
            }
        }

        public int ChannelInventoryStatus
        {
            get
            {
                return channelInventoryStatus ?? 1;
            }

            set
            {
                channelInventoryStatus = value;
            }
        }

        public DateTime ChannelPreorderAvailabilityDate
        {
            get
            {
                return channelPreorderAvailabilityDate ?? new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            set
            {
                channelPreorderAvailabilityDate = value;
            }
        }

        public int ChannelPreorderQuantity
        {
            get
            {
                return channelPreorderQuantity ?? 4;
            }

            set
            {
                channelPreorderQuantity = value;
            }
        }

        public int ChannelReorderMinQuantity
        {
            get
            {
                return channelReorderMinQuantity ?? 3;
            }

            set
            {
                channelReorderMinQuantity = value;
            }
        }

        public int ChannelReservedQuantity
        {
            get
            {
                return channelReservedQuantity ?? 2;
            }

            set
            {
                channelReservedQuantity = value;
            }
        }

        public string ChannelMarketId
        {
            get
            {
                return channelMarketId ?? "DEFAULT";
            }

            set
            {
                channelMarketId = value;
            }
        }

        public string ChannelCurrencyCode
        {
            get
            {
                return channelCurrencyCode ?? "USD";
            }

            set
            {
                channelCurrencyCode = value;
            }
        }

        public int ChannelPriceTypeId
        {
            get
            {
                return channelPriceTypeId ?? 0;
            }

            set
            {
                channelPriceTypeId = value;
            }
        }

        public string ChannelPriceCode
        {
            get
            {
                return channelPriceCode ?? string.Empty;
            }

            set
            {
                channelPriceCode = value;
            }
        }

        public DateTime ChannelValidFrom
        {
            get
            {
                return channelValidFrom ?? new DateTime(1967, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            set
            {
                channelValidFrom = value;
            }
        }

        public DateTime ChannelValidUntil
        {
            get
            {
                return channelValidUntil ?? new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            set
            {
                channelValidUntil = value;
            }
        }

        public double ChannelMinQuantity
        {
            get
            {
                return channelMinQuantity ?? 0.0;
            }

            set
            {
                channelMinQuantity = value;
            }
        }

        public double ChannelUnitPrice
        {
            get
            {
                return channelUnitPrice ?? 0.0;
            }

            set
            {
                channelUnitPrice = value;
            }
        }

        public string[] ResourceConfigurations
        {
            get
            {
                if (!settings.ContainsKey("RESOURCE_CONFIGURATION"))
                {
                    return new string[0];
                }

                Dictionary<string, string> resourceConfWithExt = ParseResourceConfig(settings["RESOURCE_CONFIGURATION"]);
                return resourceConfWithExt.Keys.ToArray();
            }
        }

        public Dictionary<string, string> ResourceConfiugurationExtensions
        {
            get
            {
                return resourceConfiugurationExtensions
                       ?? (resourceConfiugurationExtensions =
                           ParseResourceConfig(settings["RESOURCE_CONFIGURATION"]));
            }
        }

        public LinkType[] ExportEnabledLinkTypes
        {
            get
            {
                if (exportEnabledLinkTypes == null)
                {
                    exportEnabledLinkTypes = new List<LinkType>();
                    List<LinkType> allLinkTypes = _context.ExtensionManager.ModelService.GetAllLinkTypes();

                    LinkType firstProdItemLink = allLinkTypes.Where(
                        lt => lt.SourceEntityTypeId.Equals("Product") && lt.TargetEntityTypeId.Equals("Item"))
                        .OrderBy(l => l.Index)
                        .FirstOrDefault();

                    foreach (LinkType linkType in allLinkTypes)
                    {
                        // ChannelNode links and  Product to item links are not associations
                        if (linkType.LinkEntityTypeId == null &&
                            (linkType.SourceEntityTypeId.Equals("ChannelNode")
                            || (BundleEntityTypes.Contains(linkType.SourceEntityTypeId) && !BundleEntityTypes.Contains(linkType.TargetEntityTypeId))
                            || (PackageEntityTypes.Contains(linkType.SourceEntityTypeId) && !PackageEntityTypes.Contains(linkType.TargetEntityTypeId))
                            || (DynamicPackageEntityTypes.Contains(linkType.SourceEntityTypeId) && !DynamicPackageEntityTypes.Contains(linkType.TargetEntityTypeId))
                            || (linkType.SourceEntityTypeId.Equals("Product") && linkType.TargetEntityTypeId.Equals("Item") && firstProdItemLink != null && linkType.Id == firstProdItemLink.Id)))
                        {
                            continue;
                        }

                        if (ExportEnabledEntityTypes.Any(eee => eee.Id.Equals(linkType.SourceEntityTypeId))
                            && ExportEnabledEntityTypes.Any(eee => eee.Id.Equals(linkType.TargetEntityTypeId)))
                        {
                            exportEnabledLinkTypes.Add(linkType);
                        }
                    }
                }

                return exportEnabledLinkTypes.ToArray();
            }
        }

        public bool ItemsToSkus
        {
            get
            {
                string value = settings["ITEM_TO_SKUS"];
                if (!bool.TryParse(value, out itemsToSkus))
                {
                    itemsToSkus = false;
                }

                return itemsToSkus;
            }
        }

        public int BatchSize
        {
            get
            {
                if (settings.ContainsKey("BATCH_SIZE"))
                {
                    string value = settings["BATCH_SIZE"];

                    if (!int.TryParse(value, out batchsize) || value == "0")
                    {
                        batchsize = int.MaxValue;
                    }

                    return batchsize;
                }

                return int.MaxValue;
            }
        }

        public Dictionary<int, string> EntityIdAndType { get; set; }

        public string[] BundleEntityTypes
        {
            get
            {
                if (!settings.ContainsKey("BUNDLE_ENTITYTYPES"))
                {
                    return new string[0];
                }

                return StringToStringArray(settings["BUNDLE_ENTITYTYPES"]);
            }
        }

        public string[] PackageEntityTypes
        {
            get
            {
                if (!settings.ContainsKey("PACKAGE_ENTITYTYPES"))
                {
                    return new string[0];
                }

                return StringToStringArray(settings["PACKAGE_ENTITYTYPES"]);
            }
        }

        public string[] DynamicPackageEntityTypes
        {
            get
            {
                if (!settings.ContainsKey("DYNAMIC_PACKAGE_ENTITYTYPES"))
                {
                    return new string[0];
                }

                return StringToStringArray(settings["DYNAMIC_PACKAGE_ENTITYTYPES"]);
            }
        }

        public HashSet<string> EPiFieldsIninRiver
        {
            get
            {
                if (excludedFields != null)
                {
                    return excludedFields;
                }

                if (!settings.ContainsKey("EXCLUDE_FIELDS") || string.IsNullOrEmpty(settings["EXCLUDE_FIELDS"]))
                {
                    HashSet<string> excludedFieldTypes = new HashSet<string>();
                    foreach (string baseField in epiFieldsIninRiver)
                    {
                        foreach (var entityType in ExportEnabledEntityTypes)
                        {
                            excludedFieldTypes.Add(entityType.Id.ToLower() + baseField);
                        }
                    }

                    excludedFieldTypes.Add("skus");

                    excludedFields = excludedFieldTypes;
                    return excludedFields;
                }
                else
                {
                    HashSet<string> excludedFieldTypes = new HashSet<string>();
                    foreach (string baseField in epiFieldsIninRiver)
                    {
                        foreach (var entityType in ExportEnabledEntityTypes)
                        {
                            excludedFieldTypes.Add(entityType.Id.ToLower() + baseField);
                        }
                    }

                    excludedFieldTypes.Add("skus");

                    string[] fields = settings["EXCLUDE_FIELDS"].Split(',');
                    foreach (string field in fields)
                    {
                        if (!excludedFieldTypes.Contains(field.ToLower()))
                        {
                            excludedFieldTypes.Add(field.ToLower());
                        }
                    }

                    excludedFields = excludedFieldTypes;
                    return excludedFields;
                }
            }
        }

        public PublicationMode ActivePublicationMode
        {
            get
            {
                return PublicationMode.Automatic;
            }
        }

        public CVLDataMode ActiveCVLDataMode
        {
            get
            {
                if (!settings.ContainsKey("CVL_DATA"))
                {
                    return CVLDataMode.Undefined;
                }

                return StringToCVLDataMode(settings["CVL_DATA"]);
            }
        }

        public string ResourceProviderType
        {
            get
            {
                if (!settings.ContainsKey("RESOURCE_PROVIDER_TYPE"))
                {
                    return "inRiver.EPiServerCommerce.MediaPublisher.Importer";
                }

                return settings["RESOURCE_PROVIDER_TYPE"];
            }
        }

        public Dictionary<string, string> Settings
        {
            get { return settings; }
        }

        private string[] StringToStringArray(string setting)
        {
            if (string.IsNullOrEmpty(setting))
            {
                return new string[0];
            }

            setting = setting.Replace(" ", string.Empty);
            if (setting.Contains(','))
            {
                return setting.Split(',');
            }

            return new[] { setting };
        }

        private CVLDataMode StringToCVLDataMode(string str)
        {
            CVLDataMode mode;

            if (!Enum.TryParse(str, out mode))
            {
                _context.Log(LogLevel.Error, string.Format("Could not parse CVLDataMode for string {0}", str));
            }

            return mode;
        }

        private Dictionary<string, string> ParseResourceConfig(string setting)
        {
            Dictionary<string, string> settingsDictionary = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(setting))
            {
                return settingsDictionary;
            }

            setting = setting.Replace(" ", string.Empty);

            string[] resouceConfs;
            if (setting.Contains(','))
            {
                resouceConfs = setting.Split(',');
            }
            else
            {
                resouceConfs = new[] { setting };
            }

            foreach (string resouceConf in resouceConfs)
            {
                if (resouceConf.Contains(':'))
                {
                    string[] parts = resouceConf.Split(':');

                    settingsDictionary.Add(parts[0], parts[1]);
                }
                else
                {
                    settingsDictionary.Add(resouceConf, string.Empty);
                }
            }

            return settingsDictionary;
        }

        public string ChannelListenerGUID
        {
            get
            {
                if (!settings.ContainsKey("CHANNEL_LISTENER_GUID"))
                {
                    return null;
                }

                return settings["CHANNEL_LISTENER_GUID"];
            }
        }

        public string StorageAccountName
        {
            get
            {
                if (!settings.ContainsKey("STORAGE_NAME"))
                {
                    return null;
                }
                return settings["STORAGE_NAME"];
            }
        }

        public string StorageAccountKey
        {
            get
            {
                if (!settings.ContainsKey("STORAGE_KEY"))
                {
                    return null;
                }
                return settings["STORAGE_KEY"];
            }
        }

        public string StorageAccountShareReference
        {
            get
            {
                if (!settings.ContainsKey("STORAGE_SHARE_REFERENCE"))
                {
                    return null;
                }
                return settings["STORAGE_SHARE_REFERENCE"];
            }
        }

        public string StorageAccountCatalogDirectoryReference
        {
            get
            {
                if (!settings.ContainsKey("STORAGE_CATALOG_DIRECTORY_REFERENCE"))
                {
                    return null;
                }
                return settings["STORAGE_CATALOG_DIRECTORY_REFERENCE"];
            }
        }

        public string StorageAccountResourcesDirectoryReference
        {
            get
            {
                if (!settings.ContainsKey("STORAGE_RESOURCES_DIRECTORY_REFERENCE"))
                {
                    return null;
                }
                return settings["STORAGE_RESOURCES_DIRECTORY_REFERENCE"];
            }
        }

        public bool EnableEPIEndPoint
        {
            get
            {
                if (enableEPIEndPoint == null)
                {
                    if (!settings.ContainsKey("ENABLE_EPI_ENDPOINT"))
                    {
                        enableEPIEndPoint = true;
                        return true;
                    }

                    string value = settings["ENABLE_EPI_ENDPOINT"];

                    if (!string.IsNullOrEmpty(value))
                    {
                        enableEPIEndPoint = bool.Parse(value);
                    }
                    else
                    {
                        enableEPIEndPoint = true;
                    }
                }
                return (bool)enableEPIEndPoint;
            }
        }

        public string ResourceNameInCloud { get; set; }

        public string CatalogPathInCloud { get; set; }

        public string[] ExcludedFieldCategories
        {
            get
            {
                if (settings.ContainsKey("EXCLUDED_FIELD_CATEGORIES") && !string.IsNullOrWhiteSpace(settings["EXCLUDED_FIELD_CATEGORIES"]))
                {
                    return settings["EXCLUDED_FIELD_CATEGORIES"].Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                }

                return new string[0];

            }
        }

        public int TotalResourceSizeLimitMb
        {
            get
            {
                if (settings.ContainsKey("TOTAL_RESOURCE_SIZE_LIMIT_MB") && !string.IsNullOrWhiteSpace(settings["TOTAL_RESOURCE_SIZE_LIMIT_MB"]))
                {
                    int sizeLimit;
                    if (int.TryParse(settings["TOTAL_RESOURCE_SIZE_LIMIT_MB"], out sizeLimit))
                    {
                        return sizeLimit;
                    }
                }

                // default limit
                return 1024;
            }
        }

    }
}
