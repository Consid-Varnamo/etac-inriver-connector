// ReSharper disable All

using inRiver.Connectors.EPiServer;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;

namespace inRiver.EPiServerCommerce.CommerceAdapter.Helpers
{
    using inRiver.Connectors.EPiServer.Enums;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;

    public class BusinessHelper
    {
        private static List<CVLValue> cvlValues;

        private static List<CVL> cvls;

        private readonly inRiverContext _context;

        public BusinessHelper(inRiverContext context)
        {
            _context = context;
        }

        public List<CVLValue> CVLValues
        {
            get
            {
                return cvlValues ?? (cvlValues = _context.ExtensionManager.ModelService.GetAllCVLValues());
            }

            set
            {
                cvlValues = value;
            }
        }

        public List<CVL> CvLs
        {
            get
            {
                return cvls ?? (cvls = _context.ExtensionManager.ModelService.GetAllCVLs());
            }

            set
            {
                cvls = value;
            }
        }

        public string GetElapsedTimeFormated(Stopwatch stopwatch)
        {
            if (stopwatch.ElapsedMilliseconds < 1000)
            {
                return string.Format("{0} ms", stopwatch.ElapsedMilliseconds);
            }

            return stopwatch.Elapsed.ToString("hh\\:mm\\:ss");
        }

        public bool FieldTypeIsMultiLanguage(FieldType fieldType, Configuration config)
        {
            if (fieldType.DataType.Equals(DataType.LocaleString))
            {
                return true;
            }

            // if we send only keys cvl should not be multiLang in Epi
            if (fieldType.DataType.Equals(DataType.CVL))
            {
                if (config.ActiveCVLDataMode.Equals(CVLDataMode.Keys))
                {
                    return false;
                }

                CVL cvl = _context.ExtensionManager.ModelService.GetCVL(fieldType.CVLId);

                if (cvl == null)
                {
                    return false;
                }

                return cvl.DataType.Equals(DataType.LocaleString);
            }

            return false;
        }

        public string GetAllowsSearch(FieldType fieldType)
        {
            if (fieldType.Settings.ContainsKey("AllowsSearch"))
            {
                return fieldType.Settings["AllowsSearch"];
            }

            return "True";
        }

        public bool GetAllowsNulls(FieldType fieldType, Configuration config)
        {
            bool result = false;

            if (config.MappingDocument != null)
            {
                XElement fieldElement = config.MappingDocument.Descendants().FirstOrDefault(e => e.Name.LocalName == fieldType.Id);
                if (fieldElement != null)
                {
                    XAttribute allowNullsAttribute = fieldElement.Attribute("AllowNulls");
                    if (allowNullsAttribute != null)
                    {
                        if (!bool.TryParse(allowNullsAttribute.Value, out result))
                        {
                            return false;
                        }

                        return result;
                    }
                }
            }
            else
            {
                result = !fieldType.Mandatory;
            }

            return result;
        }

        public string GetDisplayTemplateEntity(Entity entity)
        {
            Field displayTemplateField =
                entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("displaytemplate"));

            if (displayTemplateField == null || displayTemplateField.IsEmpty())
            {
                return null;
            }

            return displayTemplateField.Data.ToString();
        }

        public IEnumerable<string> CultureInfosToStringArray(CultureInfo[] cultureInfo)
        {
            return cultureInfo.Select(ci => ci.Name.ToLower()).ToArray();
        }

        public string GetStartDateFromEntity(Entity entity)
        {
            Field startDateField = entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("startdate"));

            if (startDateField == null || startDateField.IsEmpty())
            {
                return DateTime.UtcNow.ToString("u");
            }

