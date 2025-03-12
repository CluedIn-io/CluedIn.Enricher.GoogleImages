using System.Collections.Generic;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Messages.Processing;
using CluedIn.ExternalSearch.Providers.GoogleImages;
using CluedIn.Testing.Base.ExternalSearch;
using Moq;
using Xunit;
using Xunit.Abstractions;

//using TestContext = CluedIn.Tests.Unit.TestContext;

namespace CluedIn.ExternalSearch.GoogleImages.Integration.Tests
{
    public class GoogleImagesTests : BaseExternalSearchTest<GoogleImagesExternalSearchProvider>
    {
        private readonly ITestOutputHelper outputHelper;

        public GoogleImagesTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        [Theory(Skip = "Requires a working api key")]
        //[InlineData("CluedIn APS", "Titangade 11", "Titangade 11, 2200 København, Denmark")]
        //[InlineData("CluedIn APS", "11 Titangade", "Titangade 11, 2200 København, Denmark")]
        //[InlineData("CluedIn APS", "Denmark, Titangade 11", "Titangade 11, 2200 København, Denmark")]
        [InlineData("Gowan Rd", "-27.595919728", "153.064914419", "Gowan Rd, Brisbane QLD, Australia")]
        // Fails since api call does not yield enough information, 
        // and no persons are returned.
        public void TestClueProductionLocation(/*string name, */string address, string lattitude, string longitude, string formattedAdress)
        {
            var code = new EntityCode(EntityType.Location, CodeOrigin.CluedIn, $"{lattitude}, {longitude}");

            var list = new List<string>(new string[] { "AIzaSyA8oZKYh7NT4bX_yZl8vKIMdecoQCHJC4I" });
            object[] parameters = { list };
            var properties = new EntityMetadataPart();
            properties.Properties.Add(CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInLocation.AddressNameStreet, address);
            properties.Properties.Add(CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInLocation.AddressLattitude, lattitude);
            properties.Properties.Add(CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInLocation.AddressLongitude, longitude);

            IEntityMetadata entityMetadata = new EntityMetadataPart()
            {
                EntityType = EntityType.Location,
                Properties = properties.Properties,
                OriginEntityCode = code,
            };

            entityMetadata.Codes.Add(code);

            //var tokenProvider = new DummyTokenProvider("AIzaSyA8oZKYh7NT4bX_yZl8vKIMdecoQCHJC4I");

            this.Setup(parameters, entityMetadata);

            // Assert
            this.testContext.ProcessingHub.Verify(h => h.SendCommand(It.IsAny<ProcessClueCommand>()), Times.AtLeastOnce);
            Assert.True(this.clues.Count > 0);
            foreach (var clue in this.clues)
            {
                var c = clue.Decompress();

                this.outputHelper.WriteLine(c.Serialize());

                c.Data.EntityData.Properties.TryGetValue("googleMaps.Location.formattedAddress", out var value);

                Assert.Equal(formattedAdress, value);
            }
        }

        [Theory(Skip = "Requires a working api key")]
        [InlineData("CluedIn APS", "Denmark, Titangade 11", "Titangade 11, 2200 København, Denmark")]
        // Fails since api call does not yield enough information, 
        // and no persons are returned.
        public void TestClueProductionOrganization(string name, string address, string formattedAdress)
        {
            var list = new List<string>(new string[] { "AIzaSyA8oZKYh7NT4bX_yZl8vKIMdecoQCHJC4I" });
            object[] parameters = { list };
            var properties = new EntityMetadataPart();
            properties.Properties.Add(CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInOrganization.OrganizationName, name);
            properties.Properties.Add(CluedIn.Core.Data.Vocabularies.Vocabularies.CluedInOrganization.Address, address);

            IEntityMetadata entityMetadata = new EntityMetadataPart()
            {
                EntityType = EntityType.Organization,
                Properties = properties.Properties
            };

            //var tokenProvider = new DummyTokenProvider("AIzaSyA8oZKYh7NT4bX_yZl8vKIMdecoQCHJC4I");

            this.Setup(parameters, entityMetadata);

            // Assert
            this.testContext.ProcessingHub.Verify(h => h.SendCommand(It.IsAny<ProcessClueCommand>()), Times.AtLeastOnce);
            Assert.True(clues.Count > 0);
            foreach (var clue in this.clues)
            {
                clue.Decompress().Data.EntityData.Properties
                    .TryGetValue("googleMaps.Organization.FormattedAddress", out var value);

                Assert.Equal(formattedAdress, value);
            }
        }

        [Theory]

        [InlineData(null)]
        public void TestNoClueProduction(string name)
        {
            IEntityMetadata entityMetadata = new EntityMetadataPart()
            {
                Name = name,
                EntityType = EntityType.Organization
            };

            this.Setup(null, entityMetadata);

            // Assert
            this.testContext.ProcessingHub.Verify(h => h.SendCommand(It.IsAny<ProcessClueCommand>()), Times.Never);
            Assert.True(clues.Count == 0);
        }

    }
}