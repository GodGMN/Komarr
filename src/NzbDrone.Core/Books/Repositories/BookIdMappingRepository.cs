using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public interface IBookIdMappingRepository : IBasicRepository<BookIdMapping>
    {
        BookIdMapping FindByGoodreadsId(string goodreadsId);
        List<BookIdMapping> FindByGoodreadsIds(List<string> goodreadsIds);
        BookIdMapping FindByOpenLibraryWorkId(string openLibraryWorkId);
        BookIdMapping FindByOpenLibraryAuthorId(string openLibraryAuthorId);
        List<BookIdMapping> GetLowConfidence(double threshold);
    }

    public class BookIdMappingRepository : BasicRepository<BookIdMapping>, IBookIdMappingRepository
    {
        public BookIdMappingRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public BookIdMapping FindByGoodreadsId(string goodreadsId)
        {
            return Query(x => x.GoodreadsId == goodreadsId).SingleOrDefault();
        }

        public List<BookIdMapping> FindByGoodreadsIds(List<string> goodreadsIds)
        {
            return Query(x => goodreadsIds.Contains(x.GoodreadsId));
        }

        public BookIdMapping FindByOpenLibraryWorkId(string openLibraryWorkId)
        {
            // Reverse lookup used by MetadataSourceFactory to restore the
            // local GoodReads-shaped ForeignBookId on returned remote books
            // so RefreshAuthorService can still correlate them to the
            // existing DB rows (which carry the GoodReads numeric id).
            return Query(x => x.OpenLibraryWorkId == openLibraryWorkId).FirstOrDefault();
        }

        public BookIdMapping FindByOpenLibraryAuthorId(string openLibraryAuthorId)
        {
            // Author mappings are written by ReidentifyService.MapAuthor
            // with OpenLibraryWorkId=null and the OL author OLID stuffed
            // into OpenLibraryEditionId (awkward column reuse, but that's
            // the convention the migration locked in). Match on that
            // field + the null work id so we don't accidentally cross
            // with book-shaped mappings.
            return Query(x => x.OpenLibraryEditionId == openLibraryAuthorId && x.OpenLibraryWorkId == null).FirstOrDefault();
        }

        public List<BookIdMapping> GetLowConfidence(double threshold)
        {
            // Surfaced by the Phase 5 wizard's "needs manual review" step.
            return Query(x => x.Confidence < threshold);
        }
    }
}
