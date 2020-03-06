using System.Xml.Serialization;

namespace inRiver.EPiServerCommerce.MediaPublisher
{

    public class EntryCode
    {
        // ATTRIBUTES
        [XmlIgnore]
        public int SortOrder { get; set; }

        [XmlAttribute("SortOrder")]
        public string SortOrderString
        {
            get { return SortOrder.ToString(); }
            set
            {
                int outValue = default(int);

                if (int.TryParse(value, out outValue))
                {
                    SortOrder = outValue;
                }
            }
        }

        // ELEMENTS
        [XmlText]
        public string Value { get; set; }

        // CONSTRUCTOR
        public EntryCode()
        { }
    }
}
