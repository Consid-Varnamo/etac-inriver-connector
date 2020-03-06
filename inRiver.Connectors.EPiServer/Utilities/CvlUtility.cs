using inRiver.Connectors.EPiServer.Communication;
using inRiver.Connectors.EPiServer.Enums;
using inRiver.Connectors.EPiServer.EpiXml;
using inRiver.Connectors.EPiServer.Helpers;
using inRiver.EPiServerCommerce.CommerceAdapter.Helpers;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace inRiver.Connectors.EPiServer.Utilities
{
    public class CvlUtility
    {
        private Configuration CvlUtilConfig { get; set; }
        private readonly inRiverContext _context;
        private readonly EpiMappingHelper _epiMappingHelper;
        private readonly EpiElement _epiElement;
        private readonly BusinessHelper _businessHelper;
        private readonly EpiDocument _epiDocument;
        private readonly ChannelHelper _channelHelper;
        private readonly EpiApi _epiApi;

        public CvlUtility(Configuration cvlUtilConfig, inRiverContext inRiverContext)
        {
            CvlUtilConfig = cvlUtilConfig;
            _context = inRiverContext;
            _epiElement = new EpiElement(inRiverContext);
            _epiMappingHelper = new EpiMappingHelper(inRiverContext);
            _businessHelper = new BusinessHelper(_context);
            _epiDocument = new EpiDocument(_context, cvlUtilConfig);
            _channelHelper = new ChannelHelper(_context);
            _epiApi = new EpiApi(_context);
        }

        public void AddCvl(string cvlId, string folderDateTime)
        {
            List<XElement> metafields = new List<XElement>();
            List<FieldType> affectedFieldTypes = _businessHelper.GetFieldTypesWithCVL(cvlId, CvlUtilConfig);

            foreach (FieldType fieldType in affectedFieldTypes)
            {
                if (_epiMappingHelper.SkipField(fieldType, CvlUtilConfig))
                {
                    continue;
                }

                XElement metaField = _epiElement.InRiverFieldTypeToMetaField(fieldType, CvlUtilConfig);

                if (fieldType.DataType.Equals(DataType.CVL))
                {
                    metaField.Add(_epiMappingHelper.GetDictionaryValues(fieldType, CvlUtilConfig));
                }

                if (metafields.Any(
                    mf =>
                    {
                        XElement nameElement = mf.Element("Name");
                        return nameElement != null && nameElement.Value.Equals(_epiMappingHelper.GetEPiMetaFieldNameFromField(fieldType, CvlUtilConfig));
                    }))
                {
                    XElement existingMetaField =
                        metafields.FirstOrDefault(
                            mf =>
                            {
                                XElement nameElement = mf.Element("Name");
                                return nameElement != null && nameElement.Value.Equals(_epiMappingHelper.GetEPiMetaFieldNameFromField(fieldType, CvlUtilConfig));
                            });

                    if (existingMetaField == null)
                    {
                        continue;
                    }

                    var movefields = metaField.Elements("OwnerMetaClass");
                    existingMetaField.Add(movefields);
                }
                else
                {
                    metafields.Add(metaField);
                }
            }

            XElement metaData = new XElement("MetaDataPlusBackup", new XAttribute("version", "1.0"), metafields.ToArray());
            XDocument doc = _epiDocument.CreateDocument(null, metaData, null, CvlUtilConfig);

            Entity channelEntity = _context.ExtensionManager.DataService.GetEntity(CvlUtilConfig.ChannelId, LoadLevel.DataOnly);
            if (channelEntity == null)
            {
                _context.Log(LogLevel.Error, string.Format("Could not find channel {0} for cvl add", CvlUtilConfig.ChannelId));
                return;
            }

            string channelIdentifier = _channelHelper.GetChannelIdentifier(channelEntity);

            if (!DocumentFileHelper.ZipDocumentAndUploadToAzure(XmlDocumentType.Catalog, doc, CvlUtilConfig, folderDateTime))
            {
                _context.Log(LogLevel.Information, "Failed to zip and upload the catalog file to azure from cvl utility AddCvl() method");
            }

            _context.Log(LogLevel.Debug, string.Format("catalog {0} saved", channelIdentifier));

            if (CvlUtilConfig.ActivePublicationMode.Equals(PublicationMode.Automatic))
            {
                _context.Log(LogLevel.Debug, "Starting automatic import!");

                _epiApi.StartImportIntoEpiServerCommerce(
                    Path.Combine(CvlUtilConfig.PublicationsRootPath, folderDateTime, Configuration.ExportFileName),
                    _channelHelper.GetChannelGuid(channelEntity, CvlUtilConfig), CvlUtilConfig);
            }
        }
    }
}
