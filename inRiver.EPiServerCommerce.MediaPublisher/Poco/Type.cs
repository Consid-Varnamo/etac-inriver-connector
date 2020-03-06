using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class Type
    {

        // ELEMENTS
        [XmlText]
        public string Value { get; set; }

        // CONSTRUCTOR
        public Type()
        { }
    }
}
