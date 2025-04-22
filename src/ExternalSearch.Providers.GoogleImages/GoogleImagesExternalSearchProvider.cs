using CluedIn.Core;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Data.Vocabularies;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.ExternalSearch;
using CluedIn.Core.Providers;
using CluedIn.Core.Connectors;
using CluedIn.ExternalSearch.Providers.GoogleImages.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

using EntityType = CluedIn.Core.Data.EntityType;

namespace CluedIn.ExternalSearch.Providers.GoogleImages
{
    /// <summary>The google image external search provider.</summary>
    /// <seealso cref="CluedIn.ExternalSearch.ExternalSearchProviderBase" />
    public class GoogleImagesExternalSearchProvider : ExternalSearchProviderBase, IExtendedEnricherMetadata, IConfigurableExternalSearchProvider, IExternalSearchProviderWithVerifyConnection
    {
        private static EntityType[] AcceptedEntityTypes = [EntityType.Organization];

        /**********************************************************************************************************
         * CONSTRUCTORS
         **********************************************************************************************************/

        public GoogleImagesExternalSearchProvider()
            : base(Constants.ProviderId, AcceptedEntityTypes)
        {
            var nameBasedTokenProvider = new NameBasedTokenProvider("GoogleImages");

            if (nameBasedTokenProvider.ApiToken != null)
                // ReSharper disable once VirtualMemberCallInConstructor
                this.TokenProvider = new RoundRobinTokenProvider(nameBasedTokenProvider.ApiToken.Split(',', ';'));
        }

        /**********************************************************************************************************
         * METHODS
         **********************************************************************************************************/

        /// <summary>Builds the queries.</summary>
        /// <param name="context">The context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The search queries.</returns>
        public override IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context, IExternalSearchRequest request)
        {
            return InternalBuildQueries(context, request);
        }

