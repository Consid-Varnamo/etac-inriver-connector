using inRiver.Connectors.EPiServer.Enums;

namespace inRiver.Connectors.EPiServer.Helpers
{
    public static class EnumHelpers
    {
        public static string GetAzureStorageDirectoryName(this Configuration config, XmlDocumentType documentType)
        {
            switch (documentType)
            {
                case XmlDocumentType.Catalog:
                    return config.StorageAccountCatalogDirectoryReference;
                default:
                    return config.StorageAccountResourcesDirectoryReference;
            }
        }

        public static string GetAzureFileName(this Configuration config, XmlDocumentType documentType)
        {
            switch (documentType)
            {
                case XmlDocumentType.Catalog:
                    return "catalog";
                default:
                    return "resources_";
            }
        }
    }
}