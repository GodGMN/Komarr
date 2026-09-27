using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MetadataSource.Audnex.Resources;

namespace NzbDrone.Core.MetadataSource.Audnex
{
    // Phase 7 audiobook augmenter. Pulls narrator + duration + ASIN-keyed
    // cover from api.audnex.us (community-hosted Audible mirror). Opt-in
    // via IConfigService.AugmentAudiobookMetadata — off by default because
    // audnex.us is a community service that may go away.
    //
    // Not registered against IProvideBookInfo on purpose: this is an
    // *augmenter*, layered on top of the primary metadata source's Book.
    // RefreshBookService.GetSkyhookData calls Augment() after the primary
    // refresh; failures are swallowed there so the main path is never
    // blocked by a transient audnex outage.
    public class AudnexProxy : IAugmentAudiobookInfo
    {
        private const string BaseUrl = "https://api.audnex.us";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public AudnexProxy(IHttpClient httpClient,
                           IConfigService configService,
                           Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public bool CanAugment(Book book)
        {
            if (!_configService.AugmentAudiobookMetadata)
            {
                return false;
            }

            if (book?.Editions?.Value == null)
            {
                return false;
            }

            return book.Editions.Value.Any(e => HasAsin(e) && IsAudiobookFormat(e));
        }

        public Book Augment(Book book)
        {
            if (!CanAugment(book))
            {
                return book;
            }

            var edition = book.Editions.Value.FirstOrDefault(e => HasAsin(e) && IsAudiobookFormat(e));
            if (edition == null)
            {
                return book;
            }

            AudnexBookResource resource;
            try
            {
                var request = new HttpRequestBuilder($"{BaseUrl}/books/{edition.Asin}")
                    .Accept(HttpAccept.Json)
                    .SetHeader("User-Agent", MetadataUserAgent.For("audiobook metadata"))
                    .WithRateLimit(1.0)
                    .Build();
                request.SuppressHttpError = true;

                var response = _httpClient.Get<AudnexBookResource>(request);
                resource = response?.Resource;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Audnex augment failed for ASIN {0}; falling back to primary metadata", edition.Asin);
                return book;
            }

            if (resource == null)
            {
                return book;
            }

            // Merge: only fill blanks. Don't overwrite the primary source's
            // authoritative title / overview / publisher.
            if (edition.Overview.IsNullOrWhiteSpace() && resource.Summary.IsNotNullOrWhiteSpace())
            {
                edition.Overview = resource.Summary;
            }

            if (edition.Publisher.IsNullOrWhiteSpace() && resource.Publisher.IsNotNullOrWhiteSpace())
            {
                edition.Publisher = resource.Publisher;
            }

            if (edition.PageCount == 0 && resource.RuntimeLengthMin.HasValue)
            {
                // Reuse PageCount for runtime-minutes — there's no Duration
                // field on Edition (yet). Phase 7b can introduce one if the
                // UI ever needs to render hours/minutes.
                edition.PageCount = resource.RuntimeLengthMin.Value;
            }

            if (!edition.ReleaseDate.HasValue && resource.ReleaseDate.HasValue)
            {
                edition.ReleaseDate = resource.ReleaseDate;
            }

            // Narrators land in the in-memory NarratorList. RefreshEditionService
            // syncs the list into the Narrators / EditionNarrators schema
            // (migration 043) after the edition is persisted. The legacy
            // Editions.Narrators string column was dropped in migration 044 —
            // the lazy-loaded list is now the only narrator field on Edition.
            var hasExistingNarrators = edition.NarratorList?.IsLoaded == true
                && edition.NarratorList.Value?.Count > 0;
            if (!hasExistingNarrators && resource.Narrators?.Count > 0)
            {
                var names = resource.Narrators
                    .Where(n => n?.Name.IsNotNullOrWhiteSpace() == true)
                    .Select(n => new Narrator { Name = n.Name })
                    .ToList();
                if (names.Count > 0)
                {
                    edition.NarratorList = new LazyLoaded<List<Narrator>>(names);
                }
            }

            return book;
        }

        private static bool HasAsin(Edition edition)
        {
            return edition?.Asin.IsNotNullOrWhiteSpace() == true;
        }

        private static bool IsAudiobookFormat(Edition edition)
        {
            // Strictly Format == AudioBook. The IsEbook flag is true for
            // *both* ebook and audiobook formats (see
            // OpenLibraryEditionMapper.IsEbookFormat), so checking it
            // here would fire audnex lookups against Kindle ebooks too,
            // and audnex only carries Audible audiobook data.
            return edition?.Format == "AudioBook";
        }
    }
}
