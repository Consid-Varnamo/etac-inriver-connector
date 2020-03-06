using inRiver.Connectors.EPiServer.Enums;
using inRiver.EPiServerCommerce.CommerceAdapter.Helpers;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Objects;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace inRiver.Connectors.EPiServer.Helpers
{
    public class EpiMappingHelper
    {
        private static int firstProductItemLinkType = -2;

        private readonly inRiverContext _context;
        public readonly BusinessHelper _businessHelper;

        public EpiMappingHelper(inRiverContext inRiverContext)
        {
            _context = inRiverContext;
            _businessHelper = new BusinessHelper(inRiverContext);
        }

        public int FirstProductItemLinkType
        {
            get
            {
                if (firstProductItemLinkType < -1)
                {
                    List<LinkType> linkTypes = _context.ExtensionManager.ModelService.GetLinkTypesForEntityType("Product");
                    LinkType first =
                        linkTypes.Where(lt => lt.TargetEntityTypeId.Equals("Item"))
                            .OrderBy(lt => lt.Index)
                            .FirstOrDefault();

                    firstProductItemLinkType = first != null ? first.Index : -1;
                }

                return firstProductItemLinkType;
            }
        }

        public string GetParentClassForEntityType(string entityTypeName)
        {
            if (entityTypeName.ToLower().Contains("channelnode"))
            {
                return "CatalogNode";
            }

            return "CatalogEntry";
        }

        public bool IsRelation(Link link, Configuration config)
        {
            if ((config.BundleEntityTypes.Contains(link.LinkType.SourceEntityTypeId) && !config.BundleEntityTypes.Contains(link.LinkType.TargetEntityTypeId))
                            || (config.PackageEntityTypes.Contains(link.LinkType.SourceEntityTypeId) && !config.PackageEntityTypes.Contains(link.LinkType.TargetEntityTypeId))
                            || (config.DynamicPackageEntityTypes.Contains(link.LinkType.SourceEntityTypeId) && !config.DynamicPackageEntityTypes.Contains(link.LinkType.TargetEntityTypeId)))
            {
                return true;
            }

            return link.LinkType.SourceEntityTypeId.Equals("Product") && link.LinkType.TargetEntityTypeId.Equals("Item")
                   && link.LinkType.Index == FirstProductItemLinkType;
        }

        public bool IsRelation(string sourceEntityTypeId, string targetEntityTypeId, int sortOrder, Configuration config)
        {
            if ((config.BundleEntityTypes.Contains(sourceEntityTypeId) && !config.BundleEntityTypes.Contains(targetEntityTypeId))
                            || (config.PackageEntityTypes.Contains(sourceEntityTypeId) && !config.PackageEntityTypes.Contains(targetEntityTypeId))
                            || (config.DynamicPackageEntityTypes.Contains(sourceEntityTypeId) && !config.DynamicPackageEntityTypes.Contains(targetEntityTypeId)))
            {
                return true;
            }

            return sourceEntityTypeId.Equals("Product") && targetEntityTypeId.Equals("Item")
                   && sortOrder == FirstProductItemLinkType;
        }

        public bool IsRelation(string linkTypeId, Configuration config)
        {
            LinkType linktype = config.LinkTypes.Find(lt => lt.Id == linkTypeId);

            if ((config.BundleEntityTypes.Contains(linktype.SourceEntityTypeId) && !config.BundleEntityTypes.Contains(linktype.TargetEntityTypeId))
                 || (config.PackageEntityTypes.Contains(linktype.SourceEntityTypeId) && !config.PackageEntityTypes.Contains(linktype.TargetEntityTypeId))
                 || (config.DynamicPackageEntityTypes.Contains(linktype.SourceEntityTypeId) && !config.DynamicPackageEntityTypes.Contains(linktype.TargetEntityTypeId)))
            {
                return true;
            }

            return linktype.SourceEntityTypeId.Equals("Product") && linktype.TargetEntityTypeId.Equals("Item")
                   && linktype.Index == FirstProductItemLinkType;
        }

        public string GetAssociationName(Link link, Configuration config)
        {
            if (link.LinkEntity != null)
            {
                // Use the Link name + the display name to create a unique ASSOCIATION NAME in EPi Commerce
                return link.LinkType.LinkEntityTypeId + '_'
                       + _businessHelper.GetDisplayNameFromEntity(link.LinkEntity, config, -1).Replace(' ', '_');
            }

            return link.LinkType.Id;
        }

        public string GetAssociationName(StructureEntity structureEntity, Entity linkEntity, Configuration config)
        {
            if (structureEntity.LinkEntityId != null)
            {
                // Use the Link name + the display name to create a unique ASSOCIATION NAME in EPi Commerce
                return linkEntity.EntityType.Id + '_'
                       + _businessHelper.GetDisplayNameFromEntity(linkEntity, config, -1).Replace(' ', '_');
            }

            return structureEntity.LinkTypeIdFromParent;
        }

        public string GetTableNameForEntityType(string entityTypeName, string name)
        {
            if (entityTypeName.ToLower().Contains("channelnode"))
            {
                return "CatalogNodeEx_" + name;
            }

            return "CatalogEntryEx_" + name;
        }

        public bool SkipField(FieldType fieldType, Configuration config)
        {
            return config.EPiFieldsIninRiver.Contains(fieldType.Id.ToLower()) || config.ExcludedFieldCategories.Any(categoryId => categoryId.Equals(fieldType.CategoryId, System.StringComparison.InvariantCultureIgnoreCase));
        }

        public int GetMetaFieldLength(FieldType fieldType, Configuration config)
        {
            int defaultLength = 150;

            if (config.MappingDocument != null)
            {
                XElement fieldElement = config.MappingDocument.Descendants().FirstOrDefault(e => e.Name.LocalName == fieldType.Id);
                if (fieldElement != null)
                {
                    XAttribute allowNullsAttribute = fieldElement.Attribute("MetaFieldLength");
                    if (allowNullsAttribute != null)
                    {
                        if (!int.TryParse(allowNullsAttribute.Value, out defaultLength))
                        {
                            return 150;
                        }

                        return defaultLength;
                    }
                }
            }
            else
            {
                if (fieldType.Settings.ContainsKey("MetaFieldLength"))
                {
                    if (!int.TryParse(fieldType.Settings["MetaFieldLength"], out defaultLength))
                    {
                        return 150;
                    }
                }
            }

            if (fieldType.Settings.ContainsKey("AdvancedTextObject"))
            {
                if (fieldType.Settings["AdvancedTextObject"] == "1")
                {
                    return 65000;
                }
            }

            return defaultLength;
        }

        public string InRiverDataTypeToEpiType(FieldType fieldType, Configuration config)
        {
            string type = string.Empty;

            if (fieldType == null || string.IsNullOrEmpty(fieldType.DataType))
            {
                return type;
            }

            if (fieldType.DataType.Equals(DataType.Boolean))
            {
                type = "Boolean";
            }
            else if (fieldType.DataType.Equals(DataType.CVL))
            {
                type = fieldType.Multivalue ? "EnumMultiValue" : "EnumSingleValue";
            }
            else if (fieldType.DataType.Equals(DataType.DateTime))
            {
                type = "DateTime";
            }
            else if (fieldType.DataType.Equals(DataType.Double))
            {
                type = "Float";
            }
            else if (fieldType.DataType.Equals(DataType.File))
            {
                type = "Integer";
            }
            else if (fieldType.DataType.Equals(DataType.Integer))
            {
                type = "Integer";
            }
            else if (fieldType.DataType.Equals(DataType.LocaleString))
            {
                if (fieldType.Settings.ContainsKey("AdvancedTextObject"))
                {
                    if (fieldType.Settings["AdvancedTextObject"] == "1")
                    {
                        type = "LongHtmlString";
                    }
                    else
                    {
                        type = "LongString";
                    }
                }
                else if (config.MappingDocument != null)
                {
                    XElement fieldElement = config.MappingDocument.Descendants().FirstOrDefault(e => e.Name.LocalName == fieldType.Id);
                    if (fieldElement != null)
                    {
                        XAttribute attribute = fieldElement.Attribute("EPiDataType");
                        if (attribute != null)
                        {
                            type = attribute.Value;
                        }
                    }
                }
                else if (fieldType.Settings.ContainsKey("EPiDataType"))
                {
                    if (fieldType.Settings["EPiDataType"] == "ShortString")
                    {
                        type = "ShortString";
                    }
                    else
                    {
                        type = "LongString";
                    }
                }
                else
                {
                    type = "LongString";
                }
            }
            else if (fieldType.DataType.Equals(DataType.String))
            {
                if (config.MappingDocument != null)
                {
                    XElement fieldElement = config.MappingDocument.Descendants().FirstOrDefault(e => e.Name.LocalName == fieldType.Id);
                    if (fieldElement != null)
                    {
                        XAttribute attribute = fieldElement.Attribute("EPiDataType");
                        if (attribute != null)
                        {
                            type = attribute.Value;
                        }
                    }
                }
                else if (fieldType.Settings.ContainsKey("EPiDataType"))
                {
                    if (fieldType.Settings["EPiDataType"] == "ShortString")
                    {
                        type = "ShortString";
                    }
                    else
                    {
                        type = "LongString";
                    }
                }
                else
                {
                    type = "LongString";
                }
            }
            else if (fieldType.DataType.Equals(DataType.Xml))
            {
                if (fieldType.Settings.ContainsKey("EPiDataType"))
                {
                    if (fieldType.Settings["EPiDataType"] == "ShortString")
                    {
                        type = "ShortString";
                    }
                    else
                    {
                        type = "LongString";
                    }
                }
                else
                {
                    type = "LongString";
                }
            }

            return type;
        }

        public List<XElement> GetDictionaryValues(FieldType fieldType, Configuration configuration)
        {
            CVL currentCVL = _businessHelper.CvLs.ToList().FirstOrDefault(c => c.Id.Equals(fieldType.CVLId));
            if (currentCVL == null)
            {
                return null;
            }

            List<CVLValue> currentCvlValues = _businessHelper.CVLValues.ToList().Where(cv => cv.CVLId.Equals(fieldType.CVLId)).ToList();
            if (!currentCvlValues.Any())
            {
                return null;
            }

            List<XElement> values = new List<XElement>();
            foreach (CVLValue cvlValue in currentCvlValues)
            {
                if (configuration.ActiveCVLDataMode.Equals(CVLDataMode.Keys))
                {
                    if (!values.Any(d => d.Value.Equals(cvlValue.Key)))
                    {
                        values.Add(new XElement("Dictionary", cvlValue.Key));
                    }
                }
                else
                {
                    string value;
                    if (currentCVL.DataType.Equals(DataType.LocaleString))
                    {
                        foreach (string localeString in GetLocaleStringValues(cvlValue.Value, configuration))
                        {
                            value = localeString;
                            if (configuration.ActiveCVLDataMode.Equals(CVLDataMode.KeysAndValues))
                            {
                                value = cvlValue.Key + Configuration.CVLKeyDelimiter + value;
                            }

                            values.Add(new XElement("Dictionary", value));
                        }
                    }
                    else
                    {
                        value = cvlValue.Value.ToString();
                        if (configuration.ActiveCVLDataMode.Equals(CVLDataMode.KeysAndValues))
                        {
                            value = cvlValue.Key + Configuration.CVLKeyDelimiter + value;
                        }

                        if (!values.Any(d => d.Value.Equals(cvlValue.Value)))
                        {
                            values.Add(new XElement("Dictionary", value));
                        }
                    }
                }
            }

            return values;
        }

        public List<string> GetLocaleStringValues(object data, Configuration configuration)
        {
            List<string> localeStringValues = new List<string>();

            if (data == null)
            {
                return localeStringValues;
            }

            LocaleString ls = (LocaleString)data;

            foreach (KeyValuePair<CultureInfo, CultureInfo> keyValuePair in configuration.LanguageMapping)
            {
                if (!localeStringValues.Any(e => e.Equals(ls[keyValuePair.Value])))
                {
                    localeStringValues.Add(ls[keyValuePair.Value]);
                }
            }

            return localeStringValues;
        }

        public string GetNameForEntity(Entity entity, Configuration configuration, int maxLength)
        {
            Field nameField = null;
            if (configuration.EpiNameMapping.ContainsKey(entity.EntityType.Id))
            {
                nameField = entity.GetField(configuration.EpiNameMapping[entity.EntityType.Id]);
            }

            string returnString = string.Empty;
            if (nameField == null || nameField.IsEmpty())
            {
                returnString = _businessHelper.GetDisplayNameFromEntity(entity, configuration, maxLength);
            }
            else if (nameField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                LocaleString ls = (LocaleString)nameField.Data;
                if (!string.IsNullOrEmpty(ls[configuration.LanguageMapping[configuration.ChannelDefaultLanguage]]))
                {
                    returnString = ls[configuration.LanguageMapping[configuration.ChannelDefaultLanguage]];
                }
            }
            else
            {
                returnString = nameField.Data.ToString();
            }

            if (maxLength > 0)
            {
                int lenght = returnString.Length;
                if (lenght > maxLength)
                {
                    returnString = returnString.Substring(0, maxLength - 1);
                }
            }

            return returnString;
        }

        public Field GetDisplayNameField(Entity entity, Configuration configuration)
        {
            Field nameField = null;
            if (configuration.EpiNameMapping.ContainsKey(entity.EntityType.Id))
            {
                nameField = entity.GetField(configuration.EpiNameMapping[entity.EntityType.Id]);
            }

            if (nameField == null || nameField.IsEmpty())
            {
                nameField = entity.DisplayName;
            }

            return nameField;
        }

        public string GetEPiMetaFieldNameFromField(FieldType fieldType, Configuration config)
        {
            string name = fieldType.Id;

            if (config.MappingDocument != null)
            {
                XElement fieldElement = config.MappingDocument.Descendants().FirstOrDefault(e => e.Name.LocalName == fieldType.Id);
                if (fieldElement != null)
                {
                    XAttribute attribute = fieldElement.Attribute("EPiMetaFieldName");
                    if (attribute != null)
                    {
                        name = attribute.Value;

                        return name;
                    }
                }
            }
            else if (fieldType.Settings != null && fieldType.Settings.ContainsKey(Configuration.EPiCommonField)
                && !string.IsNullOrEmpty(fieldType.Settings[Configuration.EPiCommonField]))
            {
                name = fieldType.Settings[Configuration.EPiCommonField];
            }

            return name;
        }

        public string GetEntryType(string entityTypeId, Configuration configuration)
        {
            if (entityTypeId.Equals("Item"))
            {
                if (!(configuration.UseThreeLevelsInCommerce && configuration.ItemsToSkus))
                {
                    return "Variation";
                }
            }
            else if (configuration.BundleEntityTypes.Contains(entityTypeId))
            {
                return "Bundle";
            }
            else if (configuration.PackageEntityTypes.Contains(entityTypeId))
            {
                return "Package";
            }
            else if (configuration.DynamicPackageEntityTypes.Contains(entityTypeId))
            {
                return "DynamicPackage";
            }

            return "Product";
        }
    }
}