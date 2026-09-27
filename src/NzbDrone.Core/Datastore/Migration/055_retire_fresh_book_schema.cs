using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(055)]
    public class retire_fresh_book_schema : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(RetireBookTablesOnFreshInstall);
        }

        private static void RetireBookTablesOnFreshInstall(IDbConnection connection, IDbTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT \"Value\" FROM \"Config\" WHERE \"Key\" = 'KomarrFreshSchema'";

            if (command.ExecuteScalar() == null)
            {
                return;
            }

            // Earlier migrations need these tables. Drop them only after the
            // manga schema is complete, and only for databases marked above.
            foreach (var table in new[]
                     {
                         "EditionNarrators", "Narrators", "SeriesBookLink", "BookFiles",
                         "Editions", "Books", "Series", "Authors", "AuthorMetadata", "BookIdMapping"
                     })
            {
                command.CommandText = $"DROP TABLE IF EXISTS \"{table}\"";
                command.ExecuteNonQuery();
            }

            command.CommandText = "DELETE FROM \"Config\" WHERE \"Key\" = 'KomarrFreshSchema'";
            command.ExecuteNonQuery();
        }
    }
}
