using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.ExternalSearch.Providers.GoogleImages
{
    public class GoogleImagesExternalSearchJobData : CrawlJobData
    {
        public GoogleImagesExternalSearchJobData(IDictionary<string, object> configuration)
        {
            ApiToken = GetValue<string>(configuration, Constants.KeyName.ApiToken);
            AcceptedEntityType = GetValue<string>(configuration, Constants.KeyName.AcceptedEntityType);
            OrgNameKey = GetValue<string>(configuration, Constants.KeyName.ImageSearch);

        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                { Constants.KeyName.ApiToken, ApiToken },
                { Constants.KeyName.AcceptedEntityType, AcceptedEntityType },
                { Constants.KeyName.ImageSearch, OrgNameKey }
            };
        }

        public string ApiToken { get; set; }
        public string AcceptedEntityType { get; set; }
        public string OrgNameKey { get; set; }

    }
}
