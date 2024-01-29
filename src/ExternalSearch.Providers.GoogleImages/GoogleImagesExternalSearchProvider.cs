using CluedIn.Core;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.ExternalSearch;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Providers;
using EntityType = CluedIn.Core.Data.EntityType;
using CluedIn.Core.Data.Vocabularies;
using CluedIn.ExternalSearch.Providers.GoogleMaps.Models.Companies;

namespace CluedIn.ExternalSearch.Providers.GoogleImages
{
    /// <summary>The googlemaps graph external search provider.</summary>
    /// <seealso cref="CluedIn.ExternalSearch.ExternalSearchProviderBase" />
    public class GoogleImagesExternalSearchProvider : ExternalSearchProviderBase, IExtendedEnricherMetadata, IConfigurableExternalSearchProvider
    {
        private static EntityType[] AcceptedEntityTypes = { EntityType.Organization };

        /**********************************************************************************************************
         * CONSTRUCTORS
         **********************************************************************************************************/

        public GoogleImagesExternalSearchProvider()
            : base(Constants.ProviderId, AcceptedEntityTypes)
        {
            var nameBasedTokenProvider = new NameBasedTokenProvider("GoogleImages");

            if (nameBasedTokenProvider.ApiToken != null)
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
            foreach (var externalSearchQuery in InternalBuildQueries(context, request))
            {
                yield return externalSearchQuery;
            }
        }
        private IEnumerable<IExternalSearchQuery> InternalBuildQueries(ExecutionContext context, IExternalSearchRequest request, IDictionary<string, object> config = null)
        {
            if (config.TryGetValue(Constants.KeyName.AcceptedEntityType, out var customType) && !string.IsNullOrWhiteSpace(customType?.ToString()))
            {
                if (!request.EntityMetaData.EntityType.Is(customType.ToString()))
                {
                    yield break;
                }
            }
            else if (!this.Accepts(request.EntityMetaData.EntityType))
                yield break;

            var existingResults = request.GetQueryResults<ImageDetailsResponse>(this).ToList();

            bool NameFilter(string value)
            {
                return existingResults.Any(r => string.Equals(r.Data.queries.request.First().searchTerms, value, StringComparison.InvariantCultureIgnoreCase));
            }

            var entityType = request.EntityMetaData.EntityType;

            var organizationName = GetValue(request, config, Constants.KeyName.ImageSearch, Core.Data.Vocabularies.Vocabularies.CluedInOrganization.OrganizationName);



            if (organizationName != null && organizationName.Count > 0)
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

            if (query.QueryParameters.ContainsKey("companyName"))
            {
                var name = query.QueryParameters["companyName"].FirstOrDefault();
                var key = apiKey;
                var request = new RestRequest($"key={key}&cx=000950167190857528722:vf0rypkbf0w&q={name}&searchType=image", Method.GET);
           
                var response = client.ExecuteTaskAsync<ImageDetailsResponse>(request).Result;
               
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (response.Data != null)
                        yield return new ExternalSearchQueryResult<ImageDetailsResponse>(query, response.Data);
                }
                else if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
                    yield break;
                else if (response.ErrorException != null)
                    throw new AggregateException(response.ErrorException.Message, response.ErrorException);
                else
                    throw new ApplicationException("Could not execute external search query - StatusCode:" + response.StatusCode + "; Content: " + response.Content);
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

           if (result is IExternalSearchQueryResult<ImageDetailsResponse> companyResult)
            {

                var code = this.GetOrganizationOriginEntityCode(companyResult, request);

                var clue = new Clue(code, context.Organization);

                this.PopulateCompanyMetadata(clue.Data.EntityData, companyResult, request);
                this.DownloadPreviewImage(context, companyResult.Data.items.First().link, clue);
                // TODO: If necessary, you can create multiple clues and return them.

                return new[] { clue };
            }

            return null;


        }

        /// <summary>Gets the primary entity metadata.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The primary entity metadata.</returns>
        public override IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request)
        {

            if (result is IExternalSearchQueryResult<ImageDetailsResponse> companyResult)
            {
                if (companyResult.Data.items != null)
                {
                    return this.CreateCompanyMetadata(companyResult, request);
                }
            }
            return null;
        }

        /// <summary>Gets the preview image.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The preview image.</returns>
        public override IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request)
        {
            return null;
        }

  

        private IEntityMetadata CreateCompanyMetadata(IExternalSearchQueryResult<ImageDetailsResponse> resultItem, IExternalSearchRequest request)
        {
            var metadata = new EntityMetadataPart();

            this.PopulateCompanyMetadata(metadata, resultItem, request);

            return metadata;
        }


      
        private EntityCode GetOrganizationOriginEntityCode(IExternalSearchQueryResult<ImageDetailsResponse> resultItem, IExternalSearchRequest request)
        {

            return new EntityCode(request.EntityMetaData.EntityType, this.GetCodeOrigin(), request.EntityMetaData.OriginEntityCode.Value);
        }

        /// <summary>Gets the code origin.</summary>
        /// <returns>The code origin</returns>
        private CodeOrigin GetCodeOrigin()
        {
            return CodeOrigin.CluedIn.CreateSpecific("googleimages");
        }

       

        private void PopulateCompanyMetadata(IEntityMetadata metadata, IExternalSearchQueryResult<ImageDetailsResponse> resultItem, IExternalSearchRequest request)
        {
            var code = this.GetOrganizationOriginEntityCode(resultItem, request);

            metadata.EntityType = request.EntityMetaData.EntityType;
            metadata.Name = request.EntityMetaData.Name;
            metadata.OriginEntityCode = code;
            metadata.Codes.Add(code);
            metadata.Codes.Add(request.EntityMetaData.OriginEntityCode);
        }

        public IEnumerable<EntityType> Accepts(IDictionary<string, object> config, IProvider provider)
        {
            var customTypes = config[Constants.KeyName.AcceptedEntityType].ToString();
            if (string.IsNullOrWhiteSpace(customTypes))
            {
                AcceptedEntityTypes = new EntityType[] { config[Constants.KeyName.AcceptedEntityType].ToString() };
            };

            return AcceptedEntityTypes;
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