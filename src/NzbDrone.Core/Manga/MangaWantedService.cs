using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Manga
{
    public class MangaWantedItem
    {
        public int MangaId { get; set; }
        public string MangaTitle { get; set; }
        public string CoverUrl { get; set; }
        public int ItemId { get; set; }
        public MangaItemType Type { get; set; }
        public string NumberText { get; set; }
        public decimal? NumberDecimal { get; set; }
        public bool Monitored { get; set; }
        public bool Owned { get; set; }
        public bool InProgress { get; set; }
        public bool Missing => Monitored && !Owned;
    }

    public interface IMangaWantedService
    {
        List<MangaWantedItem> GetForManga(int mangaId);
        List<MangaWantedItem> GetMissing();
    }

    public class MangaWantedService : IMangaWantedService
    {
        private readonly IMangaService _manga;
        private readonly IMangaFileItemRepository _coverage;
        private readonly IMangaDownloadRepository _downloads;

        public MangaWantedService(IMangaService manga, IMangaFileItemRepository coverage, IMangaDownloadRepository downloads)
        {
            _manga = manga;
            _coverage = coverage;
            _downloads = downloads;
        }

        public List<MangaWantedItem> GetForManga(int mangaId)
        {
            var manga = _manga.Find(mangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            return GetForManga(manga);
        }

        public List<MangaWantedItem> GetMissing()
        {
            return _manga.All().Where(manga => manga.Monitored)
                .SelectMany(GetForManga)
                .Where(item => item.Missing)
                .OrderBy(item => item.MangaTitle)
                .ThenBy(item => item.Type)
                .ThenBy(item => item.NumberDecimal ?? decimal.MaxValue)
                .ThenBy(item => item.NumberText)
                .ToList();
        }

        private List<MangaWantedItem> GetForManga(Manga manga)
        {
            var type = manga.TrackingMode == MangaTrackingMode.Volume ? MangaItemType.Volume : MangaItemType.Chapter;
            var items = _manga.GetItems(manga.Id).Where(item => item.Type == type).ToList();
            var fileIds = _manga.GetFiles(manga.Id).Select(file => file.Id).ToList();
            var owned = _coverage.GetByFileIds(fileIds).Select(link => link.MangaItemId).ToHashSet();
            var inProgress = _downloads.GetByMangaId(manga.Id)
                .Where(download => download.Status is MangaDownloadStatus.Pending or MangaDownloadStatus.Sent)
                .SelectMany(download => download.CoveredItemIds)
                .ToHashSet();
            return items.Select(item => new MangaWantedItem
            {
                MangaId = manga.Id,
                MangaTitle = manga.PreferredTitle ?? manga.TitleRomaji,
                CoverUrl = manga.CoverUrl,
                ItemId = item.Id,
                Type = item.Type,
                NumberText = item.NumberText,
                NumberDecimal = item.NumberDecimal,
                Monitored = manga.Monitored && item.Monitored,
                Owned = owned.Contains(item.Id),
                InProgress = inProgress.Contains(item.Id)
            }).OrderBy(item => item.NumberDecimal ?? decimal.MaxValue).ThenBy(item => item.NumberText).ToList();
        }
    }
}
