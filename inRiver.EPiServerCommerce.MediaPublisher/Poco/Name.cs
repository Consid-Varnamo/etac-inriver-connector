using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class Name
    {

        // ELEMENTS
        [XmlText]
        public string Value { get; set; }

        // CONSTRUCTOR
        public Name()
        { }
    }
}
