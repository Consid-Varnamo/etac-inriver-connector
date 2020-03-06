using System.Xml.Serialization;
using System.Collections.Generic;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class ResourceFields
    {

        // ELEMENTS
        [XmlElement("MetaField")]
        public List<MetaField> MetaField { get; set; }

        // CONSTRUCTOR
        public ResourceFields()
        { }
    }
}
