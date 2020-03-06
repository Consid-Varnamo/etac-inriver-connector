using inRiver.EPiServerCommerce.Interfaces;
using inRiver.EPiServerCommernce.Twelve.Importer.ResourceModels;

namespace inRiver.EPiServerCommerce.Twelve.Importer.Services
{
    public interface IMedataDataService
    {
        /// <summary>
        /// Update the <see cref="IContentData"/> properties from the import resource model.
        /// Add your custom property here to update them from the import model.
        /// </summary>
        /// <typeparam name="T">Type of the content data object to update</typeparam>
        /// <param name="resource">The content data object</param>
        /// <param name="importResource">The import model containg the new values</param>
        void UpdateResourceProperties<T>(T resource, IInRiverImportResource importResource) where T : IInRiverResource;
    }
}
