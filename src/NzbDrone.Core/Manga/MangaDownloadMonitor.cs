using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Manga
{
    public class MangaDownloadMonitor : IHandle<TrackedDownloadRefreshedEvent>
    {
        private readonly IMangaDownloadRepository _downloads;
        private readonly IMangaDownloadFileRepository _files;
        private readonly IMangaDownloadFileIdentifier _identifier;
        private readonly IMangaImportService _imports;
        private readonly IProvideDownloadClient _clients;
        private readonly IDiskProvider _disk;
        private readonly Logger _logger;

        public MangaDownloadMonitor(
            IMangaDownloadRepository downloads,
            IMangaDownloadFileRepository files,
            IMangaDownloadFileIdentifier identifier,
            IMangaImportService imports,
            IProvideDownloadClient clients,
            IDiskProvider disk,
            Logger logger)
        {
            _downloads = downloads;
            _files = files;
            _identifier = identifier;
            _imports = imports;
            _clients = clients;
            _disk = disk;
            _logger = logger;
        }

        public void Handle(TrackedDownloadRefreshedEvent message)
        {
            var sent = _downloads.GetSent().Where(download => !string.IsNullOrWhiteSpace(download.DownloadId)).ToList();
            foreach (var group in sent.GroupBy(download => download.DownloadClientId))
            {
                try
                {
                    var client = _clients.Get(group.Key);
                    if (client == null)
                    {
                        continue;
                    }

                    var clientItems = client.GetItems().ToList();
                    foreach (var download in group)
                    {
                        try
                        {
                            var item = clientItems.FirstOrDefault(value => string.Equals(value.DownloadId, download.DownloadId, StringComparison.OrdinalIgnoreCase));
                            if (item == null || item.Status != DownloadItemStatus.Completed || item.OutputPath.IsEmpty)
                            {
                                continue;
                            }

                            IdentifyCompleted(download, item.OutputPath.FullPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn("Could not identify files for manga download {0}: {1}", download.Id, ex.GetType().Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Could not inspect completed manga downloads for client {0}: {1}", group.Key, ex.GetType().Name);
                }
            }

            foreach (var completed in _downloads.GetCompleted() ?? Enumerable.Empty<MangaDownload>())
            {
                if (!_files.GetByDownloadId(completed.Id).Any(file => file.Status == MangaDownloadFileStatus.Ready))
                {
                    continue;
                }

                try
                {
                    _imports.ImportReady(completed);
                }
                catch (Exception ex)
                {
                    _logger.Warn("Could not import ready manga files from download {0}: {1}", completed.Id, ex.GetType().Name);
                }
            }
        }

        private void IdentifyCompleted(MangaDownload download, string outputPath)
        {
            IEnumerable<string> paths;
            if (_disk.FileExists(outputPath))
            {
                paths = new[] { outputPath };
            }
            else if (_disk.FolderExists(outputPath))
            {
                paths = _disk.GetFiles(outputPath, true);
            }
            else
            {
                _logger.Warn("Completed manga download {0} has no accessible output path yet", download.Id);
                return;
            }

            var identified = _identifier.Identify(download, paths);
            if (identified.Count == 0)
            {
                _logger.Warn("Completed manga download {0} contains no files", download.Id);
                return;
            }

            var recordedPaths = _files.GetByDownloadId(download.Id).Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in identified.Where(file => !recordedPaths.Contains(file.Path)))
            {
                _files.Insert(file);
            }

            download.Status = MangaDownloadStatus.Completed;
            download.LastUpdated = DateTime.UtcNow;
            _downloads.Update(download);
        }
    }
}