        // ReSharper disable once UnusedParameter.Local
        private IEnumerable<IExternalSearchQuery> InternalBuildQueries(ExecutionContext context, IExternalSearchRequest request, IDictionary<string, object> config = null)
        {
            if (config != null && config.TryGetValue(Constants.KeyName.AcceptedEntityType, out var customType) && !string.IsNullOrWhiteSpace(customType?.ToString()))
            {
                if (!request.EntityMetaData.EntityType.Is(customType.ToString()))
                {
                    yield break;
                }
            }
            else if (!this.Accepts(request.EntityMetaData.EntityType))
                yield break;

            var existingResults = request.GetQueryResults<ImageDetailsResponse>(this).ToList();

            var entityType = request.EntityMetaData.EntityType;

            var organizationName = GetValue(request, config, Constants.KeyName.ImageSearch, Vocabularies.CluedInOrganization.OrganizationName);

            if (organizationName is { Count: > 0 })
            {
                foreach (var nameValue in organizationName.Where(v => !NameFilter(v)))
                {
                    var companyDict = new Dictionary<string, string>
                                    {
                                        {"companyName", nameValue }
                                    };
                    yield return new ExternalSearchQuery(this, entityType, companyDict);
                }
            }

            yield break;

            bool NameFilter(string value)
            {
                return existingResults.Any(r => string.Equals(r.Data.queries.request.First().searchTerms, value, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        private static HashSet<string> GetValue(IExternalSearchRequest request, IDictionary<string, object> config, string keyName, VocabularyKey defaultKey)
        {
            HashSet<string> value;
            if (config.TryGetValue(keyName, out var customVocabKey) && !string.IsNullOrWhiteSpace(customVocabKey?.ToString()))
            {
                value = request.QueryParameters.GetValue<string, HashSet<string>>(customVocabKey.ToString(), new HashSet<string>());
            }
            else
            {
                value = request.QueryParameters.GetValue(defaultKey, new HashSet<string>());
            }

            return value;
        }

        /// <summary>Executes the search.</summary>
        /// <param name="context">The context.</param>
        /// <param name="query">The query.</param>
        /// <returns>The results.</returns>
        public override IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context, IExternalSearchQuery query)
        {
            var apiKey = this.TokenProvider.ApiToken;

            foreach (var externalSearchQueryResult in InternalExecuteSearch(query, apiKey)) yield return externalSearchQueryResult;
        }

        private static IEnumerable<IExternalSearchQueryResult> InternalExecuteSearch(IExternalSearchQuery query, string apiKey)
        {
            var client = new RestClient("https://www.googleapis.com/customsearch/v1");

            if (!query.QueryParameters.TryGetValue("companyName", out var parameter)) yield break;
            var name = parameter.FirstOrDefault();
            var request = new RestRequest($"?key={apiKey}&cx=000950167190857528722:vf0rypkbf0w&q={name}&searchType=image", Method.GET);
           
            var response = client.ExecuteAsync<ImageDetailsResponse>(request).Result;
               
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (response.Data == null) yield break;
                response.Data.items.RemoveAll(x => x.fileFormat == "image/"); // remove empty mimetype items
                yield return new ExternalSearchQueryResult<ImageDetailsResponse>(query, response.Data);
            }
            else switch (response.StatusCode)
            {
                case HttpStatusCode.NoContent or HttpStatusCode.NotFound:
                    yield break;
                default:
                {
                    if (response.ErrorException != null)
                        throw new AggregateException(response.ErrorException.Message, response.ErrorException);

                    throw new ApplicationException("Could not execute external search query - StatusCode:" + response.StatusCode + "; Content: " + response.Content);
                }
            }
        }

        /// <summary>Builds the clues.</summary>
        /// <param name="context">The context.</param>
        /// <param name="query">The query.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The clues.</returns>
        public override IEnumerable<Clue> BuildClues(ExecutionContext context, IExternalSearchQuery query, IExternalSearchQueryResult result, IExternalSearchRequest request)
        { 
            if (result is not IExternalSearchQueryResult<ImageDetailsResponse> companyResult) return null;
           
            var code = new EntityCode(request.EntityMetaData.EntityType, "googleimages", $"{query.QueryKey}{request.EntityMetaData.OriginEntityCode}".ToDeterministicGuid());
            var clue = new Clue(code, context.Organization);

            PopulateCompanyMetadata(clue.Data.EntityData, companyResult, request);
            DownloadPreviewImage(context, companyResult.Data.items.First().link, clue);
            // TODO: If necessary, you can create multiple clues and return them.

            return [clue];
        }

        /// <summary>Gets the primary entity metadata.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The primary entity metadata.</returns>
        public override IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request)
        {
            if (result is not IExternalSearchQueryResult<ImageDetailsResponse> companyResult) return null;

            return companyResult.Data.items != null ? CreateCompanyMetadata(companyResult, request) : null;
        }

        /// <summary>Gets the preview image.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The preview image.</returns>
        public override IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request)
        { 
            return DownloadPreviewImageBlob<ImageDetailsResponse>(context, result, r => r.Data.items.First().link);
        }

        private static IEntityMetadata CreateCompanyMetadata(IExternalSearchQueryResult<ImageDetailsResponse> resultItem, IExternalSearchRequest request)
        {
            var metadata = new EntityMetadataPart();

            PopulateCompanyMetadata(metadata, resultItem, request);

            return metadata;
        }

        // ReSharper disable once UnusedParameter.Local
        private static void PopulateCompanyMetadata(IEntityMetadata metadata, IExternalSearchQueryResult<ImageDetailsResponse> resultItem, IExternalSearchRequest request)
        {
            var code = new EntityCode(request.EntityMetaData.EntityType, "googleimages", $"{request.Queries.FirstOrDefault()?.QueryKey}{request.EntityMetaData.OriginEntityCode}".ToDeterministicGuid());

            metadata.EntityType = request.EntityMetaData.EntityType;
            metadata.Name = request.EntityMetaData.Name;
            metadata.OriginEntityCode = code;
            metadata.Codes.Add(request.EntityMetaData.OriginEntityCode);
        }

