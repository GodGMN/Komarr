using System.Collections.Generic;

namespace NzbDrone.Core.Books.Model
{
    public class ListInfo
    {
        public string ForeignListId { get; set; }
        public int Page { get; set; }
        public int PerPage { get; set; }
        public int TotalBooks { get; set; }
        public List<BookListItem> Books { get; set; } = new List<BookListItem>();
    }
}
