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

        public static readonly string Instruction = $$"""
                                      [
                                        {
                                          "type": "bulleted-list",
                                          "children": [
                                            {
                                              "type": "list-item",
                                              "children": [
                                                {
                                                  "text": "Add the {{EntityTypeLabel.ToLower()}} to specify the golden records you want to enrich. Only golden records belonging to that {{EntityTypeLabel.ToLower()}} will be enriched."
                                                }
                                              ]
                                            },
                                            {
                                              "type": "list-item",
                                              "children": [
                                                {
                                                  "text": "Add the vocabulary keys to provide the input for the enricher to search for additional information. For example, if you provide the website vocabulary key for the Web enricher, it will use specific websites to look for information about companies. In some cases, vocabulary keys are not required. If you don't add them, the enricher will use default vocabulary keys."
                                                }
                                              ]
                                            },
                                            {
                                              "type": "list-item",
                                              "children": [
                                                {
                                                  "text": "Add the API key to enable the enricher to retrieve information from a specific API. For example, the BvD enricher requires an access key to authenticate with the BvD API."
                                                }
                                              ]
                                            }
                                          ]
                                        }
                                      ]
                                      """;

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
                    Name = KeyName.ApiToken,
                    Help = "The key to authenticate access to the Google API.",
                },
                new()
                {
                    DisplayName = $"Accepted {EntityTypeLabel}",
                    Type = "entityTypeSelector",
                    IsRequired = true,
                    Name = KeyName.AcceptedEntityType,
                    Help = $"The {EntityTypeLabel.ToLower()} that defines the golden records you want to enrich (e.g., /Organization)."
                },
                new()
                {
                    DisplayName = "Image Search Text",
                    Type = "vocabularyKeySelector",
                    IsRequired = true,
                    Name = KeyName.ImageSearch,
                    Help = "The vocabulary key that contains the image search text you want to enrich (e.g., organization.name)."
                }
            }
        };

        public static IEnumerable<Control> Properties { get; set; } = new List<Control>();
        public static Guide Guide { get; set; } = new() { Instructions = Instruction };
        public static IntegrationType IntegrationType { get; set; } = IntegrationType.Enrichment;
    }
}