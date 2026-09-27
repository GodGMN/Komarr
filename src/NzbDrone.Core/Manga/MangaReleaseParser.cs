using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Manga
{
    public interface IMangaReleaseParser
    {
        ParsedMangaReleaseInfo Parse(string releaseTitle);
    }

    // Deliberately conservative: unresolved titles must go to manual review.
    public class MangaReleaseParser : IMangaReleaseParser
    {
        private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex VolumeToken = New(@"(?<![\p{L}\p{N}])(?:vol(?:ume)?s?\.?|v)\s*(?<start>\d{1,4}(?:\.\d+)?)(?:\s*[-~–—]\s*(?:(?:vol(?:ume)?s?\.?|v)\s*)?(?<end>\d{1,4}(?:\.\d+)?))?(?![\p{L}\p{N}])");
        private static readonly Regex ChapterToken = New(@"(?<![\p{L}\p{N}])(?:chapters?\.?|ch\.?|c)\s*(?<start>\d{1,4}(?:\.\d+)?|extra[- ]?\d+)(?:\s*[-~–—]\s*(?:(?:chapters?\.?|ch\.?|c)\s*)?(?<end>\d{1,4}(?:\.\d+)?|extra[- ]?\d+))?(?![\p{L}\p{N}])");
        private static readonly Regex LeadingGroup = New(@"^\s*\[(?<group>[^\]]+)\]\s*");
        private static readonly Regex TrailingCompilationRange = New(@"\s+\d{2,4}\s*[-~]\s*\d{2,4}(?:\.\d+)?\s+as\s*$");
        private static readonly Regex BareNumber = New(@"(?<![\p{L}\p{N}])\d{1,4}(?:\.\d+)?(?:\s*[-~]\s*\d{1,4}(?:\.\d+)?)?(?![\p{L}\p{N}])");
        private static readonly Regex YearToken = New(@"(?<!\d)(?<year>(?:19|20)\d{2})(?!\d)");
        private static readonly Regex NonManga = New(@"\baudiobook\b|\blight novel\b|\bweb novel\b|\bnovel\b|\bj-novel club\b|\[(?:LN|magazine|Seven Seas|Hanashi Media|MTBBooks|Yen Press|Kobo|mr-moon|dingMan)\]|\(WN\)|\bfix\b");
        private static readonly Regex GenericBundle = New(@"\b(?:weekly|monthly)\b.*\b(?:updates?|dump)\b|\b(?:magazine|textbooks?)\b");
        private static readonly Regex PackMarker = New(@"\b(?:batch|pack|collection|complete|completed)\b");
        private static readonly Regex CompleteMarker = New(@"\b(?:complete|completed)\b");
        private static readonly Regex DisjointRange = New(@"\s*,\s*(?:(?:v|vol|ch|c)\s*)?\d{1,4}\s*[-~]\s*\d{1,4}");
        private static readonly Regex JoinedMixedUnits = New(@"(?:v|vol)\d{1,4}c\d{1,4}");
        private static readonly Regex ChapterCue = New(@"\bchapters?\b");
        private static readonly Regex AdditionalCoverage = New(@"\+\s*extras?\b");
        private static readonly Regex BatchSuffix = New(@"\s+batch\s*$");
        private static readonly Regex AmbiguousGroup = New(@"^(?:batch|digital|english|japanese|raw|scanlation)$");
        private static readonly Regex AmbiguousTitle = New(@"\b(?:pack|bundle|dump)\b");
        private static readonly Regex FileExtension = New(@"\.(?:cbz|cbr|pdf|zip|rar|7z)$");
        private static readonly Regex LanguageTag = New(@"(?:\[|\()(?<language>English|Japanese|Spanish|French|German|Italian|Portuguese|Korean|Chinese)(?:\]|\))");
        private static readonly Regex DigitalSource = New(@"\bdigital\b");
        private static readonly Regex ScanlationSource = New(@"\bscanlat(?:ion|ed)\b");
        private static readonly Regex RawSource = New(@"(?:\[|\()raw(?:\]|\))");

        public ParsedMangaReleaseInfo Parse(string releaseTitle)
        {
            var result = new ParsedMangaReleaseInfo { RawTitle = releaseTitle };
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                result.Warnings.Add("Release title is empty.");
                return result;
            }

            if (releaseTitle.Length > 1024)
            {
                result.Warnings.Add("Release title is too long to parse safely.");
                return result;
            }

            var name = StripFileExtension(releaseTitle.Trim());
            result.IsComplete = CompleteMarker.IsMatch(name);
            result.IsPack = result.IsComplete || PackMarker.IsMatch(name);
            result.EditionHint = FindEdition(name);
            result.Language = FindLanguage(name);
            result.Source = FindSource(name);
            result.Year = FindYear(name);

            if (NonManga.IsMatch(name))
            {
                result.Warnings.Add("Release identifies a non-manga or format-ambiguous source.");
                return result;
            }

            if (GenericBundle.IsMatch(name))
            {
                result.Warnings.Add("Release is a multi-title update or bundle.");
                return result;
            }

            if (JoinedMixedUnits.IsMatch(name))
            {
                result.Warnings.Add("Volume and chapter tokens are joined in one ambiguous label.");
                return result;
            }

            var volumeMatches = VolumeToken.Matches(name).Cast<Match>().Where(match => !InsideSquareBrackets(name, match.Index)).ToList();
            var chapterMatches = ChapterToken.Matches(name).Cast<Match>().Where(match => !InsideSquareBrackets(name, match.Index)).ToList();

            if (volumeMatches.Count + chapterMatches.Count != 1)
            {
                result.Warnings.Add(volumeMatches.Count + chapterMatches.Count == 0
                    ? BareNumber.IsMatch(name) ? "Bare numbers have no explicit volume or chapter token." : "No volume or chapter token was found."
                    : "Conflicting or repeated volume and chapter tokens were found.");
                return result;
            }

            var match = volumeMatches.SingleOrDefault() ?? chapterMatches.Single();
            var unit = volumeMatches.Count == 1 ? MangaReleaseUnitType.Volume : MangaReleaseUnitType.Chapter;
            var suffix = name.Substring(match.Index + match.Length);
            var prefixBeforeToken = name.Substring(0, match.Index);
            if ((BareNumber.IsMatch(prefixBeforeToken) && prefixBeforeToken.Contains('+')) ||
                (unit == MangaReleaseUnitType.Volume && suffix.StartsWith("c", StringComparison.OrdinalIgnoreCase) && suffix.Length > 1 && char.IsDigit(suffix[1])) ||
                AdditionalCoverage.IsMatch(suffix))
            {
                result.Warnings.Add("Release contains additional coverage or extras outside the parsed interval.");
                return result;
            }

            if (DisjointRange.IsMatch(suffix))
            {
                result.Warnings.Add("Disjoint ranges cannot be represented as one coverage interval.");
                return result;
            }

            if (unit == MangaReleaseUnitType.Volume && ChapterCue.IsMatch(suffix))
            {
                result.Warnings.Add("Volume and chapter cues conflict.");
                return result;
            }

            var prefix = name.Substring(0, match.Index);
            var group = LeadingGroup.Match(prefix);
            if (group.Success)
            {
                var candidate = group.Groups["group"].Value;
                if (!AmbiguousGroup.IsMatch(candidate))
                {
                    result.ReleaseGroup = candidate;
                }

                prefix = prefix.Substring(group.Length);
            }

            prefix = TrailingCompilationRange.Replace(prefix, string.Empty);
            prefix = BatchSuffix.Replace(prefix, string.Empty);
            var title = prefix.Trim().TrimEnd(' ', '-', '_', ':', '(').Trim();
            if (title.Length < 2 || title.StartsWith("_", StringComparison.Ordinal) ||
                (AmbiguousTitle.IsMatch(title) && title.IndexOf('(') >= 0))
            {
                result.Warnings.Add("A reliable manga title could not be isolated.");
                return result;
            }

            var startText = match.Groups["start"].Value;
            var endText = match.Groups["end"].Success ? match.Groups["end"].Value : startText;
            var start = ParseNumber(startText);
            var end = ParseNumber(endText);
            if (start.HasValue != end.HasValue ||
                (start.HasValue && (start > end || end - start > (unit == MangaReleaseUnitType.Volume ? 500 : 2000))))
            {
                result.Warnings.Add("Release range is inconsistent or implausibly wide.");
                return result;
            }

            result.ParsedTitle = title;
            result.UnitType = unit;
            result.StartNumberText = startText;
            result.EndNumberText = endText;
            result.StartNumber = start;
            result.EndNumber = end;
            result.IsPack |= startText != endText;
            result.Confidence = start.HasValue ? MangaParseConfidence.High : MangaParseConfidence.Medium;
            return result;
        }

        private static Regex New(string pattern)
        {
            return new Regex(pattern, Options, RegexTimeout);
        }

        private static decimal? ParseNumber(string text)
        {
            return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        private static bool InsideSquareBrackets(string text, int index)
        {
            return text.LastIndexOf('[', Math.Max(0, index)) > text.LastIndexOf(']', Math.Max(0, index));
        }

        private static string StripFileExtension(string title)
        {
            return FileExtension.Replace(title, string.Empty);
        }

        private static string FindEdition(string title)
        {
            foreach (var value in new[] { "Master Edition", "Perfect Edition", "Collectors Edition", "Omnibus", "Deluxe", "Official" })
            {
                if (title.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return value;
                }
            }

            return null;
        }

        private static string FindLanguage(string title)
        {
            var match = LanguageTag.Match(title);
            var value = match.Groups["language"].Value;
            return match.Success ? char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant() : null;
        }

        private static string FindSource(string title)
        {
            if (DigitalSource.IsMatch(title))
            {
                return "Digital";
            }

            if (ScanlationSource.IsMatch(title))
            {
                return "Scanlation";
            }

            return RawSource.IsMatch(title) ? "Raw" : null;
        }

        private static int? FindYear(string title)
        {
            var year = YearToken.Match(title);
            return year.Success && int.TryParse(year.Groups["year"].Value, out var value) ? value : null;
        }
    }
}
