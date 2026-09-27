using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga
{
    public class MangaAutomaticAcquisitionService : IHandle<MangaRssSyncCompleteEvent>, IExecute<MangaSearchMissingCommand>
    {
        private readonly IMangaService _manga;
        private readonly IMangaWantedService _wanted;
        private readonly IMangaIndexerSearchService _indexers;
        private readonly IMangaRssSyncService _rss;
        private readonly IMangaGrabService _grab;
        private readonly Logger _logger;

        public MangaAutomaticAcquisitionService(
            IMangaService manga,
            IMangaWantedService wanted,
            IMangaIndexerSearchService indexers,
            IMangaRssSyncService rss,
            IMangaGrabService grab,
            Logger logger)
        {
            _manga = manga;
            _wanted = wanted;
            _indexers = indexers;
            _rss = rss;
            _grab = grab;
            _logger = logger;
        }

        public void Handle(MangaRssSyncCompleteEvent message)
        {
            Process(message.Result.Evaluations, "recent feed").GetAwaiter().GetResult();
        }

        public void Execute(MangaSearchMissingCommand message)
        {
            SearchMissing().GetAwaiter().GetResult();
        }

        private async Task SearchMissing()
        {
            var candidates = _manga.All().Where(manga => manga.Monitored)
                .OrderBy(manga => manga.LastSearchTime ?? DateTime.MinValue)
                .ThenBy(manga => manga.Id)
                .Take(100)
                .ToList();
            var selected = new List<Manga>();
            foreach (var candidate in candidates)
            {
                try
                {
                    if (_wanted.GetForManga(candidate.Id).Any(item => item.Missing && !item.InProgress))
                    {
                        selected.Add(candidate);
                    }
                    else
                    {
                        _manga.MarkSearched(candidate.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Could not check wanted manga {0}: {1}", candidate.Id, ex.GetType().Name);
                    _manga.MarkSearched(candidate.Id);
                }

                if (selected.Count == 5)
                {
                    break;
                }
            }

            foreach (var manga in selected)
            {
                try
                {
                    var search = await _indexers.Search(manga, false);
                    foreach (var failure in search.IndexerErrors)
                    {
                        _logger.Warn("Automatic manga search for {0}: indexer {1} failed: {2}", manga.PreferredTitle, failure.Key, failure.Value);
                    }

                    var evaluated = _rss.Process(search.Releases.Take(100));
                    await Process(evaluated.Evaluations.Where(value => value.Candidate.Manga.Id == manga.Id), "automatic search");
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Automatic manga search failed for {0}", manga.PreferredTitle);
                }
                finally
                {
                    _manga.MarkSearched(manga.Id);
                }
            }
        }

        private async Task Process(IEnumerable<MangaRssReleaseEvaluation> evaluations, string source)
        {
            var wantedByManga = new Dictionary<int, List<MangaWantedItem>>();
            foreach (var evaluation in evaluations
                .OrderBy(value => value.Decision.IsUpgrade ? 1 : 0)
                .ThenBy(value => value.Release.IndexerPriority)
                .ThenByDescending(value => TorrentInfo.GetSeeders(value.Release) ?? -1)
                .ThenByDescending(value => value.Release.PublishDate))
            {
                var manga = evaluation.Candidate.Manga;
                var covered = evaluation.Candidate.CoveredItems.Select(item => item.Id).ToList();
                if (!wantedByManga.TryGetValue(manga.Id, out var wanted))
                {
                    wanted = _wanted.GetForManga(manga.Id);
                    wantedByManga.Add(manga.Id, wanted);
                }

                var missing = wanted.Where(item => item.Missing && !item.InProgress).Select(item => item.ItemId).ToHashSet();
                var upgradeable = evaluation.Decision.IsUpgrade && covered.All(id => wanted.Any(item =>
                    item.ItemId == id && item.Owned && item.Monitored && !item.InProgress));
                var reason = evaluation.Decision.Rejections.Concat(evaluation.Decision.ReviewReasons).FirstOrDefault();
                if (!evaluation.Decision.CanGrabAutomatically || covered.Count == 0 ||
                    (!upgradeable && covered.Any(id => !missing.Contains(id))))
                {
                    reason ??= covered.Count == 0 ? "No known wanted item is covered." :
                        "Coverage includes an owned, unmonitored, or already downloading item.";
                    _logger.Info("Skipped manga {0} from {1}: {2} ({3})", evaluation.Release.Title, source, reason, manga.PreferredTitle);
                    continue;
                }

                try
                {
                    _logger.Info("Selected manga {0} from {1}: exact unambiguous title, monitored missing coverage, and quality checks passed ({2})",
                        evaluation.Release.Title,
                        source,
                        manga.PreferredTitle);
                    await _grab.GrabAutomatic(manga.Id, evaluation.Release);
                    foreach (var id in covered)
                    {
                        var state = wanted.FirstOrDefault(item => item.ItemId == id);
                        if (state != null)
                        {
                            state.InProgress = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var failureReason = ex is MangaGrabValidationException ? ex.Message : ex.GetType().Name;
                    _logger.Warn("Skipped manga {0} from {1}: {2} ({3})", evaluation.Release.Title, source, failureReason, manga.PreferredTitle);
                }
            }
        }
    }
}
