using System;
using System.Collections.Generic;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Providers;

namespace CluedIn.ExternalSearch.Providers.GoogleImages
{
    public static class Constants
    {
        public const string ComponentName = "GoogleImages";
        public const string ProviderName = "Google Images";
        public static readonly Guid ProviderId = Guid.Parse("fd6f802e-fe4f-42aa-9623-ba6369128e84");

        public struct KeyName
        {
            public const string ApiToken = "apiToken";
            public const string AcceptedEntityType = "acceptedEntityType";
            public const string ImageSearch = "imageSearchKey";

        }

        public static string About { get; set; } = "Google Images is a source of online images";
        public static string Icon { get; set; } = "Resources.google-icon-logo.svg";
        public static string Domain { get; set; } = "N/A";

        private static Version _cluedInVersion;
        public static Version CluedInVersion => _cluedInVersion ??= typeof(Core.Constants).Assembly.GetName().Version;
        public static string EntityTypeLabel => CluedInVersion < new Version(4, 5, 0) ? "Entity Type" : "Business Domain";
        public static AuthMethods AuthMethods { get; set; } = new()
        {
            Token = new List<Control>
            {
                new()
                {
                    DisplayName = "API Key",
                    Type = "password",
                    IsRequired = true,
                    Name = KeyName.ApiToken
                },
                new()
                {
                    DisplayName = $"Accepted {EntityTypeLabel}",
                    Type = "entityTypeSelector",
                    IsRequired = true,
                    Name = KeyName.AcceptedEntityType
                },
                new()
                {
                    DisplayName = "Image Search Text",
                    Type = "vocabularyKeySelector",
                    IsRequired = false,
                    Name = KeyName.ImageSearch
                }
            }
        };

        public static IEnumerable<Control> Properties { get; set; } = new List<Control>();

        public static Guide Guide { get; set; } = null;
        public static IntegrationType IntegrationType { get; set; } = IntegrationType.Enrichment;
    }
}