using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;

namespace inRiver.Connectors.EPiServer.Utilities.Tests
{
    [TestClass()]
    public class XmlSplitUtilityTests
    {
        private XmlDocument resourceDocument;
        private int[] fileIds = new[] { 382, 385, 388, 391, 422, 425 };

        [TestMethod()]
        public void GetPartialResourcesXmlTest()
        {
            resourceDocument = new XmlDocument();

            resourceDocument.Load(@"c:\temp\resources_.20191122-182038.527\Resources.xml");

            XmlDocument partialDocument = XmlSplitUtility.GetPartialResourcesXml(resourceDocument, fileIds);

            partialDocument.Save(@"c:\temp\resources_.20191122-182038.527\Resources_partial.xml");
        }
    }
}