using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books.Model;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    // Phase 6 series provider. Concrete class — interface declarations live
    // on MetadataSourceFactory, same pattern as OpenLibraryProxy. The foreign
    // series ID is a Wikidata Q-number (e.g., Q47461 = Foundation series),
    // NOT an OL work key. Migrating from Goodreads series IDs (which are
    // ints) requires a one-time mapping per series — out of scope here;
    // the Phase 6 reidentify pass writes BookIdMapping rows for series too.
    public class OpenLibrarySeriesProxy
    {
        private readonly IWikidataClient _wikidata;
        private readonly Logger _logger;

        public OpenLibrarySeriesProxy(IWikidataClient wikidata, Logger logger)
        {
            _wikidata = wikidata;
            _logger = logger;
        }

        // Matches IProvideSeriesInfo.GetSeriesInfo. foreignSeriesId is the
        // Wikidata Q-number without the "Q" prefix or "wd:" namespace —
        // e.g., "47461" (Foundation), "8517" (Discworld).
        public SeriesInfo GetSeriesInfo(string foreignSeriesId, bool useCache = true)
        {
            var qid = NormalizeQid(foreignSeriesId);
            var sparql = BuildSeriesQuery(qid);

            var response = _wikidata.Query(sparql);
            if (response?.Results?.Bindings == null)
            {
                _logger.Debug("Wikidata returned no bindings for series {0}", foreignSeriesId);
                return new SeriesInfo { ForeignSeriesId = foreignSeriesId };
            }

            var info = new SeriesInfo
            {
                ForeignSeriesId = foreignSeriesId
            };

            foreach (var binding in response.Results.Bindings)
            {
                var workUri = TryGet(binding, "work");
                var workLabel = TryGet(binding, "workLabel");
                var ordinal = TryGet(binding, "ordinal");
                var olid = TryGet(binding, "olid");

                if (workUri.IsNullOrWhiteSpace())
                {
                    continue;
                }

                info.Books.Add(new BookListItem
                {
                    ForeignBookId = olid ?? ExtractEntityId(workUri),
                    Title = workLabel,
                    ForeignEditionId = null,
                    AuthorName = null,
                    ForeignAuthorId = null,
                    Position = ordinal
                });
            }

            // TODO Phase 6b: fetch the series label + description in a second
            // pass (single SPARQL query for the Q-id rather than bundling
            // with the works query — keeps each query simple).
            return info;
        }

        // Matches IProvideListInfo.GetListInfo. OL/Wikidata don't have a
        // direct equivalent to Goodreads listopia; the Phase 6 design
        // replaces this with import-list providers (OpenLibrarySubjectImportList
        // etc.) rather than ad-hoc lookups. Returns empty so consumers degrade
        // gracefully if anything still calls it on OpenLibrary mode.
        public ListInfo GetListInfo(string foreignListId, int page, bool useCache = true)
        {
            _logger.Debug("OL ListInfo lookup requested for {0} (page {1}) — OL has no listopia analog; returning empty", foreignListId, page);
            return new ListInfo { ForeignListId = foreignListId, Page = page, PerPage = 0, TotalBooks = 0 };
        }

        private static string NormalizeQid(string raw)
        {
            if (raw.IsNullOrWhiteSpace())
            {
                return raw;
            }

            // Strip "Q" prefix and "wd:" namespace if present.
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("wd:", System.StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(3);
            }

            if (trimmed.StartsWith("Q", System.StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            return trimmed;
        }

        private static string BuildSeriesQuery(string qid)
        {
            // P179 = part of the series. P1545 = series ordinal. P648 = OL ID.
            // ORDER BY uses xsd:decimal so "1.5" sorts between "1" and "2".
            return $@"SELECT ?work ?workLabel ?ordinal ?olid WHERE {{
  ?work wdt:P179 wd:Q{qid} .
  OPTIONAL {{ ?work wdt:P1545 ?ordinal . }}
  OPTIONAL {{ ?work wdt:P648 ?olid . }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
}}
ORDER BY xsd:decimal(?ordinal)";
        }

        private static string TryGet(Dictionary<string, WikidataSparqlValue> binding, string key)
        {
            return binding != null && binding.TryGetValue(key, out var v) ? v?.Value : null;
        }

        private static string ExtractEntityId(string uri)
        {
            // "http://www.wikidata.org/entity/Q47461" → "Q47461"
            if (uri.IsNullOrWhiteSpace())
            {
                return uri;
            }

            var slash = uri.LastIndexOf('/');
            return slash >= 0 ? uri.Substring(slash + 1) : uri;
        }
    }
}
