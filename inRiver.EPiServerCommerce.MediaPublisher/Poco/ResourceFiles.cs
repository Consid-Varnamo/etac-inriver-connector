using System.Collections.Generic;
using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class ResourceFiles
    {

        // ELEMENTS
        [XmlElement("Resource")]
        public List<Resource> Resource { get; set; }

        // CONSTRUCTOR
        public ResourceFiles()
        { }
    }
}
