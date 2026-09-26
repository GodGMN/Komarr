using System.Collections.Generic;

namespace NzbDrone.Core.Books.Model
{
    public class SeriesInfo
    {
        public string ForeignSeriesId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<BookListItem> Books { get; set; } = new List<BookListItem>();
    }

    public class BookListItem
    {
        public string ForeignBookId { get; set; }
        public string Title { get; set; }
        public string ForeignEditionId { get; set; }
        public string AuthorName { get; set; }
        public string ForeignAuthorId { get; set; }
        public string Position { get; set; }
    }
}
