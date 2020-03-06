namespace inRiver.EPiServerCommerce.Interfaces
{
    using Enums;

    public class DeleteCompletedData
    {
        public string CatalogName { get; set; }

        public DeleteCompletedEventType EventType { get; set; }
    }
}