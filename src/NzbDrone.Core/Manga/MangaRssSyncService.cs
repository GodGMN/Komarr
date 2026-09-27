using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga
{
    public class MangaRssReleaseEvaluation
    {
        public ReleaseInfo Release { get; set; }
        public ParsedMangaReleaseInfo Parsed { get; set; }
        public MangaMatchOutcome Match { get; set; }
        public MangaMatchCandidate Candidate { get; set; }
        public MangaReleaseDecision Decision { get; set; }
    }

    public class MangaRssSyncResult
    {
        public int Reports { get; set; }
        public int Duplicates { get; set; }
        public int Unresolved { get; set; }
        public int TooBroad { get; set; }
        public int Matched { get; set; }
        public int Ambiguous { get; set; }
        public int SafeCandidates { get; set; }
        public List<MangaRssReleaseEvaluation> Evaluations { get; set; } = new ();
    }

    public class MangaRssSyncCompleteEvent : IEvent
    {
        public MangaRssSyncCompleteEvent(MangaRssSyncResult result)
        {
            Result = result;
        }

        public MangaRssSyncResult Result { get; }
    }

    public interface IMangaRssSyncService
    {
        MangaRssSyncResult Process(IEnumerable<ReleaseInfo> releases);
    }

    public class MangaRssSyncService : IMangaRssSyncService
    {
        private readonly IMangaRepository _manga;
        private readonly IMangaItemRepository _items;
        private readonly IMangaFileRepository _files;
        private readonly IMangaFileItemRepository _fileItems;
        private readonly IMangaBlocklistRepository _blocklist;
        private readonly IMangaReleaseParser _parser;
        private readonly IMangaReleaseMatcher _matcher;
        private readonly IMangaReleaseDecisionEngine _decisions;
        private readonly Logger _logger;

        public MangaRssSyncService(IMangaRepository manga,
            IMangaItemRepository items,
            IMangaFileRepository files,
            IMangaFileItemRepository fileItems,
            IMangaBlocklistRepository blocklist,
            IMangaReleaseParser parser,
            IMangaReleaseMatcher matcher,
            IMangaReleaseDecisionEngine decisions,
            Logger logger)
        {
            _manga = manga;
            _items = items;
            _files = files;
            _fileItems = fileItems;
            _blocklist = blocklist;
            _parser = parser;
            _matcher = matcher;
            _decisions = decisions;
            _logger = logger;
        }

        public MangaRssSyncResult Process(IEnumerable<ReleaseInfo> releases)
        {
            var result = new MangaRssSyncResult();
            var library = _manga.All().Where(value => value.Monitored).ToList();
            if (library.Count == 0)
            {
                return result;
            }

            var index = new MangaAliasIndex(library);
            var items = _items.All().GroupBy(value => value.MangaId).ToDictionary(group => group.Key, group => group.ToList());
            var files = _files.All().GroupBy(value => value.MangaId).ToDictionary(group => group.Key, group => group.ToList());
            var fileItems = _fileItems.All().GroupBy(value => value.MangaFileId).ToDictionary(group => group.Key, group => group.ToList());
            var blocked = _blocklist.All().Where(value => !string.IsNullOrWhiteSpace(value.ReleaseGuid))
                .Select(value => (value.MangaId, value.IndexerId, Guid: value.ReleaseGuid.ToUpperInvariant())).ToHashSet();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var release in releases ?? Enumerable.Empty<ReleaseInfo>())
            {
                result.Reports++;
                if (release == null || string.IsNullOrWhiteSpace(release.Title))
                {
                    result.Unresolved++;
                    continue;
                }

                var identity = !string.IsNullOrWhiteSpace(release.Guid) ? release.Guid :
                    !string.IsNullOrWhiteSpace(release.DownloadUrl) ? release.DownloadUrl : $"{release.Title}|{release.Size}";
                if (!seen.Add($"{release.IndexerId}|{identity}"))
                {
                    result.Duplicates++;
                    continue;
                }

                try
                {
                    var parsed = _parser.Parse(release.Title);
                    if (parsed.UnitType == MangaReleaseUnitType.Unknown || string.IsNullOrWhiteSpace(parsed.ParsedTitle))
                    {
                        result.Unresolved++;
                        continue;
                    }

                    var lookup = index.Find(parsed.ParsedTitle);
                    if (lookup.TooBroad)
                    {
                        result.TooBroad++;
                        continue;
                    }

                    if (lookup.Candidates.Count == 0)
                    {
                        continue;
                    }

                    var scopedItems = lookup.Candidates.SelectMany(value => items.GetValueOrDefault(value.Id) ?? new List<MangaItem>()).ToList();
                    var match = _matcher.Match(parsed, lookup.Candidates, scopedItems);
                    if (match.Candidates.Count == 0)
                    {
                        continue;
                    }

                    result.Matched++;
                    if (match.IsAmbiguous)
                    {
                        result.Ambiguous++;
                    }

                    foreach (var candidate in match.Candidates)
                    {
                        var mangaFiles = files.GetValueOrDefault(candidate.Manga.Id) ?? new List<MangaFile>();
                        var links = mangaFiles.SelectMany(value => fileItems.GetValueOrDefault(value.Id) ?? new List<MangaFileItem>());
                        var decision = _decisions.Evaluate(candidate.Manga, match, candidate, release, mangaFiles, links);
                        if (blocked.Contains((candidate.Manga.Id, release.IndexerId, release.Guid?.ToUpperInvariant())) &&
                            !decision.Rejections.Contains("Release is blocklisted."))
                        {
                            decision.Rejections.Add("Release is blocklisted after a failed download.");
                        }

                        if (decision.CanGrabAutomatically)
                        {
                            result.SafeCandidates++;
                        }

                        result.Evaluations.Add(new MangaRssReleaseEvaluation
                        {
                            Release = release,
                            Parsed = parsed,
                            Match = match,
                            Candidate = candidate,
                            Decision = decision
                        });
                    }
                }
                catch (Exception ex)
                {
                    result.Unresolved++;
                    _logger.Warn(ex, "Could not evaluate manga RSS release '{0}'", release.Title);
                }
            }

            return result;
        }
    }
}
