﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using inRiver.Remoting;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using inRiver.Remoting.Query;

namespace inRiver.Connectors.EPiServer
{
    public class OldHelper
    {
        private const long Buffersize = 4096;
        private readonly inRiverContext _context;

        public OldHelper(inRiverContext context)
        {
            _context = context;
        }

        internal List<Entity> GetPublishedChannels()
        {
            Criteria cat = new Criteria { FieldTypeId = "ChannelPublished", Operator = Operator.IsTrue };

            return _context.ExtensionManager.DataService.Search(cat, LoadLevel.Shallow);
        }

        internal XDocument GetChannelStructure(int channelId)
        {
            try
            {
                string channelStructure = _context.ExtensionManager.ChannelService.GetChannelStructure(channelId);
                if (!string.IsNullOrEmpty(channelStructure))
                {
                    return XDocument.Parse(channelStructure);
                }
            }
            catch (Exception ex)
            {
                _context.Log(LogLevel.Error, "Error in GetChannelStructure", ex);
            }

            return new XDocument();
        }

        internal XElement FindEntityElementInStructure(
            XDocument channelStructure,
            int sourceEntityId,
            int targetEntityId)
        {
            if (channelStructure.Root != null
                && channelStructure.Root.Name.LocalName.Split('_')[1]
                == sourceEntityId.ToString(CultureInfo.InvariantCulture))
            {
                return
                    channelStructure.Descendants()
                        .FirstOrDefault(
                            e =>
                            e.Name.LocalName.EndsWith("_" + targetEntityId) && e.Parent != null
                            && e.Parent.Name.LocalName.EndsWith("_" + sourceEntityId));
            }

            return
                channelStructure.Descendants()
                    .FirstOrDefault(
                        e =>
                        e.Name.LocalName.EndsWith("_" + targetEntityId) && e.Parent != null && e.Parent.Parent != null
                        && e.Parent.Parent.Name.LocalName.EndsWith("_" + sourceEntityId));
        }

        internal List<XElement> FindEntitiesElementInStructure(XDocument channelStructure, int sourceEntityId, int targetEntityId, string linktype)
        {
            List<XElement> elems = new List<XElement>();

            if (channelStructure.Root != null && channelStructure.Root.Name.LocalName.Split('_')[1] == sourceEntityId.ToString(CultureInfo.InvariantCulture))
            {
                elems.AddRange(channelStructure.Descendants().Where(e => e.Name.LocalName.EndsWith("_" + targetEntityId) && e.Parent != null && e.Parent.Name.LocalName.Contains("_" + sourceEntityId)).ToList());
            }

            List<XElement> parents = channelStructure.Descendants().Where(e => e.Name.LocalName.EndsWith("_" + sourceEntityId)).ToList();

            foreach (XElement parent in parents)
            {
                elems.AddRange(
                    parent.Descendants()
                        .Where(
                            e2 =>
                            e2.Parent != null && e2.Parent.Parent != null
                            && e2.Name.LocalName.EndsWith("_" + targetEntityId)
                            && e2.Parent.Name.LocalName.Contains("Link_" + linktype))
                        .ToList());
            }

            return elems;
        }

        internal XElement MergeXElemnts(XElement element1, XElement element2)
        {
            XElement element = ElementMerger(element1, element2);
            return element;
        }

