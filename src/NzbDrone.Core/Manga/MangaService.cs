using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MetadataSource.AniList;

namespace NzbDrone.Core.Manga
{
    public class MangaAddOptions
    {
        public int AniListId { get; set; }
        public string RootFolderPath { get; set; }
        public string Path { get; set; }
        public string PreferredTitle { get; set; }
        public MangaTrackingMode TrackingMode { get; set; } = MangaTrackingMode.Volume;
        public bool Monitored { get; set; } = true;
        public bool MonitorFutureItems { get; set; } = true;
        public int QualityProfileId { get; set; }
        public int? MetadataProfileId { get; set; }
        public List<int> Tags { get; set; } = new ();
        public List<string> UserAliases { get; set; }
        public MangaReleasePolicy QualityPolicy { get; set; }
    }

    public class MangaItemAddOptions
    {
        public string NumberText { get; set; }
        public string Title { get; set; }
        public bool Monitored { get; set; } = true;
    }

    public class MangaMetadataException : Exception
    {
        public MangaMetadataException(string message, AniListAvailability availability)
            : base(message)
        {
            Availability = availability;
        }

        public AniListAvailability Availability { get; }
    }

    public interface IMangaService
    {
        IEnumerable<Manga> All();
        Manga Find(int id);
        Manga FindByAniListId(int id);
        Manga Add(MangaAddOptions options);
        Manga Update(int id, MangaAddOptions options);
        Manga Refresh(int id);
        void Delete(int id);
        IEnumerable<MangaItem> GetItems(int mangaId);
        MangaItem AddItem(int mangaId, MangaItemAddOptions options);
        MangaItem SetItemMonitored(int mangaId, int itemId, bool monitored);
        void MarkSearched(int mangaId);
        Manga SetQualityPolicy(int mangaId, MangaReleasePolicy policy);
        IEnumerable<MangaFile> GetFiles(int mangaId);
    }

    public class MangaService : IMangaService
    {
        private readonly IMangaRepository _repository;
        private readonly IMangaItemRepository _items;
        private readonly IMangaFileRepository _files;
        private readonly IAniListMetadataClient _metadata;

        public MangaService(IMangaRepository repository, IMangaItemRepository items, IMangaFileRepository files, IAniListMetadataClient metadata)
        {
            _repository = repository;
            _items = items;
            _files = files;
            _metadata = metadata;
        }

        public IEnumerable<Manga> All() => _repository.All();

        public Manga Find(int id) => _repository.Find(id);

        public Manga FindByAniListId(int id) => _repository.FindByAniListId(id);

        public IEnumerable<MangaItem> GetItems(int mangaId) => _items.GetByMangaId(mangaId);

