using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public class MangaReleaseDiscoveryService : IHandle<MangaRssSyncCompleteEvent>
    {
        private const string UnknownCoverageReason = "No known manga item falls inside the parsed release coverage.";
        private readonly IMangaItemRepository _items;
        private readonly Logger _logger;

        public MangaReleaseDiscoveryService(IMangaItemRepository items, Logger logger)
        {
            _items = items;
            _logger = logger;
        }

        public void Handle(MangaRssSyncCompleteEvent message)
        {
            var knownByManga = new Dictionary<int, List<MangaItem>>();
            var added = 0;
            foreach (var evaluation in message.Result.Evaluations)
            {
                if (!CanDiscover(evaluation))
                {
                    continue;
                }

                var manga = evaluation.Candidate.Manga;
                var type = manga.TrackingMode == MangaTrackingMode.Volume ? MangaItemType.Volume : MangaItemType.Chapter;
                if (!knownByManga.TryGetValue(manga.Id, out var known))
                {
                    known = _items.GetByMangaId(manga.Id).Where(item => item.Type == type).ToList();
                    knownByManga.Add(manga.Id, known);
                }

                var start = evaluation.Parsed.StartNumber.Value;
                var end = evaluation.Parsed.EndNumber.Value;
                var latest = known.Where(item => item.NumberDecimal.HasValue).Select(item => item.NumberDecimal.Value)
                    .DefaultIfEmpty(0).Max();
                if (start > latest + 1 || end > latest + 5)
                {
                    continue;
                }

                for (var number = start; number <= end; number++)
                {
                    if (known.Any(item => item.NumberDecimal == number))
                    {
                        continue;
                    }

                    var item = new MangaItem
                    {
                        MangaId = manga.Id,
                        Type = type,
                        Monitored = true,
                        DiscoveredFrom = MangaItemDiscoverySource.Release,
                        ReleaseDate = evaluation.Release.PublishDate == default ? null : evaluation.Release.PublishDate,
                        Added = DateTime.UtcNow
                    };
                    item.SetNumber(number.ToString(CultureInfo.InvariantCulture));
                    known.Add(_items.Insert(item));
                    added++;
                }
            }

            if (added > 0)
            {
                _logger.Info("Discovered {0} future manga item(s) from confident recent releases", added);
            }
        }

        private static bool CanDiscover(MangaRssReleaseEvaluation evaluation)
        {
            var manga = evaluation?.Candidate?.Manga;
            var parsed = evaluation?.Parsed;
            var decision = evaluation?.Decision;
            if (manga == null || parsed == null || decision == null ||
                !manga.Monitored || !manga.MonitorFutureItems ||
                !string.Equals(manga.Status, "RELEASING", StringComparison.OrdinalIgnoreCase) ||
                !evaluation.Candidate.CanAutoMatch || evaluation.Match.IsAmbiguous ||
                parsed.Confidence != MangaParseConfidence.High ||
                !parsed.StartNumber.HasValue || !parsed.EndNumber.HasValue ||
                parsed.StartNumber <= 0 || parsed.EndNumber - parsed.StartNumber > 4 ||
                (parsed.StartNumber != parsed.EndNumber &&
                    (parsed.StartNumber != decimal.Truncate(parsed.StartNumber.Value) ||
                        parsed.EndNumber != decimal.Truncate(parsed.EndNumber.Value))) ||
                !string.IsNullOrWhiteSpace(parsed.EditionHint) ||
                decision.Rejections.Count > 0 ||
                decision.ReviewReasons.Any(reason => reason != UnknownCoverageReason))
            {
                return false;
            }

            return parsed.UnitType == (manga.TrackingMode == MangaTrackingMode.Volume ? MangaReleaseUnitType.Volume : MangaReleaseUnitType.Chapter);
        }
    }
}
