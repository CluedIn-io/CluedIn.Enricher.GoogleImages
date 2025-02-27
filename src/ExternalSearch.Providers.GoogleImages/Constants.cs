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

        public static AuthMethods AuthMethods { get; set; } = new AuthMethods
        {
            token = new List<Control>()
            {
                new Control()
                {
                    displayName = "Api Key",
                    type = "input",
                    isRequired = true,
                    name = KeyName.ApiToken
                },
                new Control()
                {
                    displayName = "Accepted Business Domain",
                    type = "input",
                    isRequired = false,
                    name = KeyName.AcceptedEntityType
                },
                new Control()
                {
                    displayName = "Image Search Text",
                    type = "input",
                    isRequired = false,
                    name = KeyName.ImageSearch
                }
            }
        };

        public static IEnumerable<Control> Properties { get; set; } = new List<Control>()
        {
            // NOTE: Leaving this commented as an example - BF
            //new()
            //{
            //    displayName = "Some Data",
            //    type = "input",
            //    isRequired = true,
            //    name = "someData"
            //}
        };

        public static Guide Guide { get; set; } = null;
        public static IntegrationType IntegrationType { get; set; } = IntegrationType.Enrichment;
    }
}