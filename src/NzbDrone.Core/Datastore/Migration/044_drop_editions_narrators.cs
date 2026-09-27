using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(044)]
    public class drop_editions_narrators : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Migration 042 added Editions.Narrators as a flat comma-separated
            // staging column. Migration 043 introduced the normalized
            // Narrators + EditionNarrators schema and RefreshEditionService
            // started syncing the flat column into the join on every refresh.
            // This migration removes the legacy column now that the data
            // path is normalized end-to-end:
            //
            //   * AudnexProxy.Augment writes a LazyLoaded<List<Narrator>>
            //     directly to edition.NarratorList.
            //   * RefreshEditionService syncs that list to Narrators +
            //     EditionNarrators after edition persistence.
            //   * EditionResource projects the lazy-loaded list as a
            //     comma-joined string for the API (frontend unchanged).
            //
            // Existing data: migration 043 + the RefreshEditionService sync
            // wired in commit 19e5092 ensure every populated Editions.Narrators
            // row already has matching Narrators / EditionNarrators rows by
            // the time a user is on a build that runs this migration. No
            // data is lost.
            //
            // No down-migration: re-creating the column is the easy half;
            // re-deriving the comma-joined strings from the join is also
            // easy (a single SQL join + GROUP_CONCAT), but the audnex
            // augmenter no longer writes the column, so a downgraded
            // binary would still see narrators only via the lazy-loaded
            // list and the recreated column would go immediately stale.
            // Roll back by restoring a pre-044 SQLite backup instead.
            Delete.Column("Narrators").FromTable("Editions");
        }
    }
}
