using CluedIn.Core.Data;
using CluedIn.Core.Data.Vocabularies;

namespace CluedIn.ExternalSearch.Providers.GoogleImages.Vocabularies
{
    public static class GoogleImagesVocabulary
    {
        /// <summary>
        /// Initializes static members of the <see cref="KnowledgeGraphVocabulary" /> class.
        /// </summary>
        static GoogleImagesVocabulary()
        {
            Organization = new GoogleImagesDetailsVocabulary();

        }

        public static GoogleImagesDetailsVocabulary Organization { get; private set; }

    }
}