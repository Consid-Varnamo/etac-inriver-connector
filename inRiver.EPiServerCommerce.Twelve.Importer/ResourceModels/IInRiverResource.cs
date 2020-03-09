namespace inRiver.EPiServerCommernce.Twelve.Importer.ResourceModels
{
    using System.Collections.Generic;

    using EPiServer.Core;

    public interface IInRiverResource : IContentData
    {
        int ResourceFileId { get; set; }

        int EntityId { get; set; }

        string ResourceName { get; set; }

        IList<string> ResourceMarket { get; set; }

        string ResourceMimeType { get; set; }

        string ResourceMainCategory { get; set; }

        string ResourceSubCategory { get; set; }

        string ResourceDescription { get; set; }

        string ResourceYouTubeId { get; set; }
    }
}