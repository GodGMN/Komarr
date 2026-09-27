using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public interface IMangaRepository : IBasicRepository<Manga>
    {
        Manga FindByAniListId(int aniListId);
    }

    public class MangaRepository : BasicRepository<Manga>, IMangaRepository
    {
        public MangaRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Manga FindByAniListId(int aniListId)
        {
            return Query(x => x.AniListId == aniListId).FirstOrDefault();
        }
    }

    public interface IMangaItemRepository : IBasicRepository<MangaItem>
    {
        IEnumerable<MangaItem> GetByMangaId(int mangaId);
    }

    public class MangaItemRepository : BasicRepository<MangaItem>, IMangaItemRepository
    {
        public MangaItemRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public IEnumerable<MangaItem> GetByMangaId(int mangaId)
        {
            return Query(x => x.MangaId == mangaId);
        }
    }

    public interface IMangaFileRepository : IBasicRepository<MangaFile>
    {
        IEnumerable<MangaFile> GetByMangaId(int mangaId);
        MangaFile FindByPath(string path);
    }

    public class MangaFileRepository : BasicRepository<MangaFile>, IMangaFileRepository
    {
        public MangaFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public IEnumerable<MangaFile> GetByMangaId(int mangaId)
        {
            return Query(x => x.MangaId == mangaId);
        }

        public MangaFile FindByPath(string path)
        {
            return Query(x => x.Path == path).FirstOrDefault();
        }
    }

    public interface IMangaFileItemRepository : IBasicRepository<MangaFileItem>
    {
        IEnumerable<MangaFileItem> GetByFileIds(IEnumerable<int> fileIds);
    }

    public class MangaFileItemRepository : BasicRepository<MangaFileItem>, IMangaFileItemRepository
    {
        public MangaFileItemRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public IEnumerable<MangaFileItem> GetByFileIds(IEnumerable<int> fileIds)
        {
            var ids = fileIds.Distinct().ToList();
            return ids.Count == 0 ? Enumerable.Empty<MangaFileItem>() : Query(x => ids.Contains(x.MangaFileId));
        }
    }

    public interface IMangaDownloadRepository : IBasicRepository<MangaDownload>
    {
        IEnumerable<MangaDownload> GetByMangaId(int mangaId);
        IEnumerable<MangaDownload> GetSent();
        IEnumerable<MangaDownload> GetCompleted();
        MangaDownload FindByDownloadId(int clientId, string downloadId);
    }

    public class MangaDownloadRepository : BasicRepository<MangaDownload>, IMangaDownloadRepository
    {
        public MangaDownloadRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public IEnumerable<MangaDownload> GetByMangaId(int mangaId)
        {
            return Query(x => x.MangaId == mangaId);
        }

        public IEnumerable<MangaDownload> GetSent()
        {
            return Query(x => x.Status == MangaDownloadStatus.Sent);
        }

        public IEnumerable<MangaDownload> GetCompleted()
        {
            return Query(x => x.Status == MangaDownloadStatus.Completed);
        }

        public MangaDownload FindByDownloadId(int clientId, string downloadId)
        {
            return Query(x => x.DownloadClientId == clientId && x.DownloadId == downloadId).FirstOrDefault();
        }
    }

    public interface IMangaDownloadFileRepository : IBasicRepository<MangaDownloadFile>
    {
        IEnumerable<MangaDownloadFile> GetByDownloadId(int downloadId);
    }

    public class MangaDownloadFileRepository : BasicRepository<MangaDownloadFile>, IMangaDownloadFileRepository
    {
        public MangaDownloadFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public IEnumerable<MangaDownloadFile> GetByDownloadId(int downloadId)
        {
            return Query(x => x.MangaDownloadId == downloadId);
        }
    }
}
