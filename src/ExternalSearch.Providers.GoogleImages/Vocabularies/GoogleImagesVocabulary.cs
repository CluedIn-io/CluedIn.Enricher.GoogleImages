using CluedIn.Core.Data;
using CluedIn.Core.Data.Vocabularies;

namespace CluedIn.ExternalSearch.Providers.GoogleImages.Vocabularies
{
    public class ImageDetailsVocabulary : SimpleVocabulary
    {
        public ImageDetailsVocabulary()
        {
            this.VocabularyName = "Google Images";
            this.KeyPrefix      = "googleImages";
            this.KeySeparator   = ".";
            this.Grouping       = EntityType.Organization;

            this.Logo = this.Add(new VocabularyKey("Logo"));
        }

        public VocabularyKey Logo { get; protected set; }
    }

    public static class GoogleImagesVocabulary
    {
        static GoogleImagesVocabulary()
        {
            Image = new ImageDetailsVocabulary();
        }

        public static ImageDetailsVocabulary Image { get; private set; }
    }
}
