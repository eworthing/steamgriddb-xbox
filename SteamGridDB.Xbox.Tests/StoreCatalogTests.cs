using System.Collections.Generic;
using System.Linq;

using SteamGridDB.Xbox.Services.Stores;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Reading the Store's display catalogue.
    ///
    /// The response is deeply nested and almost entirely irrelevant - a product carries twenty
    /// top-level members and its localised half another twenty-five - so what these pin is the narrow
    /// path through it, against a response trimmed from a real one rather than invented. The document
    /// below keeps the field names and the shapes exactly as the service returns them, including the
    /// protocol-relative image URIs, which no HTTP client accepts as written.
    ///
    /// The request itself is not tested, for the same reason SteamGridDbClient's is not: a test that
    /// called the live service would be grading Microsoft's uptime.
    /// </summary>
    public class StoreCatalogTests
    {
        private const string wobblyLife = @"{
  ""Products"": [
    {
      ""ProductId"": ""9NS86BQ33SPX"",
      ""ProductKind"": ""Game"",
      ""ProductType"": ""Application"",
      ""LocalizedProperties"": [
        {
          ""ProductTitle"": ""Wobbly Life"",
          ""ShortTitle"": """",
          ""DeveloperName"": ""RubberBandGames"",
          ""Images"": [
            { ""ImagePurpose"": ""Logo"", ""Height"": 300, ""Width"": 300,
              ""Uri"": ""//store-images.s-microsoft.com/image/apps.51495.logo"" },
            { ""ImagePurpose"": ""Poster"", ""Height"": 1080, ""Width"": 720,
              ""Uri"": ""//store-images.s-microsoft.com/image/apps.4650.poster"" },
            { ""ImagePurpose"": ""BoxArt"", ""Height"": 1080, ""Width"": 1080,
              ""Uri"": ""//store-images.s-microsoft.com/image/apps.14861.boxart"" },
            { ""ImagePurpose"": ""SuperHeroArt"", ""Height"": 1080, ""Width"": 1920,
              ""Uri"": ""//store-images.s-microsoft.com/image/apps.15760.hero"" },
            { ""ImagePurpose"": ""FeaturePromotionalSquareArt"", ""Height"": 600, ""Width"": 600,
              ""Uri"": ""//store-images.s-microsoft.com/image/apps.46000.square"" }
          ]
        }
      ]
    }
  ]
}";

        private static StoreCatalog.Product Single(string json)
        {
            return Assert.Single(StoreCatalog.ParseProducts(json));
        }

        [Fact]
        public void Reads_the_product_id_title_and_kind()
        {
            StoreCatalog.Product product = Single(wobblyLife);

            Assert.Equal("9NS86BQ33SPX", product.StoreId);
            Assert.Equal("Wobbly Life", product.Title);
            Assert.Equal("Game", product.ProductKind);
            Assert.True(product.IsGame);
        }

        [Fact]
        public void Takes_only_the_square_tile_artwork()
        {
            // Poster and hero art are the wrong shape to ever be a tile, and Logo is a small square
            // several unrelated games share - matching on it produces false positives
            Assert.Equal(
                new[]
                {
                    "https://store-images.s-microsoft.com/image/apps.14861.boxart",
                    "https://store-images.s-microsoft.com/image/apps.46000.square",
                },
                Single(wobblyLife).TileArtworkUris.ToArray());
        }

        [Fact]
        public void Orders_tile_artwork_largest_first()
        {
            // The 1080px box art before the 600px square, whatever order the document lists them in
            IReadOnlyList<string> uris = Single(wobblyLife).TileArtworkUris;

            Assert.EndsWith("boxart", uris[0]);
            Assert.EndsWith("square", uris[1]);
        }

        [Fact]
        public void Makes_protocol_relative_uris_absolute()
        {
            Assert.All(Single(wobblyLife).TileArtworkUris, uri => Assert.StartsWith("https://", uri));
        }

        [Theory]
        [InlineData("//store-images.s-microsoft.com/x", "https://store-images.s-microsoft.com/x")]
        [InlineData("https://already.absolute/x", "https://already.absolute/x")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void NormaliseUri_only_fills_in_a_missing_scheme(string input, string expected)
        {
            Assert.Equal(expected, StoreCatalog.NormaliseUri(input));
        }

        [Fact]
        public void A_content_pack_is_not_a_game()
        {
            StoreCatalog.Product product = Single(@"{""Products"":[{
                ""ProductId"":""9P4RL1XBDMSN"", ""ProductKind"":""Durable"",
                ""LocalizedProperties"":[{""ProductTitle"":""Black Ops 6 - Content Pack 1"",""Images"":[]}]}]}");

            Assert.False(product.IsGame);
        }

        [Fact]
        public void IsGame_ignores_casing()
        {
            StoreCatalog.Product product = Single(@"{""Products"":[{""ProductId"":""X"",""ProductKind"":""GAME""}]}");

            Assert.True(product.IsGame);
        }

        [Fact]
        public void A_product_with_no_localised_properties_still_yields_its_id()
        {
            // Worth keeping rather than dropping: the game can still be identified and its tile
            // customised by hand, it just has no name to search SteamGridDB with
            StoreCatalog.Product product = Single(@"{""Products"":[{""ProductId"":""9NS86BQ33SPX"",""ProductKind"":""Game""}]}");

            Assert.Equal("9NS86BQ33SPX", product.StoreId);
            Assert.Null(product.Title);
            Assert.Empty(product.TileArtworkUris);
        }

        [Fact]
        public void A_product_with_no_id_is_dropped()
        {
            Assert.Empty(StoreCatalog.ParseProducts(@"{""Products"":[{""ProductKind"":""Game""}]}"));
        }

        [Fact]
        public void Non_square_artwork_is_never_offered_as_a_tile_reference()
        {
            StoreCatalog.Product product = Single(@"{""Products"":[{""ProductId"":""X"",""ProductKind"":""Game"",
                ""LocalizedProperties"":[{""ProductTitle"":""X"",""Images"":[
                    { ""ImagePurpose"":""BoxArt"", ""Height"":900, ""Width"":600, ""Uri"":""//host/tall"" }]}]}]}");

            Assert.Empty(product.TileArtworkUris);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData(@"{""Products"":null}")]
        [InlineData(@"{""Products"":[]}")]
        public void Unusable_responses_yield_nothing_rather_than_throwing(string json)
        {
            Assert.Empty(StoreCatalog.ParseProducts(json));
        }
    }
}
