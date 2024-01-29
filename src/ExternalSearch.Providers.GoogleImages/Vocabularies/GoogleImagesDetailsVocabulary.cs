using CluedIn.Core.Data;
using CluedIn.Core.Data.Vocabularies;

namespace CluedIn.ExternalSearch.Providers.GoogleImages.Vocabularies
{
    public class GoogleImagesDetailsVocabulary : SimpleVocabulary
    {
        public GoogleImagesDetailsVocabulary()
        {
            this.VocabularyName = "GoogleImages Details";
            this.KeyPrefix = "googleImages.Image";
            this.KeySeparator = ".";
            this.Grouping = EntityType.Images;

            this.AddGroup("GoogleImages Image Details", group =>
            {
                this.AddressComponents = group.Add(new VocabularyKey("AddressComponents", VocabularyKeyDataType.Json, VocabularyKeyVisibility.Hidden));

           

            });

        }

        public VocabularyKey AddressComponents { get; set; }
     
    }

}