using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibraryWorkMapper
    {
        // Returns the assembled Book plus any AuthorMetadata entries inferred
        // from the work + editions. AuthorMetadata is intentionally slim here;
        // callers that need full author data fetch /authors/{key}.json separately.
        public static (Book Book, List<AuthorMetadata> Authors) ToBook(
            OpenLibraryWorkResource work,
            OpenLibraryEditionListResource editions)
        {
            var workKey = ExtractKey(work.Key);

            var primaryEdition = SelectPrimaryEdition(editions);
            var editionList = (editions?.Entries ?? new List<OpenLibraryEditionResource>())
                .Select(OpenLibraryEditionMapper.ToEdition)
                .ToList();

            // Mark the primary edition Monitored=true, all others false.
            if (editionList.Any())
            {
                var primaryKey = primaryEdition != null ? ExtractKey(primaryEdition.Key) : editionList.First().ForeignEditionId;
                foreach (var e in editionList)
                {
                    e.Monitored = e.ForeignEditionId == primaryKey;
                }

                // Work-level covers serve two roles here:
                //   1. PRIMARY: prepend work.covers[0] onto the monitored
                //      edition's Images list so the displayed cover
                //      matches OL's own editorial pick (the cover OL
                //      shows on the /works/<id> page), not whichever
                //      cover-bearing edition happened to be first in
                //      OL's response order. This is the user-visible
                //      "canonical" default; users override via the
                //      cover-picker modal (PreferredCoverUrl).
                //   2. BACKFILL: editions whose JSON has no cover_i at
                //      all still get the work covers list so the modal
                //      has something to show.
                var workCovers = OpenLibraryCoverUrls.ForBook(work.Covers);
                if (workCovers.Count > 0)
                {
                    foreach (var e in editionList)
                    {
                        if (e.Monitored)
                        {
                            // Prepend canonical work cover; keep existing
                            // edition-cover URLs after it so MediaCoverProxy
                            // still has fallback URLs if the canonical
                            // pick 404s.
                            e.Images = workCovers.Take(1).Concat(e.Images).ToList();
                        }
                        else if (e.Images.Count == 0)
                        {
                            e.Images = workCovers;
                        }
                    }
                }
            }

            var book = new Book
            {
                ForeignBookId = workKey,
                ForeignEditionId = editionList.FirstOrDefault(e => e.Monitored)?.ForeignEditionId,
                TitleSlug = workKey,
                Title = work.Title,
                CleanTitle = Parser.Parser.CleanAuthorName(work.Title),
                ReleaseDate = OpenLibraryDateParser.Parse(work.FirstPublishDate) ?? primaryEdition?.PublishDate.AsNullableDate(),
                Genres = work.Subjects?.Take(10).ToList() ?? new List<string>(),
                AnyEditionOk = true,
                Editions = editionList
            };

            // Each author link is just "/authors/OL...A" — we don't have
            // full author records yet. Surface stub AuthorMetadata so the
            // Book.AuthorMetadataId join can resolve once /authors/{key}.json
            // is fetched on the per-author refresh path.
            var authors = (work.Authors ?? new List<OpenLibraryAuthorLink>())
                .Select(a => a.Author?.Key)
                .Where(k => k.IsNotNullOrWhiteSpace())
                .Select(k => new AuthorMetadata { ForeignAuthorId = ExtractKey(k), TitleSlug = ExtractKey(k), Name = string.Empty })
                .ToList();

            // Attach the primary author to the book itself. Callers that hand a
            // freshly-mapped book to import identification (rather than adding it
            // to the DB first) get no lazy-load, so Book.Author and
            // Book.AuthorMetadata would stay null and dereference downstream.
            // ForeignAuthorId is enough to match on; the display name is filled in
            // by the per-author /authors/{key}.json refresh. Name is empty rather
            // than null so downstream string handling matches the stub
            // CandidateService.ToCandidates builds for the same situation.
            var primaryAuthor = authors.FirstOrDefault();
            if (primaryAuthor != null)
            {
                book.AuthorMetadata = primaryAuthor;
                book.Author = new Author { Metadata = primaryAuthor, CleanName = string.Empty };
            }

            return (book, authors);
        }

        private static OpenLibraryEditionResource SelectPrimaryEdition(OpenLibraryEditionListResource editions)
        {
            if (editions?.Entries == null || editions.Entries.Count == 0)
            {
                return null;
            }

            // Preference order: English + ISBN-13 + cover → English + cover → any
            // with cover → English + ISBN-13 → English → has ISBN-13 → first.
            // Pulling cover-bearing editions ahead of the language/ISBN tiers
            // dramatically improves the % of library books that surface with
            // a tile image. The ISBN tier still matters because OL's
            // /b/isbn/<n>-L.jpg endpoint succeeds for many editions whose JSON
            // omits the `covers` field — see the Images-with-ISBN fallback in
            // OpenLibraryEditionMapper.ToEdition.
            //
            // Within each tier, OrderByDescending(Richness) prefers the
            // edition with the most populated downstream metadata fields
            // (publisher, page count, format, publish date, description)
            // rather than whichever one OL happened to return first. Cycle 3
            // of the Le Guin completeness loop: format/page_count/isbn_13/
            // release_date were all hovering ~20-39 short of ceiling not
            // because the data was missing on OL, but because the mapper
            // was tied to OL's response order and that order is not
            // metadata-ranked.
            return PickRichest(editions.Entries.Where(e => IsEnglishWithIsbn13(e) && HasCover(e)))
                ?? PickRichest(editions.Entries.Where(e => IsEnglish(e) && HasCover(e)))
                ?? PickRichest(editions.Entries.Where(HasCover))
                ?? PickRichest(editions.Entries.Where(IsEnglishWithIsbn13))
                ?? PickRichest(editions.Entries.Where(IsEnglish))
                ?? PickRichest(editions.Entries.Where(e => e.Isbn13?.Any() == true))
                ?? editions.Entries[0];
        }

        private static OpenLibraryEditionResource PickRichest(System.Collections.Generic.IEnumerable<OpenLibraryEditionResource> candidates)
        {
            // Tie-stable on the original OL response order: OrderByDescending
            // is stable, so when Richness ties (very common for sibling
            // reprints sharing the same publisher/format/page count), the
            // earlier-listed edition still wins.
            return candidates.OrderByDescending(Richness).FirstOrDefault();
        }

        private static int Richness(OpenLibraryEditionResource e)
        {
            // One point per non-empty downstream metadata field. Cover and
            // language are already gated by the tier predicates, so they
            // don't contribute to within-tier ordering.
            var score = 0;
            if (e.PhysicalFormat.IsNotNullOrWhiteSpace())
            {
                score++;
            }

            if ((e.NumberOfPages ?? 0) > 0)
            {
                score++;
            }

            if (e.PublishDate.IsNotNullOrWhiteSpace())
            {
                score++;
            }

            if (e.Publishers?.Any() == true)
            {
                score++;
            }

            if (e.Description.IsNotNullOrWhiteSpace())
            {
                score++;
            }

            if (e.Isbn13?.Any() == true)
            {
                score++;
            }

            return score;
        }

        private static bool IsEnglish(OpenLibraryEditionResource e) =>
            e.Languages?.Any(l => l.Key != null && l.Key.EndsWith("/eng", System.StringComparison.OrdinalIgnoreCase)) == true;

        private static bool IsEnglishWithIsbn13(OpenLibraryEditionResource e) =>
            IsEnglish(e) && e.Isbn13?.Any() == true;

        // Strictly: only true when OL's JSON says the edition has a real
        // cover_i image. The ISBN fallback path (b/isbn/<n>-L.jpg) is
        // surprisingly often 404 — OL only resolves that endpoint when
        // someone has uploaded the ISBN→cover mapping — so trusting
        // "has ISBN-13" as a cover proxy meant SelectPrimaryEdition kept
        // picking coverless editions over siblings that had real cover_i
        // entries. Concrete repro: work OL16796166W (Casual Vacancy)
        // → mapper picked OL34507766M (Covers=null, isbn 9781306761017
        // → 404) instead of OL28783445M (cover_i=10716309).
        private static bool HasCover(OpenLibraryEditionResource e) =>
            e.Covers?.Any(c => c > 0) == true;

        private static string ExtractKey(string olKey)
        {
            if (olKey.IsNullOrWhiteSpace())
            {
                return olKey;
            }

            var slash = olKey.LastIndexOf('/');
            return slash >= 0 ? olKey.Substring(slash + 1) : olKey;
        }
    }

    internal static class OpenLibraryDateExtensions
    {
        public static System.DateTime? AsNullableDate(this string raw) => OpenLibraryDateParser.Parse(raw);
    }
}
