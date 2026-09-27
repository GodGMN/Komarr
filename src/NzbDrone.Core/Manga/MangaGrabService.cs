using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Books;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga
{
    public class MangaGrabRequest
    {
        public string Guid { get; set; }
        public string Title { get; set; }
        public int IndexerId { get; set; }
        public int? ItemId { get; set; }
        public int? DownloadClientId { get; set; }
        public bool ConfirmManualReview { get; set; }
    }

    public class MangaGrabValidationException : Exception
    {
        public MangaGrabValidationException(string message)
            : base(message)
        {
        }
    }

    public class MangaGrabbedEvent : IEvent
    {
        public MangaGrabbedEvent(MangaDownload download)
        {
            Download = download;
        }

        public MangaDownload Download { get; }
    }

    public interface IMangaGrabService
    {
        Task<MangaDownload> Grab(int mangaId, MangaGrabRequest request);
        IEnumerable<MangaDownload> GetDownloads(int mangaId);
    }

    public class MangaGrabService : IMangaGrabService
    {
        private readonly object _grabLock = new ();
        private readonly IMangaService _manga;
        private readonly IMangaFileItemRepository _fileItems;
        private readonly IMangaDownloadRepository _downloads;
        private readonly IMangaHistoryService _history;
        private readonly IMangaBlocklistService _blocklist;
        private readonly IMangaIndexerSearchService _indexers;
        private readonly IMangaReleaseParser _parser;
        private readonly IMangaReleaseMatcher _matcher;
        private readonly IMangaReleaseDecisionEngine _decisions;
        private readonly IProvideDownloadClient _clients;
        private readonly IDownloadClientStatusService _clientStatus;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IEventAggregator _events;
        private readonly Logger _logger;

        public MangaGrabService(
            IMangaService manga,
            IMangaFileItemRepository fileItems,
            IMangaDownloadRepository downloads,
            IMangaHistoryService history,
            IMangaBlocklistService blocklist,
            IMangaIndexerSearchService indexers,
            IMangaReleaseParser parser,
            IMangaReleaseMatcher matcher,
            IMangaReleaseDecisionEngine decisions,
            IProvideDownloadClient clients,
            IDownloadClientStatusService clientStatus,
            IIndexerFactory indexerFactory,
            IEventAggregator events,
            Logger logger)
        {
            _manga = manga;
            _fileItems = fileItems;
            _downloads = downloads;
            _history = history;
            _blocklist = blocklist;
            _indexers = indexers;
            _parser = parser;
            _matcher = matcher;
            _decisions = decisions;
            _clients = clients;
            _clientStatus = clientStatus;
            _indexerFactory = indexerFactory;
            _events = events;
            _logger = logger;
        }

        public IEnumerable<MangaDownload> GetDownloads(int mangaId)
        {
            if (_manga.Find(mangaId) == null)
            {
                throw new KeyNotFoundException("Manga was not found.");
            }

            return _downloads.GetByMangaId(mangaId);
        }

        public async Task<MangaDownload> Grab(int mangaId, MangaGrabRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Guid) || string.IsNullOrWhiteSpace(request.Title))
            {
                throw new MangaGrabValidationException("Select a release from the current manga search results.");
            }

            var manga = _manga.Find(mangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            var items = _manga.GetItems(mangaId).ToList();
            if (request.ItemId.HasValue && items.All(item => item.Id != request.ItemId.Value))
            {
                throw new MangaGrabValidationException("Requested item does not belong to this manga.");
            }

            var search = await _indexers.Search(manga);
            var release = search.Releases.FirstOrDefault(value => value.IndexerId == request.IndexerId &&
                string.Equals(value.Guid, request.Guid, StringComparison.Ordinal) &&
                string.Equals(value.Title, request.Title, StringComparison.Ordinal));
            if (release == null)
            {
                throw new MangaGrabValidationException("Selected release is no longer available from this indexer. Search again.");
            }

            if (_blocklist.IsBlocked(mangaId, release.IndexerId, release.Guid))
            {
                throw new MangaGrabValidationException("This release is blocklisted after a failed download. Clear its blocklist entry before retrying.");
            }

            var parsed = _parser.Parse(release.Title);
            var match = _matcher.Match(parsed, _manga.All(), items, request.ItemId);
            var candidate = match.Candidates.FirstOrDefault(value => value.Manga.Id == mangaId);
            var files = _manga.GetFiles(mangaId).ToList();
            var fileItems = _fileItems.GetByFileIds(files.Select(file => file.Id));
            var decision = _decisions.Evaluate(manga, match, candidate, release, files, fileItems, requestedItemId: request.ItemId);
            if (!decision.CanGrabManually)
            {
                throw new MangaGrabValidationException(string.Join(" ", decision.Rejections));
            }

            if (decision.ReviewReasons.Count > 0 && !request.ConfirmManualReview)
            {
                throw new MangaGrabValidationException("Review this release and confirm the manual selection before grabbing.");
            }

            var client = request.DownloadClientId.HasValue
                ? _clients.Get(request.DownloadClientId.Value)
                : _clients.GetDownloadClient(release.DownloadProtocol, release.IndexerId, true, manga.Tags?.ToHashSet());
            if (client == null || client.Protocol != release.DownloadProtocol)
            {
                throw new MangaGrabValidationException("No compatible download client is configured for this release.");
            }

            var indexer = release.IndexerId > 0 ? _indexerFactory.GetInstance(_indexerFactory.Get(release.IndexerId)) : null;
            var pending = new MangaDownload
            {
                MangaId = mangaId,
                RequestedItemId = request.ItemId,
                CoveredItemIds = candidate.CoveredItems.Select(item => item.Id).ToList(),
                IndexerId = release.IndexerId,
                Indexer = release.Indexer,
                ReleaseGuid = release.Guid,
                ReleaseTitle = release.Title,
                Protocol = release.DownloadProtocol,
                DownloadClientId = client.Definition.Id,
                DownloadClient = client.Definition.Name,
                Status = MangaDownloadStatus.Pending,
                Added = DateTime.UtcNow
            };
            MangaDownload download;
            lock (_grabLock)
            {
                if (_downloads.GetByMangaId(mangaId).Any(value => value.IndexerId == request.IndexerId &&
                    value.ReleaseGuid == request.Guid && value.Status != MangaDownloadStatus.Failed))
                {
                    throw new MangaGrabValidationException("This release was already sent to a download client.");
                }

                download = _downloads.Insert(pending);
            }

            var remote = new RemoteBook
            {
                Release = release,
                Author = new Author { Name = manga.PreferredTitle ?? manga.TitleRomaji, Tags = manga.Tags?.ToHashSet() ?? new HashSet<int>() },
                Books = new List<Book> { new Book { Title = release.Title, ReleaseDate = release.PublishDate } },
                ReleaseSource = ReleaseSourceType.InteractiveSearch,
                DownloadAllowed = true
            };
            string downloadId;
            try
            {
                downloadId = await client.Download(remote, indexer);
                if (string.IsNullOrWhiteSpace(downloadId))
                {
                    throw new InvalidOperationException("Download client returned no tracking ID.");
                }
            }
            catch (Exception ex)
            {
                download.Status = MangaDownloadStatus.Failed;
                download.Error = "Download client rejected or could not fetch this release.";
                download.LastUpdated = DateTime.UtcNow;
                _downloads.Update(download);
                RecordHistory(download, MangaHistoryEventType.GrabFailed, download.Error);
                _clientStatus.RecordFailure(client.Definition.Id);
                _logger.Warn("Manual manga grab failed on client {0}: {1}", client.Definition.Name, ex.GetType().Name);
                throw;
            }

            download.DownloadId = downloadId;
            download.Status = MangaDownloadStatus.Sent;
            download.LastUpdated = DateTime.UtcNow;
            download = _downloads.Update(download);
            RecordHistory(download, MangaHistoryEventType.Grabbed, $"Sent to {download.DownloadClient}.");
            _clientStatus.RecordSuccess(client.Definition.Id);
            _events.PublishEvent(new MangaGrabbedEvent(download));
            return download;
        }

        private void RecordHistory(MangaDownload download, MangaHistoryEventType eventType, string message)
        {
            try
            {
                _history.Record(download, eventType, message);
            }
            catch (Exception ex)
            {
                _logger.Warn("Could not record manga history for download {0}: {1}", download.Id, ex.GetType().Name);
            }
        }
    }
}
