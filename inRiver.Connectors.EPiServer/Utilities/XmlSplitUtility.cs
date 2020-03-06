using System.Collections.Generic;
using System.Xml;

namespace inRiver.Connectors.EPiServer.Utilities
{
    public static class XmlSplitUtility
    {
        private const string resourceXmlTemplate = "<Resources><ResourceFiles /></Resources>";

        public static XmlDocument GetPartialResourcesXml(XmlDocument resourceXml, IEnumerable<int> fileIds)
        {
            // setup new document
            XmlDocument doc = new XmlDocument();

            // load xml template
            doc.LoadXml(resourceXmlTemplate);

            // setup resource files root node
            XmlNode resourceFilesRoot = doc.SelectSingleNode("/Resources/ResourceFiles");

            // import resource meta fields
            XmlNode metaFieldsNode = doc.ImportNode(resourceXml.SelectSingleNode("/Resources/ResourceMetaFields"), true);

            // insert metafields 
            doc.DocumentElement.InsertBefore(metaFieldsNode, doc.DocumentElement.FirstChild);

            foreach (int fileId in fileIds)
            {
                // get the first node found with matching id
                XmlNode resourceNode = resourceXml.SelectSingleNode($"/Resources/ResourceFiles/Resource[@id='{fileId}']");

                // make sure a matching node was found
                if (resourceNode != null)
                {
                    // insert resource element
                    resourceFilesRoot.AppendChild(doc.ImportNode(resourceNode, true));
                }
            }

            // return partial xml
            return doc;
        }

        public static XmlDocument GetPartialResourcesXml(XmlReader resourceXmlReader, IEnumerable<int> fileIds)
        {
            // create the xml document
            XmlDocument catalogXmlDocument = new XmlDocument();

            // load the xml document from the stream
            catalogXmlDocument.Load(resourceXmlReader);

            // return a IEnumerable of xml documents
            return GetPartialResourcesXml(catalogXmlDocument, fileIds);
        }
    }
}