            return ((DateTime)startDateField.Data).ToUniversalTime().ToString("u");
        }

        public string GetEndDateFromEntity(Entity entity)
        {
            Field endDateField = entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("enddate"));

            if (endDateField == null || endDateField.IsEmpty())
            {
                return DateTime.UtcNow.AddYears(100).ToString("u");
            }

            return ((DateTime)endDateField.Data).ToUniversalTime().ToString("u");
        }

        public string FieldIsUseInCompare(FieldType fieldType)
        {
            string value = "False";

            if (fieldType.Settings.ContainsKey("UseInComparing"))
            {
                value = fieldType.Settings["UseInComparing"];
                if (!(value.ToLower().Equals("false") || value.ToLower().Equals("true")))
                {
                    _context.Log(LogLevel.Error, string.Format("Fieldtype with id {0} has invalid UseInComparing setting", fieldType.Id));
                }
            }

            return value;
        }

        public XElement GetAttributeElements(FieldType fieldType, Configuration config, string overrideUseInComparing = null)
        {
            XElement attributesElement = new XElement("Attributes");

            string useInComparingValue = string.IsNullOrWhiteSpace(overrideUseInComparing) ? FieldIsUseInCompare(fieldType) : overrideUseInComparing;

            attributesElement.Add(
                new XElement("Attribute",
                    new XElement("Key", "useincomparing"),
                    new XElement("Value", useInComparingValue)));

            
            foreach (string settingName in config.EpiMetaFieldAttributes)
            {
                if (fieldType.Settings.ContainsKey(settingName))
                {
                    attributesElement.Add(
                        new XElement("Attribute",
                            new XElement("Key", settingName),
                            new XElement("Value", fieldType.Settings[settingName])));
                }
            }

            return attributesElement;
        }

        public string GetDisplayNameFromEntity(Entity entity, Configuration config, int maxLength)
        {
            Field displayNameField = entity.DisplayName;

            string returnString;

            if (displayNameField == null || displayNameField.IsEmpty())
            {
                returnString = string.Format("[{0}]", entity.Id);
            }
            else if (displayNameField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                LocaleString ls = (LocaleString)displayNameField.Data;
                if (string.IsNullOrEmpty(ls[config.LanguageMapping[config.ChannelDefaultLanguage]]))
                {
                    returnString = string.Format("[{0}]", entity.Id);
                }
                else
                {
                    returnString = ls[config.LanguageMapping[config.ChannelDefaultLanguage]];
                }
            }
            else
            {
                returnString = displayNameField.Data.ToString();
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

        public string GetSeoUriFromEntity(Entity entity, CultureInfo ci, Configuration configuration)
        {
            Field seoUriField = entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("seouri"));

            if (seoUriField == null || seoUriField.IsEmpty())
            {
                return string.Empty;
            }

            if (seoUriField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                return configuration.ChannelIdPrefix + ((LocaleString)seoUriField.Data)[ci];
            }

            return configuration.ChannelIdPrefix + seoUriField.Data;
        }

        public string GetSeoUriSegmentFromEntity(Entity entity, CultureInfo ci, Configuration config)
        {
            Field seoUriSegmentField = entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("seourisegment"));

            if (seoUriSegmentField == null || seoUriSegmentField.IsEmpty())
            {
                return string.Empty;
            }

            if (seoUriSegmentField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                return config.ChannelIdPrefix + ((LocaleString)seoUriSegmentField.Data)[ci];
            }

            return config.ChannelIdPrefix + seoUriSegmentField.Data;
        }

        public string GetSeoTitleFromEntity(Entity entity, CultureInfo ci)
        {
            Field seoTitleField = entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("seotitle"));

            if (seoTitleField == null || seoTitleField.IsEmpty())
            {
                return string.Empty;
            }

            if (seoTitleField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                return ((LocaleString)seoTitleField.Data)[ci];
            }

            return seoTitleField.Data.ToString();
        }

        public string GetSeoDescriptionFromEntity(Entity entity, CultureInfo ci)
        {
            Field seoDescriptionField =
                entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("seodescription"));

            if (seoDescriptionField == null || seoDescriptionField.IsEmpty())
            {
                return string.Empty;
            }

            if (seoDescriptionField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                return ((LocaleString)seoDescriptionField.Data)[ci];
            }

            return seoDescriptionField.Data.ToString();
        }

        public string GetSeoKeywordsFromEntity(Entity entity, CultureInfo ci)
        {
            Field seoKeywordsField = entity.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("seokeywords"));

            if (seoKeywordsField == null || seoKeywordsField.IsEmpty())
            {
                return string.Empty;
            }

            if (seoKeywordsField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                return ((LocaleString)seoKeywordsField.Data)[ci];
            }

            return seoKeywordsField.Data.ToString();
        }

        public string GetCVLValue(string cvlId, object key)
        {
            if (key == null)
            {
                return string.Empty;
            }

            CVLValue cv = CVLValues.FirstOrDefault(cvl => cvl.CVLId.Equals(cvlId) && cvl.Key.Equals(key));
            if (cv == null)
            {
                return string.Empty;
            }

            return cv.Value.ToString();
        }

        public List<XElement> GetCVLValues(Field field, Configuration configuration)
        {
            List<XElement> elemets = new List<XElement>();
            if (field == null || field.IsEmpty())
            {
                if (field != null)
                {
                    CVL cvl = CvLs.FirstOrDefault(c => c.Id.Equals(field.FieldType.CVLId));
                    if (cvl == null)
                    {
                        return elemets;
                    }

                    if (cvl.DataType == DataType.LocaleString)
                    {
                        Dictionary<string, XElement> valuesPerLanguage = new Dictionary<string, XElement>
                                                                     {
                                                                         {
                                                                             configuration.ChannelDefaultLanguage.Name,
                                                                             new XElement("Data", new XAttribute("language", configuration.ChannelDefaultLanguage.Name.ToLower()))
                                                                         }
                                                                     };
                        if (!configuration.ActiveCVLDataMode.Equals(CVLDataMode.Keys))
                        {
                            foreach (KeyValuePair<CultureInfo, CultureInfo> keyValuePair in configuration.LanguageMapping)
                            {
                                if (!valuesPerLanguage.ContainsKey(keyValuePair.Key.Name))
                                {
                                    valuesPerLanguage.Add(
                                        keyValuePair.Key.Name,
                                        new XElement("Data", new XAttribute("language", keyValuePair.Key.Name.ToLower())));
                                }
                            }

                            if (valuesPerLanguage.Count > 0)
                            {
                                foreach (string key in valuesPerLanguage.Keys)
                                {
                                    elemets.Add(valuesPerLanguage[key]);
                                }
                            }
                        }
                    }
                    else
                    {
                        XElement dataElement = new XElement(
                            "Data",
                            new XAttribute("language", configuration.ChannelDefaultLanguage.Name.ToLower()));

                        elemets.Add(dataElement);
                    }
                }

                return elemets;
            }

            if (configuration.ActiveCVLDataMode.Equals(CVLDataMode.Keys) ||
                (field.FieldType.Settings.ContainsKey("EPiMetaFieldName") && field.FieldType.Settings["EPiMetaFieldName"].Equals("_ExcludedCatalogEntryMarkets")))
            {
                XElement dataElement = new XElement(
                    "Data",
                    new XAttribute("language", configuration.ChannelDefaultLanguage.Name.ToLower()));
                if (field.FieldType.Multivalue)
                {
                    foreach (string cvlKey in field.Data.ToString().Split(';'))
                    {
                        dataElement.Add(new XElement("Item", new XAttribute("value", cvlKey)));
                    }
                }
                else
                {
                    dataElement.Add(new XAttribute("value", field.Data));
                }

                elemets.Add(dataElement);
            }
            else
            {
                CVL cvl = CvLs.FirstOrDefault(c => c.Id.Equals(field.FieldType.CVLId));
                if (cvl == null)
                {
                    return elemets;
                }

                string[] keys = field.FieldType.Multivalue
                                    ? field.Data.ToString().Split(';')
                                    : new[] { field.Data.ToString() };

                Dictionary<string, XElement> valuesPerLanguage = new Dictionary<string, XElement>
                                                                     {
                                                                         {
                                                                             configuration.ChannelDefaultLanguage.Name,
                                                                             new XElement("Data", new XAttribute("language", configuration.ChannelDefaultLanguage.Name.ToLower()))
                                                                         }
                                                                     };

                foreach (string key in keys)
                {
                    CVLValue cvlValue = CVLValues.FirstOrDefault(cv => cv.CVLId.Equals(cvl.Id) && cv.Key.Equals(key));
                    if (cvlValue == null || cvlValue.Value == null)
                    {
                        continue;
                    }

                    if (cvl.DataType.Equals(DataType.LocaleString))
                    {
                        LocaleString ls = (LocaleString)cvlValue.Value;
                        foreach (KeyValuePair<CultureInfo, CultureInfo> keyValuePair in configuration.LanguageMapping)
                        {
                            if (!valuesPerLanguage.ContainsKey(keyValuePair.Key.Name))
                            {
                                valuesPerLanguage.Add(
                                    keyValuePair.Key.Name,
                                    new XElement("Data", new XAttribute("language", keyValuePair.Key.Name.ToLower())));
                            }

                            string value = ls[keyValuePair.Value];
                            if (configuration.ActiveCVLDataMode.Equals(CVLDataMode.KeysAndValues))
                            {
                                value = key + Configuration.CVLKeyDelimiter + value;
                            }

                            // MultiValue uses <Item> elements, SingleValue stores the value in the <Data> element.
                            if (field.FieldType.Multivalue)
                            {
                                valuesPerLanguage[keyValuePair.Key.Name].Add(
                                    new XElement("Item", new XAttribute("value", value)));
                            }
                            else
                            {
                                valuesPerLanguage[keyValuePair.Key.Name].Add(new XAttribute("value", value));
                            }
                        }
                    }
                    else
                    {
                        string value = cvlValue.Value.ToString();
                        if (configuration.ActiveCVLDataMode.Equals(CVLDataMode.KeysAndValues))
                        {
                            value = key + Configuration.CVLKeyDelimiter + value;
                        }

                        // MultiValue uses <Item> elements, SingleValue stores the value in the <Data> element.
                        if (field.FieldType.Multivalue)
                        {
                            valuesPerLanguage[configuration.ChannelDefaultLanguage.Name].Add(
                                new XElement("Item", new XAttribute("value", value)));
                        }
                        else
                        {
                            valuesPerLanguage[configuration.ChannelDefaultLanguage.Name].Add(new XAttribute("value", value));
                        }
                    }
                }

                if (valuesPerLanguage.Count > 0)
                {
                    foreach (string key in valuesPerLanguage.Keys)
                    {
                        elemets.Add(valuesPerLanguage[key]);
                    }
                }
            }

            return elemets;
        }

        public string GetFieldDataAsString(Field field, Configuration configuration)
        {
            string value = string.Empty;

            if (field.IsEmpty())
            {
                return value;
            }

            if (field.FieldType.DataType.Equals(DataType.Boolean))
            {
                value = ((bool)field.Data).ToString();
            }
            else if (field.FieldType.DataType.Equals(DataType.CVL))
            {
                // This should never happen. CVL should be handled in the method which calls this method.
                value = GetCVLValue(field.FieldType.CVLId, field.Data);
            }
            else if (field.FieldType.DataType.Equals(DataType.DateTime))
            {
                value = ((DateTime)field.Data).ToString(Configuration.DateTimeFormatString);
            }
            else if (field.FieldType.DataType.Equals(DataType.Double))
            {
                value = ((double)field.Data).ToString(CultureInfo.InvariantCulture);
            }
            else if (field.FieldType.DataType.Equals(DataType.File))
            {
                value = field.Data.ToString();
            }
            else if (field.FieldType.DataType.Equals(DataType.Integer))
            {
                value = field.Data.ToString();
            }
            else if (field.FieldType.DataType.Equals(DataType.LocaleString))
            {
                LocaleString ls = (LocaleString)field.Data;

                foreach (KeyValuePair<CultureInfo, CultureInfo> cultureInfoPair in configuration.LanguageMapping)
                {
                    value += cultureInfoPair.Key.Name + "||" + ls[cultureInfoPair.Value] + "|;";
                }
            }
            else if (field.FieldType.DataType.Equals(DataType.String))
            {
                value = field.Data.ToString();
            }
            else if (field.FieldType.DataType.Equals(DataType.Xml))
            {
                value = field.Data.ToString();
            }

            return value;
        }

        internal void CompareAndParseSkuXmls(string oldXml, string newXml, out List<XElement> skusToAdd, out List<XElement> skusToDelete)
        {
            XDocument oldDoc = XDocument.Parse(oldXml);
            XDocument newDoc = XDocument.Parse(newXml);

            List<XElement> oldSkus = oldDoc.Descendants().Elements("SKU").ToList();
            List<XElement> newSkus = newDoc.Descendants().Elements("SKU").ToList();

            List<string> removables = new List<string>();

            foreach (XElement elem in oldSkus)
            {
                XAttribute id = elem.Attribute("id");
                if (newSkus.Exists(e => e.Attribute("id").Value == id.Value))
                {
                    if (!removables.Exists(y => y == id.Value))
                    {
                        removables.Add(id.Value);
                    }
                }
            }

            foreach (string id in removables)
            {
                oldSkus.RemoveAll(e => e.Attribute("id").Value == id);
                newSkus.RemoveAll(e => e.Attribute("id").Value == id);
            }

            skusToAdd = newSkus;
            skusToDelete = oldSkus;
        }

        internal List<FieldType> GetFieldTypesWithCVL(string cvlId, Configuration configuration)
        {
            List<FieldType> hasCVL = new List<FieldType>();
            foreach (EntityType entityType in configuration.ExportEnabledEntityTypes)
            {
                foreach (FieldType fieldType in entityType.FieldTypes)
                {
                    if (!string.IsNullOrEmpty(fieldType.CVLId) && fieldType.CVLId.Equals(cvlId))
                    {
                        hasCVL.Add(fieldType);
                    }
                }
            }

            return hasCVL;
        }
    }
}
