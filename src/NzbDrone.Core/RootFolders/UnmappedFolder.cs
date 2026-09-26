namespace NzbDrone.Core.RootFolders
{
    // A first-level subdirectory of a root folder that no author currently
    // occupies. Populated by RootFolderService.GetUnmappedFolders and consumed
    // by the Library Import wizard, which pairs each one with an OpenLibrary
    // author so an existing on-disk collection can be adopted in bulk.
    public class UnmappedFolder
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
