using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class Paths
    {

        // ELEMENTS
        [XmlElement("Path")]
        public Path Path { get; set; }

        // CONSTRUCTOR
        public Paths()
        { }
    }
}
