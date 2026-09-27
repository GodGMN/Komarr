using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Manga
{
    public class MangaLibraryMapFile
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public List<string> SuggestedNumbers { get; set; } = new ();
        public string Warning { get; set; }
        public bool Registered { get; set; }
    }

    public class MangaLibraryMapPreview
    {
        public string FolderPath { get; set; }
        public int MangaId { get; set; }
        public string MangaTitle { get; set; }
        public bool Truncated { get; set; }
        public List<MangaLibraryMapFile> Files { get; set; } = new ();
    }

    public class MangaLibraryMapSelection
    {
        public string Path { get; set; }
        public List<string> Numbers { get; set; } = new ();
    }

    public class MangaLibraryMapRequest
    {
        public string FolderPath { get; set; }
        public int MangaId { get; set; }
        public bool ConfirmUncertain { get; set; }
        public List<MangaLibraryMapSelection> Files { get; set; } = new ();
    }

    public class MangaLibraryMapResult
    {
        public int MangaId { get; set; }
        public string FolderPath { get; set; }
        public int FilesRegistered { get; set; }
        public int ItemsCreated { get; set; }
        public List<string> SkippedFiles { get; set; } = new ();
    }

    public interface IMangaLibraryMappingService
    {
        MangaLibraryMapPreview Preview(string folderPath, int mangaId);
        MangaLibraryMapResult Map(MangaLibraryMapRequest request);
    }

    public class MangaLibraryMappingService : IMangaLibraryMappingService
    {
        private const int MaximumItemsPerFile = 50;
        private readonly IMangaLibraryScanService _scan;
        private readonly IMangaService _manga;
        private readonly IMangaRepository _repository;
        private readonly IMangaFileRepository _files;
        private readonly IMangaFileItemRepository _coverage;
        private readonly IMangaReleaseParser _parser;
        private readonly IMangaReleaseMatcher _matcher;
        private readonly IDiskProvider _disk;

        public MangaLibraryMappingService(
            IMangaLibraryScanService scan,
            IMangaService manga,
            IMangaRepository repository,
            IMangaFileRepository files,
            IMangaFileItemRepository coverage,
            IMangaReleaseParser parser,
            IMangaReleaseMatcher matcher,
            IDiskProvider disk)
        {
            _scan = scan;
            _manga = manga;
            _repository = repository;
            _files = files;
            _coverage = coverage;
            _parser = parser;
            _matcher = matcher;
            _disk = disk;
        }

        public MangaLibraryMapPreview Preview(string folderPath, int mangaId)
        {
            var manga = _manga.Find(mangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            var folder = _scan.ScanFolder(folderPath);
            var library = _manga.All().ToList();
            var items = _manga.GetItems(mangaId).ToList();
            var expected = manga.TrackingMode == MangaTrackingMode.Volume ? MangaReleaseUnitType.Volume : MangaReleaseUnitType.Chapter;
            var preview = new MangaLibraryMapPreview
            {
                FolderPath = folder.Path,
                MangaId = mangaId,
                MangaTitle = manga.PreferredTitle,
                Truncated = folder.Truncated
            };

            foreach (var file in folder.Files)
            {
                var parsed = Parse(folder.Name, file.Name);
                var match = _matcher.Match(parsed, library, items);
                var candidate = match.Candidates.FirstOrDefault(value => value.Manga.Id == mangaId);
                var warning = file.Warning;
                if (parsed.UnitType != MangaReleaseUnitType.Unknown && parsed.UnitType != expected)
                {
                    warning = "Filename uses a different unit than this manga tracks.";
                }
                else if (match.IsAmbiguous || candidate == null || !candidate.CanAutoMatch)
                {
                    warning ??= match.IsAmbiguous ? "Several manga match this filename." : "Filename title needs manual confirmation.";
                }

                var registered = _files.FindByPath(file.Path);
                if (registered != null)
                {
                    warning = registered.MangaId == mangaId ? "File is already registered for this manga." :
                        "File is already registered for another manga.";
                }

                preview.Files.Add(new MangaLibraryMapFile
                {
                    Path = file.Path,
                    Name = file.Name,
                    SuggestedNumbers = parsed.UnitType == expected ? ExpandNumbers(parsed) : new List<string>(),
                    Warning = warning,
                    Registered = registered != null
                });
            }

            return preview;
        }

        public MangaLibraryMapResult Map(MangaLibraryMapRequest request)
        {
            if (request == null || request.MangaId <= 0 || request.Files == null || request.Files.Count == 0)
            {
                throw new ArgumentException("Choose at least one file and its volume or chapter numbers.");
            }

            var manga = _manga.Find(request.MangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            var preview = Preview(request.FolderPath, request.MangaId);
            if (!string.IsNullOrWhiteSpace(manga.Path) &&
                !string.Equals(Path.GetFullPath(manga.Path), Path.GetFullPath(preview.FolderPath), StringComparison.OrdinalIgnoreCase) &&
                _manga.GetFiles(manga.Id).Any())
            {
                throw new InvalidOperationException("This manga already has files in a different folder. Review its location before mapping another folder.");
            }

            var available = preview.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
            var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prepared = new List<(MangaLibraryMapFile File, List<string> Numbers, long Size, DateTime Modified)>();
            var result = new MangaLibraryMapResult { MangaId = manga.Id, FolderPath = preview.FolderPath };
            foreach (var selection in request.Files)
            {
                if (selection == null || string.IsNullOrWhiteSpace(selection.Path) ||
                    !selectedPaths.Add(selection.Path) || !available.TryGetValue(selection.Path, out var file))
                {
                    throw new ArgumentException("Selected file is duplicated or outside the scanned folder.");
                }

                if (file.Registered)
                {
                    result.SkippedFiles.Add(file.Path);
                    continue;
                }

                if (file.Warning != null && !request.ConfirmUncertain)
                {
                    throw new ArgumentException("Confirm uncertain filenames before mapping them.");
                }

                var numbers = (selection.Numbers ?? new List<string>()).Select(value => value?.Trim()).ToList();
                if (numbers.Count == 0 || numbers.Count > MaximumItemsPerFile ||
                    numbers.Any(value => !IsValidNumber(value)) ||
                    numbers.Select(value => decimal.Parse(value, CultureInfo.InvariantCulture)).Distinct().Count() != numbers.Count)
                {
                    throw new ArgumentException("Each selected file needs up to 50 distinct volume or chapter numbers.");
                }

                if (!_disk.FileExists(file.Path))
                {
                    throw new KeyNotFoundException("A selected file is no longer present. Scan the folder again.");
                }

                prepared.Add((file, numbers, _disk.GetFileSize(file.Path), _disk.FileGetLastWrite(file.Path)));
            }

            if (prepared.Count == 0)
            {
                return result;
            }

            var known = _manga.GetItems(manga.Id).ToList();
            manga.RootFolderPath = Path.GetDirectoryName(preview.FolderPath);
            manga.Path = preview.FolderPath;
            _repository.Update(manga);
            foreach (var entry in prepared)
            {
                var covered = new List<MangaItem>();
                foreach (var number in entry.Numbers)
                {
                    var numeric = decimal.Parse(number, CultureInfo.InvariantCulture);
                    var item = known.FirstOrDefault(value => value.NumberDecimal == numeric &&
                        value.Type == (manga.TrackingMode == MangaTrackingMode.Volume ? MangaItemType.Volume : MangaItemType.Chapter));
                    if (item?.DiscoveredFrom == MangaItemDiscoverySource.Metadata &&
                        !string.Equals(manga.Status, "FINISHED", StringComparison.OrdinalIgnoreCase))
                    {
                        item = _manga.AddItem(manga.Id, new MangaItemAddOptions { NumberText = number, Monitored = item.Monitored });
                        known.RemoveAll(value => value.Id == item.Id);
                        known.Add(item);
                    }

                    if (item == null)
                    {
                        try
                        {
                            item = _manga.AddItem(manga.Id, new MangaItemAddOptions { NumberText = number, Monitored = manga.Monitored });
                            result.ItemsCreated++;
                        }
                        catch (InvalidOperationException)
                        {
                            item = _manga.GetItems(manga.Id).FirstOrDefault(value => value.NumberDecimal == numeric);
                            if (item == null)
                            {
                                throw;
                            }
                        }

                        known.Add(item);
                    }

                    covered.Add(item);
                }

                var parsed = Parse(Path.GetFileName(preview.FolderPath), entry.File.Name);
                var stored = _files.Insert(new MangaFile
                {
                    MangaId = manga.Id,
                    Path = entry.File.Path,
                    Size = entry.Size,
                    Modified = entry.Modified,
                    DateAdded = DateTime.UtcNow,
                    OriginalFilePath = entry.File.Path,
                    SceneName = entry.File.Name,
                    ReleaseGroup = parsed.ReleaseGroup,
                    Language = parsed.Language,
                    Source = parsed.Source,
                    EditionLabel = parsed.EditionHint
                });
                foreach (var item in covered)
                {
                    _coverage.Insert(new MangaFileItem { MangaFileId = stored.Id, MangaItemId = item.Id });
                }

                result.FilesRegistered++;
            }

            return result;
        }

        private ParsedMangaReleaseInfo Parse(string folderName, string fileName)
        {
            var parsed = _parser.Parse(fileName);
            return parsed.UnitType == MangaReleaseUnitType.Unknown ? _parser.Parse($"{folderName} {fileName}") : parsed;
        }

        private static List<string> ExpandNumbers(ParsedMangaReleaseInfo parsed)
        {
            if (!parsed.StartNumber.HasValue || !parsed.EndNumber.HasValue ||
                parsed.EndNumber - parsed.StartNumber >= MaximumItemsPerFile)
            {
                return new List<string>();
            }

            if (parsed.StartNumber == parsed.EndNumber)
            {
                return new List<string> { parsed.StartNumberText };
            }

            if (parsed.StartNumber != decimal.Truncate(parsed.StartNumber.Value) ||
                parsed.EndNumber != decimal.Truncate(parsed.EndNumber.Value))
            {
                return new List<string>();
            }

            return Enumerable.Range((int)parsed.StartNumber.Value, (int)(parsed.EndNumber.Value - parsed.StartNumber.Value + 1))
                .Select(number => number.ToString(CultureInfo.InvariantCulture)).ToList();
        }

        private static bool IsValidNumber(string value)
        {
            return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) &&
                number > 0 && number <= 2000;
        }
    }
}
