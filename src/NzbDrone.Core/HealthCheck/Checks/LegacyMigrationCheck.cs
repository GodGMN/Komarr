using System.Linq;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.OpenLibrary;

namespace NzbDrone.Core.HealthCheck.Checks
{
    // Surfaces the first-boot legacy → OpenLibrary migration to the UI so
    // users see what's happening when they bring up Librarr against an
    // old Readarr /config directory. LegacyMigrationService does the
    // actual work; this check is the user-facing indicator that
    // (a) a migration is needed, (b) it's running, or (c) it's done
    // (no health entry returned in that case).
    [CheckOn(typeof(ApplicationStartedEvent))]
    public class LegacyMigrationCheck : HealthCheckBase
    {
        private readonly IConfigService _configService;
        private readonly IAuthorService _authorService;
        private readonly IManageCommandQueue _commandQueue;
        private readonly Logger _logger;

        public LegacyMigrationCheck(IConfigService configService,
                                    IAuthorService authorService,
                                    IManageCommandQueue commandQueue,
                                    ILocalizationService localizationService,
                                    Logger logger)
            : base(localizationService)
        {
            _configService = configService;
            _authorService = authorService;
            _commandQueue = commandQueue;
            _logger = logger;
        }

        public override HealthCheck Check()
        {
            if (_configService.LegacyMigrationCompleted &&
                _configService.LegacyMigrationVersion >= LegacyMigrationService.CurrentVersion)
            {
                return new HealthCheck(GetType());
            }

            var hasLegacyAuthors = _authorService.GetAllAuthors()
                .Any(a => !OpenLibraryIdHelper.IsAuthorId(a.Metadata?.Value?.ForeignAuthorId));

            if (!hasLegacyAuthors)
            {
                // Either no authors (fresh install) or every author is
                // already OL-shaped. LegacyMigrationService will set the
                // marker on its next tick; show nothing in the interim.
                return new HealthCheck(GetType());
            }

            var migrating = _commandQueue.All()
                .Any(c => c.Name == nameof(ReidentifyLibraryCommand) &&
                          (c.Status == CommandStatus.Started || c.Status == CommandStatus.Queued));

            if (migrating)
            {
                return new HealthCheck(
                    GetType(),
                    HealthCheckResult.Notice,
                    "Reidentifying library against Open Library. New books discovered during this pass arrive unmonitored — flip the ones you want once it completes.",
                    "#legacy-migration-running");
            }

            return new HealthCheck(
                GetType(),
                HealthCheckResult.Warning,
                "Legacy library detected — Open Library reidentification will run automatically on startup. Restart Librarr if migration does not begin within ~30s.",
                "#legacy-migration-pending");
        }
    }
}
