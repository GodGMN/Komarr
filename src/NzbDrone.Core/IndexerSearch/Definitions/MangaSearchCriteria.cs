namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class MangaSearchCriteria : AuthorSearchCriteria
    {
        public string MangaTitle { get; set; }

        public override string ToString()
        {
            return $"[Manga: {MangaTitle}]";
        }
    }
}
