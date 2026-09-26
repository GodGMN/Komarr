using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaCover;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    // Phase 4b. OL's bibliographic JSON returns cover/photo IDs as
    // integers; the actual image is at
    //   https://covers.openlibrary.org/b/id/{id}-L.jpg     (book/edition)
    //   https://covers.openlibrary.org/a/id/{id}-L.jpg     (author photo)
    // OL uses -L (large), -M (medium), -S (small). We store the -L URL.
    //
    // Negative cover IDs in OL responses are sentinels meaning "no
    // cover" — they must be filtered out before constructing URLs.
    internal static class OpenLibraryCoverUrls
    {
        private const string BookCoverByIdFormat = "https://covers.openlibrary.org/b/id/{0}-L.jpg";
        private const string AuthorPhotoByIdFormat = "https://covers.openlibrary.org/a/id/{0}-L.jpg";

        // OL also serves covers/photos keyed by the work/author OLID
        // ("olid" path) without needing a specific cover_i / photo id.
        // Useful for search responses where only the OLID is in scope.
        // Append `?default=false` so OL returns 404 instead of a 1px
        // transparent placeholder; the frontend's own placeholder is
        // nicer than a blank cell.
        private const string BookCoverByOlidFormat = "https://covers.openlibrary.org/b/olid/{0}-L.jpg?default=false";
        private const string AuthorPhotoByOlidFormat = "https://covers.openlibrary.org/a/olid/{0}-L.jpg?default=false";

        // OL serves edition-keyed covers at /b/isbn/<isbn>-L.jpg. Useful
        // when search results include an ISBN but the work has no
        // canonical work-level cover (Library Phase-3 fallback chain:
        // cover_i → ISBN → olid).
        private const string BookCoverByIsbnFormat = "https://covers.openlibrary.org/b/isbn/{0}-L.jpg?default=false";

        public static List<MediaCover.MediaCover> ForBook(List<int> coverIds)
        {
            return BuildCovers(coverIds, BookCoverByIdFormat, MediaCoverTypes.Cover);
        }

        public static List<MediaCover.MediaCover> ForAuthor(List<int> photoIds)
        {
            return BuildCovers(photoIds, AuthorPhotoByIdFormat, MediaCoverTypes.Poster);
        }

        public static List<MediaCover.MediaCover> ForBookByOlid(string workOlid)
        {
            return BuildOlidCover(workOlid, BookCoverByOlidFormat, MediaCoverTypes.Cover);
        }

        public static List<MediaCover.MediaCover> ForAuthorByOlid(string authorOlid)
        {
            return BuildOlidCover(authorOlid, AuthorPhotoByOlidFormat, MediaCoverTypes.Poster);
        }

        public static List<MediaCover.MediaCover> ForBookByIsbn(string isbn)
        {
            return BuildOlidCover(isbn, BookCoverByIsbnFormat, MediaCoverTypes.Cover);
        }

        // OL rate-limits the covers API to 100 requests per IP per 5 minutes and
        // answers 403 beyond it — but ONLY for lookups keyed by something other
        // than CoverID or OLID. Requests to /b/id/ and /a/id/ (CoverID) and to
        // /b/olid/ and /a/olid/ (OLID) are explicitly exempt:
        //   https://openlibrary.org/dev/docs/api/covers
        //
        // So the ISBN fallback in the cover chain (cover_i -> ISBN -> olid) is
        // the only form that can get us blocked, and a library refresh issues
        // one per edition lacking a work-level cover. Throttle just that form;
        // throttling the exempt majority would slow every refresh for nothing.
        //
        // 100 per 5 minutes is one per 3 seconds.
        public static TimeSpan? RateLimitFor(string url)
        {
            if (url.IsNullOrWhiteSpace())
            {
                return null;
            }

            return url.Contains("/isbn/", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromSeconds(3)
                : null;
        }

        private static List<MediaCover.MediaCover> BuildCovers(List<int> ids, string urlFormat, MediaCoverTypes type)
        {
            if (ids == null)
            {
                return new List<MediaCover.MediaCover>();
            }

            return ids
                .Where(id => id > 0)
                .Distinct()
                .Select(id => new MediaCover.MediaCover
                {
                    Url = string.Format(urlFormat, id),
                    CoverType = type
                })
                .ToList();
        }

        private static List<MediaCover.MediaCover> BuildOlidCover(string olid, string urlFormat, MediaCoverTypes type)
        {
            if (string.IsNullOrWhiteSpace(olid))
            {
                return new List<MediaCover.MediaCover>();
            }

            return new List<MediaCover.MediaCover>
            {
                new MediaCover.MediaCover
                {
                    Url = string.Format(urlFormat, olid),
                    CoverType = type
                }
            };
        }
    }
}
