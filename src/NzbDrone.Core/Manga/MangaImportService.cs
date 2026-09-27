using System;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Manga
{
    public interface IMangaImportService
    {
        int ImportReady(MangaDownload download);
    }

    public class MangaImportService : IMangaImportService
    {
        private readonly IMangaService _manga;
        private readonly IMangaFileRepository _libraryFiles;
        private readonly IMangaFileItemRepository _coverage;
        private readonly IMangaDownloadFileRepository _downloadFiles;
        private readonly IDiskProvider _disk;
        private readonly IConfigService _config;
        private readonly IMangaHistoryService _history;
        private readonly IMangaReleaseParser _parser;
        private readonly Logger _logger;

        public MangaImportService(
            IMangaService manga,
            IMangaFileRepository libraryFiles,
            IMangaFileItemRepository coverage,
            IMangaDownloadFileRepository downloadFiles,
            IDiskProvider disk,
            IConfigService config,
            IMangaHistoryService history,
            IMangaReleaseParser parser,
            Logger logger)
        {
            _manga = manga;
            _libraryFiles = libraryFiles;
            _coverage = coverage;
            _downloadFiles = downloadFiles;
            _disk = disk;
            _config = config;
            _history = history;
            _parser = parser;
            _logger = logger;
        }

        public int ImportReady(MangaDownload download)
        {
            var manga = _manga.Find(download.MangaId);
            if (manga == null || string.IsNullOrWhiteSpace(manga.RootFolderPath))
            {
                return 0;
            }

            var root = Path.GetFullPath(manga.RootFolderPath);
            if (!Path.IsPathFullyQualified(manga.RootFolderPath) || !_disk.FolderExists(root))
            {
                return 0;
            }

            var title = SafePart(manga.PreferredTitle ?? manga.TitleRomaji);
            var destinationFolder = Path.GetFullPath(string.IsNullOrWhiteSpace(manga.Path)
                ? Path.Combine(root, title)
                : manga.Path);
            var ready = _downloadFiles.GetByDownloadId(download.Id)
                .Where(file => file.Status == MangaDownloadFileStatus.Ready)
                .ToList();
            var parsed = _parser.Parse(download.ReleaseTitle);
            foreach (var file in ready)
            {
                try
                {
                    ImportOne(download, manga, file, root, destinationFolder, title, parsed);
                }
                catch (Exception ex)
                {
                    _logger.Warn("Could not import manga download file {0}: {1}", file.Id, ex.GetType().Name);
                    if (file.Reason != "Import failed; Komarr will retry this file.")
                    {
                        file.Reason = "Import failed; Komarr will retry this file.";
                        _downloadFiles.Update(file);
                        RecordHistory(download, MangaHistoryEventType.ImportFailed, file.Reason, file);
                    }
                }
            }

            return ready.Count(file => file.Status == MangaDownloadFileStatus.Imported);
        }

        private void ImportOne(MangaDownload download, Manga manga, MangaDownloadFile file, string root, string folder, string title, ParsedMangaReleaseInfo parsed)
        {
            if (!Inside(root, folder))
            {
                NeedsReview(download, file, "The manga folder is outside its configured root folder.");
                return;
            }

            var items = _manga.GetItems(manga.Id).Where(item => file.CoveredItemIds.Contains(item.Id)).ToList();
            if (items.Count == 0 || items.Count != file.CoveredItemIds.Distinct().Count())
            {
                NeedsReview(download, file, "File coverage no longer matches known manga items.");
                return;
            }

            var existing = _manga.GetFiles(manga.Id).ToList();
            var importedItemIds = _coverage.GetByFileIds(existing.Select(value => value.Id))
                .Select(value => value.MangaItemId)
                .ToHashSet();
            if (file.CoveredItemIds.Any(importedItemIds.Contains))
            {
                NeedsReview(download, file, "An imported file already covers one of these manga items.");
                return;
            }

            var extension = Path.GetExtension(file.Path).ToLowerInvariant();
            var unit = manga.TrackingMode == MangaTrackingMode.Volume ? "v" : "c";
            var ordered = items.OrderBy(item => item.NumberDecimal ?? decimal.MaxValue).ThenBy(item => item.NumberText).ToList();
            var first = NumberLabel(ordered.First(), manga.TrackingMode);
            var last = NumberLabel(ordered.Last(), manga.TrackingMode);
            var range = first == last ? first : $"{first}-{unit}{last}";
            var target = Path.GetFullPath(Path.Combine(folder, $"{title} - {unit}{range}{extension}"));
            if (!Inside(root, target) || !Inside(folder, target) || string.Equals(file.Path, target, StringComparison.OrdinalIgnoreCase))
            {
                NeedsReview(download, file, "The destination path is unsafe or matches the source file.");
                return;
            }

            if (_libraryFiles.FindByPath(target) != null || _disk.FileExists(target))
            {
                NeedsReview(download, file, "A file already exists at the destination path.");
                return;
            }

            if (!_disk.FileExists(file.Path))
            {
                file.Reason = "The source file is not accessible yet.";
                _downloadFiles.Update(file);
                return;
            }

            _disk.EnsureFolder(folder);
            var created = false;
            MangaFile imported = null;
            try
            {
                if (download.Protocol == DownloadProtocol.Torrent && _config.CopyUsingHardlinks)
                {
                    try
                    {
                        created = _disk.TryCreateHardLink(file.Path, target);
                    }
                    catch (IOException)
                    {
                        // Cross-filesystem and unsupported hardlinks fall back to copy.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Some mounted filesystems deny links but allow a regular copy.
                    }
                }

                if (!created)
                {
                    _disk.CopyFile(file.Path, target);
                    created = true;
                }

                imported = _libraryFiles.Insert(new MangaFile
                {
                    MangaId = manga.Id,
                    Path = target,
                    Size = _disk.GetFileSize(target),
                    Modified = _disk.FileGetLastWrite(target),
                    DateAdded = DateTime.UtcNow,
                    OriginalFilePath = file.Path,
                    SceneName = download.ReleaseTitle,
                    ReleaseGroup = parsed.ReleaseGroup,
                    Language = parsed.Language,
                    Source = parsed.Source,
                    EditionLabel = parsed.EditionHint
                });
                foreach (var item in items)
                {
                    _coverage.Insert(new MangaFileItem { MangaFileId = imported.Id, MangaItemId = item.Id });
                }

                file.MangaFileId = imported.Id;
                file.ImportedAt = DateTime.UtcNow;
                file.Status = MangaDownloadFileStatus.Imported;
                file.Reason = null;
                _downloadFiles.Update(file);
                RecordHistory(download, MangaHistoryEventType.Imported, $"Imported {Path.GetFileName(target)}.", file);
            }
            catch
            {
                if (imported != null)
                {
                    _libraryFiles.Delete(imported.Id);
                }

                if (created && _disk.FileExists(target))
                {
                    _disk.DeleteFile(target);
                }

                throw;
            }
        }

        private void NeedsReview(MangaDownload download, MangaDownloadFile file, string reason)
        {
            file.Status = MangaDownloadFileStatus.ManualReview;
            file.Reason = reason;
            _downloadFiles.Update(file);
            RecordHistory(download, MangaHistoryEventType.NeedsReview, reason, file);
        }

        private void RecordHistory(MangaDownload download, MangaHistoryEventType eventType, string message, MangaDownloadFile file)
        {
            try
            {
                _history.Record(download, eventType, message, file);
            }
            catch (Exception ex)
            {
                _logger.Warn("Could not record manga import history for file {0}: {1}", file.Id, ex.GetType().Name);
            }
        }

        private static string NumberLabel(MangaItem item, MangaTrackingMode mode)
        {
            if (item.NumberDecimal is { } number && number == decimal.Truncate(number) && number >= 0 && number < 10000)
            {
                return ((int)number).ToString(mode == MangaTrackingMode.Volume ? "D2" : "D3");
            }

            return SafePart(item.NumberText);
        }

        private static string SafePart(string value)
        {
            var source = string.IsNullOrWhiteSpace(value) ? "Manga" : value;
            var safe = new string(source.Select(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character) ? '_' : character).ToArray())
                .Trim().Trim('.');
            return string.IsNullOrWhiteSpace(safe) ? "Manga" : safe;
        }

        private static bool Inside(string root, string path)
        {
            var relative = Path.GetRelativePath(root, path);
            return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }
    }
}
