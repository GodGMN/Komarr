using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Qualities
{
    public static class QualityFormatExtensions
    {
        public static QualityFormat Format(this QualityModel quality)
        {
            return quality?.Quality?.Format ?? QualityFormat.Text;
        }

        // Restrict a book's existing files to the ones that actually compete
        // with a release of this format.
        //
        // This is the half of per-format profiles that is easy to miss.
        // Giving an author a second quality profile changes which ranking a
        // release is judged against, but the upgrade and cutoff decisions
        // also walk every existing file for the book — so without this an
        // incoming M4B would still be compared against, and could still
        // replace, an EPUB the user already has. An ebook and an audiobook
        // are not alternatives for one slot; they are separate slots.
        public static IEnumerable<BookFile> OfFormat(this IEnumerable<BookFile> files, QualityFormat format)
        {
            return files.Where(f => f != null && f.Quality.Format() == format);
        }

        public static IEnumerable<BookFile> MatchingFormat(this IEnumerable<BookFile> files, QualityModel quality)
        {
            return files.OfFormat(quality.Format());
        }
    }
}