        internal List<XElement> FindChannelAndChannelNodesForEntity(XDocument publishedStructure, int entityId)
        {
            List<XElement> channelNodeList = new List<XElement>();
            IEnumerable<XElement> elementList = publishedStructure.Descendants().Where(n => n.Name.LocalName.EndsWith("_" + entityId));
            foreach (XElement entityElement in elementList)
            {
                XElement channelNode = entityElement;
                if (entityElement.Parent != null)
                {
                    channelNode = entityElement.Parent;
                }

                while (!channelNode.Name.LocalName.Contains("ChannelNode_") && channelNode.Parent != null)
                {
                    channelNode = channelNode.Parent;
                }

                if ((channelNode.Name.LocalName.Contains("ChannelNode_") || channelNode.Name.LocalName.Contains("Channel_")) && !channelNodeList.Exists(c => c.Name.LocalName == channelNode.Name.LocalName))
                {
                    XElement copy = new XElement(channelNode.Name.LocalName);
                    foreach (XElement descendant in channelNode.Descendants())
                    {
                        if (descendant.Name.LocalName.StartsWith("Link_") && descendant.Elements().Any(d => d.Name.LocalName.Contains("ChannelNode_")))
                        {
                            continue;
                        }

                        if (descendant.Parent != null)
                        {
                            XElement element = copy.DescendantsAndSelf().FirstOrDefault(p => p.Name.LocalName == descendant.Parent.Name.LocalName);
                            if (element == null)
                            {
                                continue;
                            }

                            element.Add(new XElement(descendant.Name.LocalName));
                        }
                    }

                    channelNodeList.Add(copy);
                }
            }

            return channelNodeList;
        }

        internal XElement[] FindEntityElementsInStructure(XDocument publishedStructure, int entityId)
        {
            return (from e in publishedStructure.Descendants()
                    where
                        e.Name.LocalName.EndsWith("_" + entityId.ToString(CultureInfo.InvariantCulture)) && e.Parent != null
                        && e.Parent.Parent != null
                    select e.Parent.Parent).ToArray();
        }

        internal XElement FindEntityElementInStructure(XDocument publishedStructure, int entityId)
        {
            return
                publishedStructure.Descendants()
                    .FirstOrDefault(
                        e =>
                        e.Name.LocalName.EndsWith("_" + entityId.ToString(CultureInfo.InvariantCulture)) && e.Parent != null);
        }

        internal XElement FindEntityElementInStructure(XDocument structure, XElement element)
        {
            return
                structure.Descendants()
                    .FirstOrDefault(
                        e =>
                        element.Parent != null
                        && (element.Parent.Parent != null
                            && (e.Parent != null
                                && (e.Parent.Parent != null
                                    && (e.Name.Equals(element.Name)
                                        && e.Parent.Parent.Name.Equals(element.Parent.Parent.Name))))));
        }

        internal string GetDisplayNameFromEntity(Entity entity, Configuration configuration, int maxLength)
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
                if (string.IsNullOrEmpty(ls[configuration.LanguageMapping[configuration.ChannelDefaultLanguage]]))
                {
                    returnString = string.Format("[{0}]", entity.Id);
                }
                else
                {
                    returnString = ls[configuration.LanguageMapping[configuration.ChannelDefaultLanguage]];
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

        internal string GetDisplayDescriptionFromEntity(Entity entity, Configuration configuration)
        {
            Field displayDescriptionField = entity.DisplayDescription;

            if (displayDescriptionField == null || displayDescriptionField.IsEmpty())
            {
                return GetDisplayNameFromEntity(entity, configuration, -1);
            }

            if (displayDescriptionField.FieldType.DataType.Equals(DataType.LocaleString))
            {
                LocaleString ls = (LocaleString)displayDescriptionField.Data;
                if (string.IsNullOrEmpty(ls[configuration.LanguageMapping[configuration.ChannelDefaultLanguage]]))
                {
                    return GetDisplayNameFromEntity(entity, configuration, -1);
                }

                return ls[configuration.LanguageMapping[configuration.ChannelDefaultLanguage]];
            }

            return displayDescriptionField.Data.ToString();
        }

        internal string GetEPiMetaFieldNameFromField(FieldType fieldType, Configuration config)
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

        internal string GetChannelPrefix(Entity channel)
        {
            Field channelPrefixField =
                channel.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("channelprefix"));

            if (channelPrefixField == null || channelPrefixField.IsEmpty())
            {
                return string.Empty;
            }

            return channelPrefixField.Data.ToString();
        }

        internal Dictionary<string, string> GetChannelMimeTypeMappings(Entity channel)
        {
            Dictionary<string, string> channelMimeTypeMappings = new Dictionary<string, string>();
            Field channelMimeTypeField =
                channel.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("channelmimetypemappings"));
            if (channelMimeTypeField == null || channelMimeTypeField.IsEmpty())
            {
                return channelMimeTypeMappings;
            }

