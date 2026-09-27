using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class book_id_mapping : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Bridge table populated by Phase 5's reidentify command. Each row
            // records "this Goodreads ID corresponds to this Open Library
            // work/edition with this confidence." Reverse-lookup by GoodreadsId
            // dominates the access pattern (during legacy data migration).
            Create.Table("BookIdMapping")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("GoodreadsId").AsString().NotNullable()
                .WithColumn("OpenLibraryWorkId").AsString().Nullable()
                .WithColumn("OpenLibraryEditionId").AsString().Nullable()
                .WithColumn("Confidence").AsDouble().NotNullable()
                .WithColumn("Source").AsString().NotNullable()
                .WithColumn("CreatedUtc").AsDateTime().NotNullable();

            Create.Index().OnTable("BookIdMapping").OnColumn("GoodreadsId").Ascending();
            Create.Index().OnTable("BookIdMapping").OnColumn("OpenLibraryWorkId").Ascending();
        }
    }
}
