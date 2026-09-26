using System.Collections.Generic;
using Newtonsoft.Json;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public interface IWikidataClient
    {
        WikidataSparqlResponse Query(string sparql);
    }

    // Thin SPARQL client for query.wikidata.org. Used by OpenLibrarySeriesProxy
    // to assemble series metadata that OL doesn't natively carry — series
    // ordinals (P1545) and "part of the series" (P179) live on Wikidata,
    // not on OL works. Most OL works that are part of a series have a
    // sister Wikidata item linked via P648 (Open Library ID).
    //
    // Wikidata enforces a User-Agent policy strictly — anonymous queries
    // get rate-limited fast. See https://meta.wikimedia.org/wiki/User-Agent_policy.
    public class WikidataClient : IWikidataClient
    {
        private const string Endpoint = "https://query.wikidata.org/sparql";

        private readonly IHttpClient _httpClient;

        public WikidataClient(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public WikidataSparqlResponse Query(string sparql)
        {
            var request = new HttpRequestBuilder(Endpoint)
                .AddQueryParam("query", sparql)
                .AddQueryParam("format", "json")
                .Accept(HttpAccept.Json)
                .SetHeader("User-Agent", MetadataUserAgent.For("series metadata"))
                .WithRateLimit(1.5)
                .Build();

            var response = _httpClient.Get<WikidataSparqlResponse>(request);
            return response?.Resource;
        }
    }

    // SPARQL JSON shape per https://www.w3.org/TR/sparql11-results-json/.
    public class WikidataSparqlResponse
    {
        [JsonProperty("head")]
        public WikidataSparqlHead Head { get; set; }

        [JsonProperty("results")]
        public WikidataSparqlResults Results { get; set; }
    }

    public class WikidataSparqlHead
    {
        [JsonProperty("vars")]
        public List<string> Vars { get; set; }
    }

    public class WikidataSparqlResults
    {
        [JsonProperty("bindings")]
        public List<Dictionary<string, WikidataSparqlValue>> Bindings { get; set; }
    }

    public class WikidataSparqlValue
    {
        // "uri" | "literal" | "bnode"
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("datatype")]
        public string Datatype { get; set; }

        [JsonProperty("xml:lang")]
        public string Language { get; set; }
    }
}
