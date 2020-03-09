using System;
using System.Collections.Generic;
using System.Linq;
using EPiServer.Core;
using EPiServer.Logging;
using inRiver.EPiServerCommerce.Interfaces;
using inRiver.EPiServerCommernce.Twelve.Importer.ResourceModels;

namespace inRiver.EPiServerCommerce.Twelve.Importer.Services
{
    public class MetaDataService : IMedataDataService
    {
        private static readonly ILogger log = LogManager.GetLogger();
        public void UpdateResourceProperties<T>(T resource, IInRiverImportResource importResource) where T : IInRiverResource
        {
            if (resource == null)
            {
                throw new ArgumentException("Parameter can not be null", "resource");
            }

            if (importResource == null)
            {
                throw new ArgumentException("Parameter can not be null", "importResource");
            }

            List<ResourceMetaField> metaFields = importResource.MetaFields;

            log.Information("Updating properties on asset '{0}'", ((IContent)resource).Name);

            // map all resource properties from Etac's InRiver
            resource.ResourceName = metaFields.FirstOrDefault(mf => mf.Id == "ResourceName")?.Values?.FirstOrDefault()?.Data;
            log.Debug("ResourceName was set to '{0}'", resource.ResourceName);

            resource.ResourceMarket = metaFields.FirstOrDefault(mf => mf.Id == "ResourceMarket")?.Values?.Select(v => v.Data?.Split(';')).Where(a => a != null).SelectMany(s => s).ToList();
            log.Debug("ResourceMarket was set to '{0}'", string.Join(";", resource.ResourceMarket ?? new string[0]));

            resource.ResourceMainCategory = metaFields.FirstOrDefault(mf => mf.Id == "ResourceMainCategory")?.Values?.FirstOrDefault()?.Data;
            log.Debug("ResourceMainCategory was set to '{0}'", resource.ResourceMainCategory);

            resource.ResourceSubCategory = metaFields.FirstOrDefault(mf => mf.Id == "ResourceSubCategory")?.Values?.FirstOrDefault()?.Data;
            log.Debug("ResourceSubCategory was set to '{0}'", resource.ResourceSubCategory);

            resource.ResourceMimeType = metaFields.FirstOrDefault(mf => mf.Id == "ResourceMimeType")?.Values?.FirstOrDefault()?.Data;
            log.Debug("ResourceMimeType was set to '{0}'", resource.ResourceMimeType);

            resource.ResourceDescription = metaFields.FirstOrDefault(mf => mf.Id == "ResourceDescription")?.Values?.FirstOrDefault()?.Data;
            log.Debug("ResourceDescription was set to '{0}'", resource.ResourceDescription);

            resource.ResourceYouTubeId = metaFields.FirstOrDefault(mf => mf.Id == "ResourceYouTubeId")?.Values?.FirstOrDefault()?.Data;
            log.Debug("ResourceYouTubeId was set to '{0}'", resource.ResourceYouTubeId);
        }
    }
}
