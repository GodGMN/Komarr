using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Manga
{
    public enum MangaTitleMatchKind
    {
        ExactPreferred,
        ExactAlias,
        Structured,
        Fuzzy
    }

    public class MangaMatchCandidate
    {
        public Manga Manga { get; set; }
        public string MatchedAlias { get; set; }
        public MangaTitleMatchKind MatchKind { get; set; }
        public double Score { get; set; }
        public List<MangaItem> CoveredItems { get; set; } = new ();
        public bool ContainsRequestedItem { get; set; }
        public bool CanAutoMatch { get; set; }
        public List<string> Evidence { get; set; } = new ();
    }

    public class MangaMatchOutcome
    {
        public ParsedMangaReleaseInfo Release { get; set; }
        public List<MangaMatchCandidate> Candidates { get; set; } = new ();
        public bool IsAmbiguous { get; set; }
        public string Explanation { get; set; }
    }

    public interface IMangaReleaseMatcher
    {
        MangaMatchOutcome Match(ParsedMangaReleaseInfo release, IEnumerable<Manga> manga, IEnumerable<MangaItem> items, int? requestedItemId = null);
    }

    public class MangaReleaseMatcher : IMangaReleaseMatcher
    {
        private class Alias
        {
            public string Title { get; set; }
            public string Normalized { get; set; }
            public bool Preferred { get; set; }
        }

        public MangaMatchOutcome Match(ParsedMangaReleaseInfo release, IEnumerable<Manga> manga, IEnumerable<MangaItem> items, int? requestedItemId = null)
        {
            var outcome = new MangaMatchOutcome { Release = release };
            if (release == null || release.UnitType == MangaReleaseUnitType.Unknown || string.IsNullOrWhiteSpace(release.ParsedTitle))
            {
                outcome.Explanation = "Release title or unit is unresolved; manual identification is required.";
                return outcome;
            }

            var normalizedTitle = Normalize(release.ParsedTitle);
            if (normalizedTitle.Length == 0)
            {
                outcome.Explanation = "Release title has no comparable characters.";
                return outcome;
            }

            var availableItems = items?.ToList() ?? new List<MangaItem>();
            var candidates = new List<MangaMatchCandidate>();
            foreach (var title in manga ?? Enumerable.Empty<Manga>())
            {
                var aliases = GetAliases(title);
                var exact = aliases.FirstOrDefault(x => x.Normalized == normalizedTitle);
                var structured = exact == null ? aliases.FirstOrDefault(x => IsStructured(normalizedTitle, x.Normalized)) : null;
                var fuzzy = exact == null && structured == null
                    ? aliases.Where(x => IsNarrowed(normalizedTitle, x.Normalized))
                        .Select(x => new { Alias = x, Similarity = Similarity(normalizedTitle, x.Normalized) })
                        .OrderByDescending(x => x.Similarity)
                        .FirstOrDefault(x => x.Similarity >= 0.6)
                    : null;
                if (exact == null && structured == null && fuzzy == null)
                {
                    continue;
                }

                var matched = exact ?? structured ?? fuzzy.Alias;
                var kind = exact != null ? matched.Preferred ? MangaTitleMatchKind.ExactPreferred : MangaTitleMatchKind.ExactAlias
                    : structured != null ? MangaTitleMatchKind.Structured : MangaTitleMatchKind.Fuzzy;
                var covered = availableItems.Where(item => item.MangaId == title.Id && Covers(release, item)).ToList();
                var requested = requestedItemId.HasValue && availableItems.Any(item => item.Id == requestedItemId.Value && item.MangaId == title.Id);
                var containsRequested = requested && covered.Any(item => item.Id == requestedItemId.Value);
                var score = kind switch
                {
                    MangaTitleMatchKind.ExactPreferred => 1.0,
                    MangaTitleMatchKind.ExactAlias => 0.98,
                    MangaTitleMatchKind.Structured => 0.75,
                    _ => 0.45 + (fuzzy.Similarity * 0.25)
                };
                var candidate = new MangaMatchCandidate
                {
                    Manga = title,
                    MatchedAlias = matched.Title,
                    MatchKind = kind,
                    Score = score,
                    CoveredItems = covered,
                    ContainsRequestedItem = containsRequested
                };
                candidate.Evidence.Add($"{kind} title match: {matched.Title}");
                candidate.Evidence.Add($"Parsed {release.UnitType.ToString().ToLowerInvariant()} coverage: {release.StartNumberText}–{release.EndNumberText}.");
                candidate.Evidence.Add(covered.Count > 0 ? $"Covers {covered.Count} known item(s)." : "No known items fall inside the parsed coverage.");
                if (requestedItemId.HasValue)
                {
                    candidate.Evidence.Add(containsRequested ? "Contains the requested item." : "Does not contain the requested item.");
                }

                if (release.Language != null)
                {
                    candidate.Evidence.Add($"Release language hint: {release.Language}.");
                }

                if (release.EditionHint != null)
                {
                    candidate.Evidence.Add($"Edition hint: {release.EditionHint}.");
                }

                candidates.Add(candidate);
            }

            var bestKind = candidates.Any(x => x.MatchKind is MangaTitleMatchKind.ExactPreferred or MangaTitleMatchKind.ExactAlias)
                ? "exact"
                : candidates.Any(x => x.MatchKind == MangaTitleMatchKind.Structured) ? "structured" : "fuzzy";
            if (bestKind == "exact")
            {
                candidates = candidates.Where(x => x.MatchKind is MangaTitleMatchKind.ExactPreferred or MangaTitleMatchKind.ExactAlias).ToList();
            }
            else if (bestKind == "structured")
            {
                candidates = candidates.Where(x => x.MatchKind == MangaTitleMatchKind.Structured).ToList();
            }

            outcome.IsAmbiguous = candidates.Count > 1;
            foreach (var candidate in candidates)
            {
                var isExact = candidate.MatchKind == MangaTitleMatchKind.ExactPreferred || candidate.MatchKind == MangaTitleMatchKind.ExactAlias;
                candidate.CanAutoMatch = !outcome.IsAmbiguous &&
                                         isExact &&
                                         release.Confidence == MangaParseConfidence.High &&
                                         (!requestedItemId.HasValue || candidate.ContainsRequestedItem);
                if (outcome.IsAmbiguous)
                {
                    candidate.Evidence.Add("Several manga share this title or alias; select one manually.");
                }
                else if (candidate.MatchKind is MangaTitleMatchKind.Structured or MangaTitleMatchKind.Fuzzy)
                {
                    candidate.Evidence.Add("Non-exact title matches require manual confirmation.");
                }
            }

            outcome.Candidates = candidates.OrderByDescending(x => x.Score).ThenBy(x => x.Manga.AniListId).ToList();
            outcome.Explanation = outcome.IsAmbiguous
                ? "Multiple manga match; automatic acquisition is not permitted."
                : outcome.Candidates.Count == 0 ? "No plausible manga title or alias matched." : "Match evidence is available for review.";
            return outcome;
        }

        public static string Normalize(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var value = title.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            var builder = new StringBuilder(value.Length);
            var previousSeparator = false;
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    previousSeparator = false;
                }
                else if (!previousSeparator && builder.Length > 0 && CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(' ');
                    previousSeparator = true;
                }
            }

            return builder.ToString().Trim();
        }

        private static IEnumerable<Alias> GetAliases(Manga manga)
        {
            var titles = new List<Alias>();
            void Add(string value, bool preferred = false)
            {
                var normalized = Normalize(value);
                if (normalized.Length > 0 && titles.All(x => x.Normalized != normalized))
                {
                    titles.Add(new Alias { Title = value, Normalized = normalized, Preferred = preferred });
                }
            }

            Add(manga.PreferredTitle, true);
            Add(manga.TitleEnglish);
            Add(manga.TitleRomaji);
            Add(manga.TitleNative);
            foreach (var value in manga.UserAliases?.Take(20) ?? Enumerable.Empty<string>())
            {
                Add(value);
            }

            foreach (var value in manga.Synonyms?.Take(20) ?? Enumerable.Empty<string>())
            {
                Add(value);
            }

            return titles;
        }

        public static IEnumerable<string> GetAliasTitles(Manga manga)
        {
            return GetAliases(manga).Select(alias => alias.Title);
        }

        private static bool Covers(ParsedMangaReleaseInfo release, MangaItem item)
        {
            if (item.Type != (release.UnitType == MangaReleaseUnitType.Volume ? MangaItemType.Volume : MangaItemType.Chapter))
            {
                return false;
            }

            if (release.StartNumber.HasValue && release.EndNumber.HasValue && item.NumberDecimal.HasValue)
            {
                return item.NumberDecimal >= release.StartNumber && item.NumberDecimal <= release.EndNumber;
            }

            return string.Equals(item.NumberText, release.StartNumberText, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(release.StartNumberText, release.EndNumberText, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStructured(string title, string alias)
        {
            return title.StartsWith(alias + " ", StringComparison.Ordinal) || alias.StartsWith(title + " ", StringComparison.Ordinal);
        }

        private static bool IsNarrowed(string title, string alias)
        {
            return title.Length >= 4 && alias.Length >= 4 &&
                   Math.Abs(title.Length - alias.Length) <= Math.Max(title.Length, alias.Length) / 2 &&
                   title.Substring(0, 2) == alias.Substring(0, 2);
        }

        private static double Similarity(string left, string right)
        {
            var previous = Enumerable.Range(0, right.Length + 1).ToArray();
            var current = new int[right.Length + 1];
            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= right.Length; j++)
                {
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
                }

                (previous, current) = (current, previous);
            }

            return 1.0 - ((double)previous[right.Length] / Math.Max(left.Length, right.Length));
        }
    }
}
