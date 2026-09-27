namespace NzbDrone.Core.Qualities
{
    // Which kind of thing a Quality actually is. Librarr has always had both
    // ebook and audiobook qualities in one enumeration, distinguished only by
    // convention: ids 0-4 are text, 10-13 are audio, and 5-9 were left as a
    // gap. Nothing enforced that convention or let code ask the question, so
    // an EPUB and an M4B were indistinguishable to the decision engine and
    // ended up ranked against each other in a single quality profile.
    //
    // Deliberately not persisted anywhere: it is derived from the quality id,
    // so there is nothing to migrate and nothing that can drift out of sync.
    public enum QualityFormat
    {
        Text = 0,
        Audio = 1
    }
}
