namespace inRiver.EPiServerCommerce.Interfaces
{
    using Enums;

    public class ImportUpdateCompletedData
    {
        public string CatalogName { get; set; }

        public ImportUpdateCompletedEventType EventType { get; set; }

        public bool ResourcesIncluded { get; set; }
    }
}