using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Nyaa
{
    public class NyaaRequestGenerator : IIndexerRequestGenerator
    {
        public NyaaSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(null));

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var term = searchCriteria.BookTitle.IsNotNullOrWhiteSpace()
                ? searchCriteria.BookQuery
                : searchCriteria.AuthorQuery;

            return BuildSearchRequests(term);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var term = (searchCriteria as MangaSearchCriteria)?.MangaTitle;

            if (term.IsNullOrWhiteSpace())
            {
                term = searchCriteria.AuthorQuery;
            }

            return BuildSearchRequests(term);
        }

        private IndexerPageableRequestChain BuildSearchRequests(string term)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            if (term.IsNotNullOrWhiteSpace())
            {
                pageableRequests.Add(GetPagedRequests(PrepareQuery(term)));
            }

            return pageableRequests;
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term)
        {
            var baseUrl = $"{Settings.BaseUrl.TrimEnd('/')}/?page=rss{Settings.AdditionalParameters}";

            if (term != null)
            {
                baseUrl += "&term=" + term;
            }

            yield return new IndexerRequest(baseUrl, HttpAccept.Rss);
        }

        private string PrepareQuery(string query)
        {
            return Uri.EscapeDataString(query.Replace("+", " ").Trim());
        }
    }
}
