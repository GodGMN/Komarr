using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Manga
{
    public class MangaLibraryFile
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string ParsedTitle { get; set; }
        public MangaReleaseUnitType UnitType { get; set; }
        public string StartNumber { get; set; }
        public string EndNumber { get; set; }
        public string Warning { get; set; }
    }

    public class MangaLibraryFolder
    {
        public string RootPath { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public int? MappedMangaId { get; set; }
        public int? SuggestedMangaId { get; set; }
        public string SuggestedTitle { get; set; }
        public bool NeedsReview { get; set; }
        public bool Truncated { get; set; }
        public List<MangaLibraryFile> Files { get; set; } = new ();
    }

    public class MangaLibraryScanResult
    {
        public int RootsScanned { get; set; }
        public bool Truncated { get; set; }
        public List<MangaLibraryFolder> Folders { get; set; } = new ();
        public List<string> Errors { get; set; } = new ();
    }

    public interface IMangaLibraryScanService
    {
        MangaLibraryScanResult Scan();
        MangaLibraryFolder ScanFolder(string folderPath);
    }

    public class MangaLibraryScanService : IMangaLibraryScanService
    {
        private const int MaximumRoots = 100;
        private const int MaximumFolders = 2000;
        private const int MaximumNestedFolders = 20;
        private const int MaximumFilesPerFolder = 200;
        private const int MaximumFilesTotal = 10000;
        private const int MaximumEntriesPerFolder = 1000;
        private static readonly HashSet<string> SupportedExtensions = new (StringComparer.OrdinalIgnoreCase)
        {
            ".cbz", ".cbr", ".epub", ".pdf", ".zip"
        };

        private readonly IRootFolderService _roots;
        private readonly IMangaService _manga;
        private readonly IMangaReleaseParser _parser;
        private readonly IDiskProvider _disk;
        private readonly Logger _logger;

        public MangaLibraryScanService(
            IRootFolderService roots,
            IMangaService manga,
            IMangaReleaseParser parser,
            IDiskProvider disk,
            Logger logger)
        {
            _roots = roots;
            _manga = manga;
            _parser = parser;
            _disk = disk;
            _logger = logger;
        }

        public MangaLibraryScanResult Scan()
        {
            var result = new MangaLibraryScanResult();
            var roots = _roots.All().Where(root => !string.IsNullOrWhiteSpace(root.Path)).Take(MaximumRoots + 1).ToList();
            result.Truncated = roots.Count > MaximumRoots;
            var library = _manga.All().ToList();
            var aliases = new MangaAliasIndex(library, true);
            var mapped = library.Where(manga => !string.IsNullOrWhiteSpace(manga.Path))
                .GroupBy(manga => NormalizePath(manga.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
            var foldersInspected = 0;
            var filesReturned = 0;

            foreach (var root in roots.Take(MaximumRoots))
            {
                if (foldersInspected >= MaximumFolders || filesReturned >= MaximumFilesTotal)
                {
                    result.Truncated = true;
                    break;
                }

                try
                {
                    if (!_disk.FolderExists(root.Path))
                    {
                        result.Errors.Add($"Root is not accessible: {root.Path}");
                        continue;
                    }

                    result.RootsScanned++;
                    foreach (var folderPath in _disk.GetDirectories(root.Path).Take(MaximumFolders - foldersInspected + 1))
                    {
                        if (foldersInspected >= MaximumFolders || filesReturned >= MaximumFilesTotal)
                        {
                            result.Truncated = true;
                            break;
                        }

                        foldersInspected++;
                        try
                        {
                            var folder = InspectFolder(root.Path, folderPath, aliases, mapped);
                            if (folder.Files.Count > 0 || folder.Truncated)
                            {
                                result.Folders.Add(folder);
                                filesReturned += folder.Files.Count;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn(ex, "Could not scan manga folder {0}", folderPath);
                            result.Errors.Add($"Could not scan folder: {folderPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Could not scan manga root {0}", root.Path);
                    result.Errors.Add($"Could not scan root: {root.Path}");
                }
            }

            return result;
        }

        public MangaLibraryFolder ScanFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Path.IsPathFullyQualified(folderPath))
            {
                throw new ArgumentException("Choose a folder inside a configured root.");
            }

            var normalized = NormalizePath(folderPath);
            var parent = Path.GetDirectoryName(normalized);
            var root = _roots.All().FirstOrDefault(value =>
                !string.IsNullOrWhiteSpace(value.Path) &&
                string.Equals(NormalizePath(value.Path), parent, StringComparison.OrdinalIgnoreCase));
            if (root == null || !_disk.FolderExists(normalized))
            {
                throw new KeyNotFoundException("Manga folder was not found directly under a configured root.");
            }

            var library = _manga.All().ToList();
            var aliases = new MangaAliasIndex(library, true);
            var mapped = library.Where(manga => !string.IsNullOrWhiteSpace(manga.Path))
                .GroupBy(manga => NormalizePath(manga.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
            return InspectFolder(root.Path, normalized, aliases, mapped);
        }

        private MangaLibraryFolder InspectFolder(
            string rootPath,
            string folderPath,
            MangaAliasIndex aliases,
            Dictionary<string, int> mapped)
        {
            var name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var folder = new MangaLibraryFolder { RootPath = rootPath, Path = folderPath, Name = name };
            if (mapped.TryGetValue(NormalizePath(folderPath), out var mangaId))
            {
                folder.MappedMangaId = mangaId;
            }
            else
            {
                var lookup = aliases.Find(name);
                if (!lookup.TooBroad && lookup.Candidates.Count == 1)
                {
                    folder.SuggestedMangaId = lookup.Candidates[0].Id;
                    folder.SuggestedTitle = lookup.Candidates[0].PreferredTitle;
                }

                folder.NeedsReview = true;
            }

            var paths = new List<string>();
            var entriesInspected = 0;
            foreach (var path in _disk.GetFiles(folderPath, false))
            {
                entriesInspected++;
                if (SupportedExtensions.Contains(Path.GetExtension(path)))
                {
                    paths.Add(path);
                }

                if (paths.Count > MaximumFilesPerFolder || entriesInspected >= MaximumEntriesPerFolder)
                {
                    folder.Truncated = true;
                    break;
                }
            }

            if (!folder.Truncated)
            {
                var nested = _disk.GetDirectories(folderPath).Take(MaximumNestedFolders + 1).ToList();
                folder.Truncated |= nested.Count > MaximumNestedFolders;
                foreach (var child in nested.Take(MaximumNestedFolders))
                {
                    foreach (var path in _disk.GetFiles(child, false))
                    {
                        entriesInspected++;
                        if (SupportedExtensions.Contains(Path.GetExtension(path)))
                        {
                            paths.Add(path);
                        }

                        if (paths.Count > MaximumFilesPerFolder || entriesInspected >= MaximumEntriesPerFolder)
                        {
                            folder.Truncated = true;
                            break;
                        }
                    }

                    if (paths.Count > MaximumFilesPerFolder || entriesInspected >= MaximumEntriesPerFolder)
                    {
                        break;
                    }
                }
            }

            foreach (var path in paths.Take(MaximumFilesPerFolder))
            {
                var filename = Path.GetFileName(path);
                var parsed = _parser.Parse(filename);
                if (parsed.UnitType == MangaReleaseUnitType.Unknown)
                {
                    parsed = _parser.Parse($"{name} {filename}");
                }

                var file = new MangaLibraryFile
                {
                    Path = path,
                    Name = filename,
                    ParsedTitle = parsed.ParsedTitle,
                    UnitType = parsed.UnitType,
                    StartNumber = parsed.StartNumberText,
                    EndNumber = parsed.EndNumberText,
                    Warning = parsed.UnitType == MangaReleaseUnitType.Unknown ?
                        parsed.Warnings.FirstOrDefault() ?? "Filename needs manual review." : null
                };
                folder.Files.Add(file);
                folder.NeedsReview |= file.Warning != null;
            }

            return folder;
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
