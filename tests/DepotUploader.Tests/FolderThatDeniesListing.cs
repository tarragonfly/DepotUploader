using System.Security.AccessControl;
using System.Security.Principal;

namespace DepotUploader.Tests;

internal sealed class FolderThatDeniesListing : IDisposable
{
    private readonly DirectoryInfo _folder;
    private readonly FileSystemAccessRule _denyListing = new FileSystemAccessRule(
        WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);

    public FolderThatDeniesListing(string folderPath)
    {
        _folder = Directory.CreateDirectory(folderPath);
        DirectorySecurity security = _folder.GetAccessControl();
        security.AddAccessRule(_denyListing);
        _folder.SetAccessControl(security);
    }

    public void Dispose()
    {
        DirectorySecurity security = _folder.GetAccessControl();
        security.RemoveAccessRule(_denyListing);
        _folder.SetAccessControl(security);
    }
}
