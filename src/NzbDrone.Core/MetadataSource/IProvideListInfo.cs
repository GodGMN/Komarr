using NzbDrone.Core.Books.Model;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideListInfo
    {
        ListInfo GetListInfo(string foreignListId, int page, bool useCache = true);
    }
}
