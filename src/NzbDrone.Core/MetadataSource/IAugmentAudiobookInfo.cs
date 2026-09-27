using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource
{
    // Phase 7 augmenter. Composable with IProvideBookInfo — not a replacement.
    // RefreshBookService is expected to call CanAugment() after fetching the
    // book from the primary metadata source; if true, the augmenter merges
    // audiobook-specific fields (narrator, ASIN-keyed cover, duration) into
    // the Book that the primary source produced.
    public interface IAugmentAudiobookInfo
    {
        bool CanAugment(Book book);
        Book Augment(Book book);
    }
}