        public MangaItem AddItem(int mangaId, MangaItemAddOptions options)
        {
            var manga = Find(mangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            if (options == null || string.IsNullOrWhiteSpace(options.NumberText))
            {
                throw new ArgumentException("Enter a volume or chapter number.");
            }

            var type = manga.TrackingMode == MangaTrackingMode.Volume ? MangaItemType.Volume : MangaItemType.Chapter;
            var item = new MangaItem
            {
                MangaId = mangaId,
                Type = type,
                Title = options.Title?.Trim(),
                Monitored = options.Monitored,
                DiscoveredFrom = MangaItemDiscoverySource.Manual,
                Added = DateTime.UtcNow
            };
            item.SetNumber(options.NumberText);
            var existing = _items.GetByMangaId(mangaId).FirstOrDefault(existing => existing.Type == type &&
                (string.Equals(existing.NumberText, item.NumberText, StringComparison.OrdinalIgnoreCase) ||
                    (existing.NumberDecimal.HasValue && existing.NumberDecimal == item.NumberDecimal)));
            if (existing != null)
            {
                if (existing.DiscoveredFrom == MangaItemDiscoverySource.Metadata &&
                    !string.Equals(manga.Status, "FINISHED", StringComparison.OrdinalIgnoreCase))
                {
                    existing.DiscoveredFrom = MangaItemDiscoverySource.Manual;
                    existing.Monitored = options.Monitored;
                    existing.Title = item.Title;
                    return _items.Update(existing);
                }

                throw new InvalidOperationException("This manga item is already known.");
            }

            return _items.Insert(item);
        }

        public MangaItem SetItemMonitored(int mangaId, int itemId, bool monitored)
        {
            if (Find(mangaId) == null)
            {
                throw new KeyNotFoundException("Manga was not found.");
            }

            var item = _items.Find(itemId);
            if (item?.MangaId != mangaId)
            {
                throw new KeyNotFoundException("Manga item was not found.");
            }

            item.Monitored = monitored;
            return _items.Update(item);
        }

        public void MarkSearched(int mangaId)
        {
            var manga = Find(mangaId);
            if (manga != null)
            {
                manga.LastSearchTime = DateTime.UtcNow;
                _repository.Update(manga);
            }
        }

        public Manga SetQualityPolicy(int mangaId, MangaReleasePolicy policy)
        {
            var manga = Find(mangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            if (policy == null || policy.SourcePreference == null || policy.SourcePreference.Count > 10 ||
                (!string.IsNullOrWhiteSpace(policy.UpgradeCutoffSource) &&
                    !policy.SourcePreference.Contains(policy.UpgradeCutoffSource, StringComparer.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Choose a source cutoff from the ordered source preference list.");
            }

            manga.QualityPolicy = policy;
            return _repository.Update(manga);
        }

        public IEnumerable<MangaFile> GetFiles(int mangaId) => _files.GetByMangaId(mangaId);

        public Manga Add(MangaAddOptions options)
        {
            if (options == null || options.AniListId <= 0)
            {
                throw new ArgumentException("Select an AniList manga ID before adding a title.");
            }

            if (_repository.FindByAniListId(options.AniListId) != null)
            {
                throw new InvalidOperationException("This AniList manga is already in the library.");
            }

            var result = _metadata.GetById(options.AniListId);
            var media = result.Availability == AniListAvailability.Available
                ? result.Media.SingleOrDefault(x => x.Id == options.AniListId)
                : null;
            if (media == null)
            {
                throw new MangaMetadataException(
                    result.Availability == AniListAvailability.Available
                        ? "AniList did not return the selected manga ID."
                        : result.Message ?? "AniList is unavailable; try adding this manga later.",
                    result.Availability);
            }

            var manga = new Manga
            {
                AniListId = options.AniListId,
                Added = DateTime.UtcNow
            };
            ApplyMetadata(manga, media);
            ApplyOptions(manga, options);
            manga = _repository.Insert(manga);
            SyncKnownItems(manga);
            return manga;
        }

        public Manga Update(int id, MangaAddOptions options)
        {
            var manga = _repository.Find(id) ?? throw new KeyNotFoundException("Manga was not found.");
            if (options == null || options.AniListId != manga.AniListId)
            {
                throw new ArgumentException("AniList identity cannot be changed after adding a manga.");
            }

            ApplyOptions(manga, options);
            return _repository.Update(manga);
        }

        public Manga Refresh(int id)
        {
            var manga = _repository.Find(id) ?? throw new KeyNotFoundException("Manga was not found.");
            var result = _metadata.GetById(manga.AniListId, true);
            if (result.Availability != AniListAvailability.Available)
            {
                // Keep the local record intact even if a stale cache exists.
                return manga;
            }

            var media = result.Media.SingleOrDefault(x => x.Id == manga.AniListId);
            if (media == null)
            {
                return manga;
            }

            ApplyMetadata(manga, media);
            manga = _repository.Update(manga);
            SyncKnownItems(manga);
            return manga;
        }

        public void Delete(int id)
        {
            if (_repository.Find(id) == null)
            {
                throw new KeyNotFoundException("Manga was not found.");
            }

            _repository.Delete(id);
        }

        private static void ApplyOptions(Manga manga, MangaAddOptions options)
        {
            manga.RootFolderPath = options.RootFolderPath;
            manga.Path = options.Path;
            manga.TrackingMode = options.TrackingMode;
            manga.Monitored = options.Monitored;
            manga.MonitorFutureItems = options.MonitorFutureItems;
            manga.QualityProfileId = options.QualityProfileId;
            manga.MetadataProfileId = options.MetadataProfileId;
            manga.Tags = options.Tags?.Distinct().ToList() ?? new List<int>();
            if (options.UserAliases != null)
            {
                manga.UserAliases = options.UserAliases
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Where(x => MangaReleaseMatcher.Normalize(x).Length > 0)
                    .DistinctBy(MangaReleaseMatcher.Normalize)
                    .Take(20)
                    .ToList();
            }

            if (options.QualityPolicy != null)
            {
                manga.QualityPolicy = options.QualityPolicy;
            }

            if (!string.IsNullOrWhiteSpace(options.PreferredTitle))
            {
                manga.PreferredTitle = options.PreferredTitle.Trim();
            }
        }

        private static void ApplyMetadata(Manga manga, AniListMedia media)
        {
            manga.MalId = media.MalId;
            manga.TitleRomaji = media.TitleRomaji ?? media.TitleEnglish ?? media.TitleNative ?? $"AniList #{media.Id}";
            manga.TitleEnglish = media.TitleEnglish;
            manga.TitleNative = media.TitleNative;
            manga.PreferredTitle ??= media.TitleEnglish ?? media.TitleRomaji ?? media.TitleNative;
            manga.CleanTitle = (manga.PreferredTitle ?? string.Empty).Trim().ToLowerInvariant();
            manga.Synonyms = media.Synonyms ?? new List<string>();
            manga.Description = media.Description;
            manga.Status = media.Status;
            manga.Format = media.Format;
            manga.CountryOfOrigin = media.CountryOfOrigin;
            manga.StartDate = media.StartDate;
            manga.EndDate = media.EndDate;
            manga.AniListChapterCount = media.Chapters;
            manga.AniListVolumeCount = media.Volumes;
            manga.CoverUrl = media.CoverUrl;
            manga.BannerUrl = media.BannerUrl;
            manga.AniListUpdatedAt = media.UpdatedAt;
            manga.LastInfoSync = DateTime.UtcNow;
        }

        private void SyncKnownItems(Manga manga)
        {
            if (!string.Equals(manga.Status, "FINISHED", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var count = manga.TrackingMode == MangaTrackingMode.Volume ? manga.AniListVolumeCount : manga.AniListChapterCount;
            var limit = manga.TrackingMode == MangaTrackingMode.Volume ? 500 : 2000;
            if (!count.HasValue || count.Value <= 0 || count.Value > limit)
            {
                return;
            }

            var type = manga.TrackingMode == MangaTrackingMode.Volume ? MangaItemType.Volume : MangaItemType.Chapter;
            var existing = (_items.GetByMangaId(manga.Id) ?? Enumerable.Empty<MangaItem>())
                .Where(item => item.Type == type && item.NumberDecimal.HasValue && item.NumberDecimal.Value >= 1 &&
                    item.NumberDecimal.Value <= limit && item.NumberDecimal.Value == decimal.Truncate(item.NumberDecimal.Value))
                .Select(item => (int)item.NumberDecimal.Value)
                .ToHashSet();
            for (var number = 1; number <= count.Value; number++)
            {
                if (existing.Contains(number))
                {
                    continue;
                }

                var item = new MangaItem
                {
                    MangaId = manga.Id,
                    Type = type,
                    Monitored = manga.Monitored,
                    DiscoveredFrom = MangaItemDiscoverySource.Metadata,
                    Added = DateTime.UtcNow
                };
                item.SetNumber(number.ToString());
                _items.Insert(item);
            }
        }
    }
}
