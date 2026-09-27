using NzbDrone.Core.Books.Model;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideSeriesInfo
    {
        SeriesInfo GetSeriesInfo(string foreignSeriesId, bool useCache = true);
    }
}
