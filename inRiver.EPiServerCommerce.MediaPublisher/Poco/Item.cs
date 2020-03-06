using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class Item
    {
        // ATTRIBUTES
        [XmlAttribute("value")]
        public string value { get; set; }

        // CONSTRUCTOR
        public Item()
        { }
    }
}
