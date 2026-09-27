using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MetadataSource.AniList;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Manga
{
    public class MangaSetupCheck
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string State { get; set; }
        public string Message { get; set; }
        public string Link { get; set; }
    }

    public class MangaSetupStatus
    {
        public bool ReadyForLocalUse { get; set; }
        public List<MangaSetupCheck> Checks { get; set; } = new ();
        public string BackupWarning { get; set; } = "Backups can contain download-client, indexer, and API credentials. Store them privately.";
    }

    public interface IMangaSetupStatusService
    {
        MangaSetupStatus GetStatus();
    }

    public class MangaSetupStatusService : IMangaSetupStatusService
    {
        private const long MinimumFreeSpace = 1024L * 1024 * 1024;
        private readonly IMangaRepository _database;
        private readonly IRootFolderService _roots;
        private readonly IIndexerFactory _indexers;
        private readonly IProvideDownloadClient _clients;
        private readonly IRemotePathMappingService _remotePaths;
        private readonly IAniListMetadataClient _anilist;
        private readonly IBackupService _backups;
        private readonly IDiskProvider _disk;
        private readonly IConfigFileProvider _config;

        public MangaSetupStatusService(
            IMangaRepository database,
            IRootFolderService roots,
            IIndexerFactory indexers,
            IProvideDownloadClient clients,
            IRemotePathMappingService remotePaths,
            IAniListMetadataClient anilist,
            IBackupService backups,
            IDiskProvider disk,
            IConfigFileProvider config)
        {
            _database = database;
            _roots = roots;
            _indexers = indexers;
            _clients = clients;
            _remotePaths = remotePaths;
            _anilist = anilist;
            _backups = backups;
            _disk = disk;
            _config = config;
        }

        public MangaSetupStatus GetStatus()
        {
            var status = new MangaSetupStatus();
            CheckDatabase(status);
            var roots = CheckRoots(status);
            CheckDiskSpace(status, roots);
            CheckIndexers(status);
            CheckClients(status);
            CheckRemotePaths(status);
            CheckAniList(status);
            CheckBackups(status);
            CheckAuthentication(status);
            status.ReadyForLocalUse = status.Checks
                .Where(check => check.Key is "database" or "roots" or "disk" or "indexers" or "clients" or "remotePaths")
                .All(check => check.State == "ready" || check.State == "warning");
            return status;
        }

        private void CheckDatabase(MangaSetupStatus status)
        {
            try
            {
                _database.Find(0);
                Add(status, "database", "Local database", "ready", "Komarr can read its local manga database.", "/system/status");
            }
            catch (Exception)
            {
                Add(status, "database", "Local database", "error", "The manga database could not be read.", "/system/status");
            }
        }

        private List<RootFolder> CheckRoots(MangaSetupStatus status)
        {
            try
            {
                var roots = _roots.All();
                if (roots.Count == 0)
                {
                    Add(status, "roots", "Manga root folders", "action", "Add a local folder for manga files.", "/settings/mediamanagement");
                }
                else if (roots.Any(root => string.IsNullOrWhiteSpace(root.Path) || !_disk.FolderExists(root.Path)))
                {
                    Add(status, "roots", "Manga root folders", "error", "At least one configured root folder is unavailable.", "/settings/mediamanagement");
                }
                else
                {
                    Add(status, "roots", "Manga root folders", "ready", $"{roots.Count} local root folder(s) are accessible.", "/settings/mediamanagement");
                }

                return roots;
            }
            catch (Exception)
            {
                Add(status, "roots", "Manga root folders", "error", "Root folders could not be checked.", "/settings/mediamanagement");
                return new List<RootFolder>();
            }
        }

        private void CheckDiskSpace(MangaSetupStatus status, List<RootFolder> roots)
        {
            if (roots.Count == 0)
            {
                Add(status, "disk", "Disk space", "action", "Add a root folder before checking free space.", "/settings/mediamanagement");
                return;
            }

            try
            {
                var available = roots.Where(root => !string.IsNullOrWhiteSpace(root.Path) && _disk.FolderExists(root.Path))
                    .Select(root => _disk.GetAvailableSpace(root.Path)).ToList();
                if (available.Count == 0 || available.Any(value => !value.HasValue))
                {
                    Add(status, "disk", "Disk space", "warning", "Free space could not be determined for every manga root.", "/system/status");
                }
                else if (available.Any(value => value < MinimumFreeSpace))
                {
                    Add(status, "disk", "Disk space", "warning", "A manga root has less than 1 GiB free.", "/system/status");
                }
                else
                {
                    Add(status, "disk", "Disk space", "ready", "Manga roots have at least 1 GiB free.", "/system/status");
                }
            }
            catch (Exception)
            {
                Add(status, "disk", "Disk space", "warning", "Free space could not be checked.", "/system/status");
            }
        }

        private void CheckIndexers(MangaSetupStatus status)
        {
            try
            {
                var rss = _indexers.RssEnabled(false);
                var search = _indexers.AutomaticSearchEnabled(false);
                if (rss.Count == 0 || search.Count == 0)
                {
                    Add(status, "indexers", "Indexer feeds and search", "action", "Enable an RSS feed and automatic search. A Prowlarr Torznab/Newznab endpoint can be added as an indexer.", "/settings/indexers");
                    return;
                }

                var failed = rss.Concat(search).Distinct().Where(indexer => !indexer.Test().IsValid).ToList();
                var message = failed.Count == 0 ? $"{rss.Count} feed(s) and {search.Count} search indexer(s) are available." : $"{failed.Count} indexer connection test(s) failed.";
                Add(status, "indexers", "Indexer feeds and search", failed.Count == 0 ? "ready" : "error", message, "/settings/indexers");
            }
            catch (Exception)
            {
                Add(status, "indexers", "Indexer feeds and search", "error", "Indexer connections could not be checked.", "/settings/indexers");
            }
        }

        private void CheckClients(MangaSetupStatus status)
        {
            try
            {
                var clients = _clients.GetDownloadClients(false).ToList();
                if (clients.Count == 0)
                {
                    Add(status, "clients", "Download client", "action", "Add a compatible torrent or Usenet client.", "/settings/downloadclients");
                    return;
                }

                var failed = clients.Where(client => !client.Test().IsValid).ToList();
                var message = failed.Count == 0 ? $"{clients.Count} download client(s) are reachable." : $"{failed.Count} download client connection test(s) failed.";
                Add(status, "clients", "Download client", failed.Count == 0 ? "ready" : "error", message, "/settings/downloadclients");
            }
            catch (Exception)
            {
                Add(status, "clients", "Download client", "error", "Download clients could not be checked.", "/settings/downloadclients");
            }
        }

        private void CheckRemotePaths(MangaSetupStatus status)
        {
            try
            {
                var mappings = _remotePaths.All();
                var missing = mappings.Count(mapping => string.IsNullOrWhiteSpace(mapping.LocalPath) || !_disk.FolderExists(mapping.LocalPath));
                var message = missing > 0 ? $"{missing} remote path mapping(s) point to missing local folders." : "Shared local paths need no mapping.";
                if (missing == 0 && mappings.Count > 0)
                {
                    message = $"{mappings.Count} remote path mapping(s) have accessible local folders.";
                }

                Add(status, "remotePaths", "Remote paths", missing == 0 ? "ready" : "error", message, "/settings/downloadclients");
            }
            catch (Exception)
            {
                Add(status, "remotePaths", "Remote paths", "error", "Remote path mappings could not be checked.", "/settings/downloadclients");
            }
        }

        private void CheckAniList(MangaSetupStatus status)
        {
            try
            {
                var response = _anilist.Search("BLAME!");
                var available = response.Availability == AniListAvailability.Available;
                var message = available ? "AniList search is available." : "AniList is unavailable or rate limited. Saved manga and local collection management still work.";
                Add(status, "anilist", "AniList metadata", available ? "ready" : "warning", message, "/manga/add");
            }
            catch (Exception)
            {
                Add(status, "anilist", "AniList metadata", "warning", "AniList could not be reached. Saved manga and local collection management still work.", "/manga/add");
            }
        }

        private void CheckBackups(MangaSetupStatus status)
        {
            try
            {
                var backups = _backups.GetBackups();
                var message = backups.Count > 0 ? $"{backups.Count} backup(s) are available for restore." : "Create a backup of the database and config before relying on this install.";
                Add(status, "backups", "Database and config backups", backups.Count > 0 ? "ready" : "warning", message, "/system/backup");
            }
            catch (Exception)
            {
                Add(status, "backups", "Database and config backups", "warning", "Backups could not be listed.", "/system/backup");
            }
        }

        private void CheckAuthentication(MangaSetupStatus status)
        {
            try
            {
                var enabled = _config.AuthenticationMethod != AuthenticationType.None;
                var message = enabled ? "UI authentication is enabled." : "UI authentication is disabled. Set a username and password before exposing Komarr beyond a trusted network.";
                Add(status, "authentication", "UI authentication", enabled ? "ready" : "warning", message, "/settings/general");
            }
            catch (Exception)
            {
                Add(status, "authentication", "UI authentication", "warning", "Authentication settings could not be checked.", "/settings/general");
            }
        }

        private static void Add(MangaSetupStatus status, string key, string label, string state, string message, string link)
        {
            status.Checks.Add(new MangaSetupCheck { Key = key, Label = label, State = state, Message = message, Link = link });
        }
    }
}