            string channelMapping = channelMimeTypeField.Data.ToString();

            if (!channelMapping.Contains(','))
            {
                return channelMimeTypeMappings;
            }

            string[] mappings = channelMapping.Split(';');

            foreach (string mapping in mappings)
            {
                if (!mapping.Contains(','))
                {
                    continue;
                }

                string[] map = mapping.Split(',');
                channelMimeTypeMappings.Add(map[0].Trim(), map[1].Trim());
            }

            return channelMimeTypeMappings;
        }

        internal CultureInfo GetChannelDefaultLanguage(Entity channel)
        {
            Field channelDefaultLanguageField =
                channel.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("channeldefaultlanguage"));

            if (channelDefaultLanguageField == null || channelDefaultLanguageField.IsEmpty())
            {
                return new CultureInfo("en-us");
            }

            return new CultureInfo(channelDefaultLanguageField.Data.ToString());
        }

        internal string GetChannelDefaultCurrency(Entity channel)
        {
            Field channelDefaultCurrencyField =
                channel.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("channeldefaultcurrency"));

            if (channelDefaultCurrencyField == null || channelDefaultCurrencyField.IsEmpty())
            {
                return "usd";
            }

            return channelDefaultCurrencyField.Data.ToString();
        }

        internal string GetChannelDefaultWeightBase(Entity channel)
        {
            Field channelDefaultWeightBaseField =
                channel.Fields.FirstOrDefault(f => f.FieldType.Id.ToLower().Contains("channeldefaultweightbase"));

            if (channelDefaultWeightBaseField == null || channelDefaultWeightBaseField.IsEmpty())
            {
                return "lbs";
            }

            return channelDefaultWeightBaseField.Data.ToString();
        }

        internal bool LinkTypeHasLinkEntity(string linkTypeId)
        {
            LinkType linktype = _context.ExtensionManager.ModelService.GetLinkType(linkTypeId);
            if (linktype.LinkEntityTypeId != null)
            {
                return true;
            }

            return false;
        }

        internal void CopyStream(FileStream inputStream, Stream outputStream)
        {
            long bufferSize = inputStream.Length < Buffersize ? inputStream.Length : Buffersize;
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }
        }

        internal Entity GetEntityFromElementName(string elementName)
        {
            if (string.IsNullOrEmpty(elementName) || !elementName.Contains('_'))
            {
                return null;
            }

            string idstring = elementName.Split('_')[1];
            int id;
            int.TryParse(idstring, out id);

            return _context.ExtensionManager.DataService.GetEntity(id, LoadLevel.DataOnly);
        }

        internal List<FieldType> GetFieldTypesWithCVL(string cvlId)
        {
            var configuration = new Configuration(_context);
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

        internal void RemoveExistingElements(XDocument doc, XDocument channelStructure)
        {
            try
            {
                if (doc.Root != null)
                {
                    XElement catalogElement = doc.Root.Element("Catalog");
                    XElement entriesElement;
                    if (catalogElement != null)
                    {
                        entriesElement = catalogElement.Element("Entries");
                        if (entriesElement != null)
                        {
                            foreach (XElement element in entriesElement.Elements())
                            {
                                XElement metaDataElement = element.Element("MetaData");
                                XElement codeElement = element.Element("Code");
                                if (metaDataElement == null || codeElement == null)
                                {
                                    continue;
                                }

                                XElement metaClassElement = metaDataElement.Element("MetaClass");
                                if (metaClassElement == null)
                                {
                                    continue;
                                }

                                XElement nameElement = metaClassElement.Element("Name");

                                if (nameElement == null)
                                {
                                    continue;
                                }

                                string name = nameElement.Value + "_" + codeElement.Value;
                                if (ElementAlreadyExistsInStructure(channelStructure, name))
                                {
                                    element.Remove();
                                }
                            }
                        }
                    }

                    if (catalogElement == null)
                    {
                        return;
                    }

                    entriesElement = catalogElement.Element("Entries");
                    if (entriesElement != null)
                    {
                        entriesElement.Attribute("totalCount").Value = entriesElement.Elements().Count().ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                _context.Log(LogLevel.Error, "Could not Remove existing entries", ex);
            }
        }

        internal bool ElementAlreadyExistsInStructure(XDocument channelStructure, string elementName)
        {
            return channelStructure.Descendants().Count(e => e.Name.LocalName.Equals(elementName)) > 1;
        }

        internal string GetParentNodeId(XElement structureElement)
        {
            XElement element = structureElement;
            do
            {
                element = element.Parent;
            }
            while (element != null && (element.Parent != null && element.Parent.Name.LocalName.IndexOf("ChannelNode_", StringComparison.Ordinal) != 0));

            if (element != null && element.Parent == null)
            {
                element = structureElement;
            }

            if (element != null && element.Parent != null)
            {
                return element.Parent.Name.LocalName.Split('_')[1];
            }

            return string.Empty;
        }

        internal List<string> GetResourceIds(XElement deletedElement, Configuration configuration)
        {
            List<string> foundResources = new List<string>();
            foreach (
                XElement resourceElement in
                    deletedElement.Descendants().Where(e => e.Name.LocalName.Contains("Resource_")))
            {
                foundResources.Add(configuration.ChannelIdPrefix + resourceElement.Name.LocalName.Split('_')[1]);
            }

            return foundResources;
        }

        internal string GetElapsedTimeFormated(Stopwatch stopwatch)
        {
            if (stopwatch.ElapsedMilliseconds < 1000)
            {
                return string.Format("{0} ms", stopwatch.ElapsedMilliseconds);
            }

            return stopwatch.Elapsed.ToString("hh\\:mm\\:ss");
        }

        internal List<XElement> GetDeletedElements(XDocument channelStructure, int parentEntityId, int entityId)
        {
            List<XElement> deletedElements;
            if (parentEntityId > 0)
            {
                deletedElements =
                    channelStructure.Descendants()
                        .Where(
                            e =>
                            e.Name.LocalName.EndsWith("_" + entityId) && e.Parent != null && e.Parent.Parent != null
                            && e.Parent.Parent.Name.LocalName.EndsWith("_" + parentEntityId)).ToList();

                if (!deletedElements.Any())
                {
                    deletedElements =
                        channelStructure.Descendants()
                            .Where(
                                e =>
                                e.Name.LocalName.EndsWith("_" + entityId) && e.Parent != null
                                && e.Parent.Name.LocalName.EndsWith("_" + parentEntityId)).ToList();
                }
            }
            else
            {
                deletedElements = channelStructure.Descendants().Where(e => e.Name.LocalName.EndsWith("_" + entityId)).ToList();
            }

            if (!deletedElements.Any())
            {
                return new List<XElement>();
            }

            if (deletedElements.First().Name.LocalName.Contains("ChannelNode_"))
            {
                return new List<XElement> { deletedElements.First() };
            }

            List<XElement> allDeletedElemets = new List<XElement>();
            allDeletedElemets.AddRange(deletedElements.ToList());
            allDeletedElemets.AddRange(deletedElements.Descendants().ToList());
            return allDeletedElemets;
        }

        internal List<CultureInfo> GetEpiCulturesForPimCulture(CultureInfo pimCi, Configuration configuration)
        {
            List<CultureInfo> cultureInfos = new List<CultureInfo>();

            foreach (KeyValuePair<CultureInfo, CultureInfo> pair in configuration.LanguageMapping)
            {
                if (pair.Value.Equals(pimCi))
                {
                    cultureInfos.Add(pair.Key);
                }
            }

            return cultureInfos;
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

        private XElement ElementMerger(XElement element1, XElement element2)
        {
            foreach (XElement elem in element2.Elements())
            {
                XElement exists = element1.Elements().FirstOrDefault(e => e.Name.LocalName == elem.Name.LocalName);
                if (exists == null)
                {
                    element1.Add(elem);
                }
                else
                {
                    ElementMerger(exists, elem);
                }
            }

            return element1;
        }
    }
}
