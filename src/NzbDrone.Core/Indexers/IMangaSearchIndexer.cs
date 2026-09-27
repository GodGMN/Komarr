namespace NzbDrone.Core.Indexers
{
    // Marks indexers that can answer a manga title query through
    // IIndexer.Fetch(AuthorSearchCriteria) or Fetch(BookSearchCriteria).
    // Sources that only implement recent-feed fetching must not implement this.
    public interface IMangaSearchIndexer
    {
    }
}
