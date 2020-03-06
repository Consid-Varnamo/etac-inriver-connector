namespace inRiver.EPiServerCommernce.Twelve.Importer.ResourceModels
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using EPiServer.Commerce.SpecializedProperties;
    using EPiServer.DataAbstraction;
    using EPiServer.DataAnnotations;
    using EPiServer.Framework.DataAnnotations;

    /// <summary>
    /// This media type will be used if there is no more specific type
    /// available.
    /// </summary>
    [ContentType(GUID = "6A6BF35B-F76D-49FD-B4D0-BAF5DB36EB38")]
    [MediaDescriptor(ExtensionString = "jpg,jpeg,jpe,ico,gif,bmp,png")]
    public class InRiverGenericMedia : CommerceImage, IInRiverResource
    {
        [Display(
            Name = "File id",
            Description = "File id",
            GroupName = SystemTabNames.Settings,
            Order = 100)]
        public virtual int ResourceFileId { get; set; }

        [Display(
            Name = "Entity id",
            Description = "Entity id",
            GroupName = SystemTabNames.Settings,
            Order = 110)]
        public virtual int EntityId { get; set; }

        [Display(
            Name = "Markets",
            Description = "Markets",
            GroupName = SystemTabNames.Content,
            Order = 10)]
        public virtual IList<string> ResourceMarket { get; set; }

        [Display(
            Name = "Main category",
            Description = "Main category",
            GroupName = SystemTabNames.Content,
            Order = 20)]
        public virtual string ResourceMainCategory { get; set; }

        [Display(
            Name = "Sub category",
            Description = "Sub category",
            GroupName = SystemTabNames.Content,
            Order = 30)]
        public virtual string ResourceSubCategory { get; set; }

        [Display(
            Name = "Mime type",
            Description = "Mime type",
            GroupName = SystemTabNames.Content,
            Order = 40)]
        public virtual string ResourceMimeType { get; set; }

        [CultureSpecific]
        [Display(
            Name = "Description",
            Description = "Description",
            GroupName = SystemTabNames.Content,
            Order = 50)]
        public virtual string ResourceDescription { get; set; }

        [CultureSpecific]
        [Display(
            Name = "YouTube Video ID",
            Description = "YouTube Video ID",
            GroupName = SystemTabNames.Content,
            Order = 60)]
        public virtual string ResourceYouTubeId { get; set; }
    }
}