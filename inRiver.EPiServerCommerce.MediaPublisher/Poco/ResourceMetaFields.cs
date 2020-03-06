using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class ResourceMetaFields
    {

        // ELEMENTS
        [XmlText]
        public string Value { get; set; }

        // CONSTRUCTOR
        public ResourceMetaFields()
        { }
    }
}