        public IEnumerable<EntityType> Accepts(IDictionary<string, object> config, IProvider provider)
        {
            var customTypes = config[Constants.KeyName.AcceptedEntityType].ToString();
            if (!string.IsNullOrWhiteSpace(customTypes))
            {
                AcceptedEntityTypes = [config[Constants.KeyName.AcceptedEntityType].ToString()];
            }

            return AcceptedEntityTypes;
        }

        public ConnectionVerificationResult VerifyConnection(ExecutionContext context, IReadOnlyDictionary<string, object> config)
        {
            IDictionary<string, object> configDict = config.ToDictionary(entry => entry.Key, entry => entry.Value);
            var jobData = new GoogleImagesExternalSearchJobData(configDict);
            var apiToken = jobData.ApiToken;
            var client = new RestClient("https://www.googleapis.com/customsearch/v1");
            
            var request = new RestRequest($"?key={apiToken}&cx=000950167190857528722:vf0rypkbf0w&q=Google&searchType=image", Method.GET);

            var response = client.ExecuteAsync<ImageDetailsResponse>(request).Result;

            if (response.StatusCode != HttpStatusCode.OK) return ConstructVerifyConnectionResponse(response);
            return response.Data == null ? new ConnectionVerificationResult(true, string.Empty) : ConstructVerifyConnectionResponse(response);
        }

        private static ConnectionVerificationResult ConstructVerifyConnectionResponse(IRestResponse response)
        {
            var errorMessageBase = $"{Constants.ProviderName} returned \"{(int)response.StatusCode} {response.StatusDescription}\".";

            if (response.ErrorException != null)
            {
                return new ConnectionVerificationResult(
                    false,
                    $"{errorMessageBase} {(!string.IsNullOrWhiteSpace(response.ErrorException.Message) ? response.ErrorException.Message : "This could be due to breaking changes in the external system")}."
                );
            }

            if (!string.IsNullOrWhiteSpace(response.Content) && response.Content.Contains("API_KEY_INVALID"))
            {
                return new ConnectionVerificationResult(false, $"{errorMessageBase} This could be due to an invalid API key.");
            }

            var regex = new Regex(@"\<(html|head|body|div|span|img|p\>|a href)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
            var isHtml = regex.IsMatch(response.Content ?? string.Empty);

            var errorMessage = response.IsSuccessful
                ? string.Empty
                : string.IsNullOrWhiteSpace(response.Content) || isHtml
                    ? $"{errorMessageBase} This could be due to breaking changes in the external system."
                    : $"{errorMessageBase} {response.Content}.";

            return new ConnectionVerificationResult(response.IsSuccessful, errorMessage);
        }

        public IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return InternalBuildQueries(context, request, config);
        }

        public IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context, IExternalSearchQuery query, IDictionary<string, object> config, IProvider provider)
        {
            var jobData = new GoogleImagesExternalSearchJobData(config);

            foreach (var externalSearchQueryResult in InternalExecuteSearch(query, jobData.ApiToken)) yield return externalSearchQueryResult;
        }

        public IEnumerable<Clue> BuildClues(ExecutionContext context, IExternalSearchQuery query, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return BuildClues(context, query, result, request);
        }

        public IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return GetPrimaryEntityMetadata(context, result, request);
        }

        public IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return GetPrimaryEntityPreviewImage(context, result, request);
        }

        public string Icon { get; } = Constants.Icon;
        public string Domain { get; } = Constants.Domain;
        public string About { get; } = Constants.About;

        public AuthMethods AuthMethods { get; } = Constants.AuthMethods;
        public IEnumerable<Control> Properties { get; } = Constants.Properties;
        public Guide Guide { get; } = Constants.Guide;
        public IntegrationType Type { get; } = Constants.IntegrationType;


    }
}