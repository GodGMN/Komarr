using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Manga
{
    public interface IMangaHistoryService
    {
        IEnumerable<MangaHistory> GetByMangaId(int mangaId);
        void Record(MangaDownload download, MangaHistoryEventType eventType, string message, MangaDownloadFile file = null);
    }

    public class MangaHistoryService : IMangaHistoryService
    {
        private readonly IMangaHistoryRepository _repository;

        public MangaHistoryService(IMangaHistoryRepository repository)
        {
            _repository = repository;
        }

        public IEnumerable<MangaHistory> GetByMangaId(int mangaId)
        {
            return _repository.GetByMangaId(mangaId).OrderByDescending(value => value.Date);
        }

        public void Record(MangaDownload download, MangaHistoryEventType eventType, string message, MangaDownloadFile file = null)
        {
            _repository.Insert(new MangaHistory
            {
                MangaId = download.MangaId,
                MangaDownloadId = download.Id > 0 ? download.Id : null,
                MangaDownloadFileId = file?.Id,
                EventType = eventType,
                Date = DateTime.UtcNow,
                Message = message,
                ReleaseTitle = download.ReleaseTitle,
                ReleaseGuid = download.ReleaseGuid,
                IndexerId = download.IndexerId,
                CoveredItemIds = file?.CoveredItemIds?.ToList() ?? download.CoveredItemIds?.ToList() ?? new List<int>()
            });
        }
    }

    public interface IMangaBlocklistService
    {
        IEnumerable<MangaBlocklist> GetByMangaId(int mangaId);
        bool IsBlocked(int mangaId, int indexerId, string releaseGuid);
        void Block(MangaDownload download, string reason);
        bool Unblock(int mangaId, int blockId);
    }

    public class MangaBlocklistService : IMangaBlocklistService
    {
        private readonly IMangaBlocklistRepository _repository;

        public MangaBlocklistService(IMangaBlocklistRepository repository)
        {
            _repository = repository;
        }

        public IEnumerable<MangaBlocklist> GetByMangaId(int mangaId)
        {
            return _repository.GetByMangaId(mangaId).OrderByDescending(value => value.Added);
        }

        public bool IsBlocked(int mangaId, int indexerId, string releaseGuid)
        {
            return !string.IsNullOrWhiteSpace(releaseGuid) && _repository.FindRelease(mangaId, indexerId, releaseGuid) != null;
        }

        public void Block(MangaDownload download, string reason)
        {
            if (string.IsNullOrWhiteSpace(download.ReleaseGuid))
            {
                return;
            }

            var existing = _repository.FindRelease(download.MangaId, download.IndexerId, download.ReleaseGuid);
            if (existing != null)
            {
                existing.Reason = reason;
                _repository.Update(existing);
                return;
            }

            _repository.Insert(new MangaBlocklist
            {
                MangaId = download.MangaId,
                IndexerId = download.IndexerId,
                ReleaseGuid = download.ReleaseGuid,
                ReleaseTitle = download.ReleaseTitle,
                Reason = reason,
                Added = DateTime.UtcNow
            });
        }

        public bool Unblock(int mangaId, int blockId)
        {
            var entry = _repository.Find(blockId);
            if (entry?.MangaId != mangaId)
            {
                return false;
            }

            _repository.Delete(entry);
            return true;
        }
    }
}
