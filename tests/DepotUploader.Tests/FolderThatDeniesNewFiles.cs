using System.Security.AccessControl;
using System.Security.Principal;

namespace DepotUploader.Tests;

internal sealed class FolderThatDeniesNewFiles : IDisposable
{
    private readonly DirectoryInfo _folder;
    private readonly FileSystemAccessRule _denyNewFiles = new FileSystemAccessRule(
        WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles, AccessControlType.Deny);

    public FolderThatDeniesNewFiles(string folderPath)
    {
        _folder = new DirectoryInfo(folderPath);
        DirectorySecurity security = _folder.GetAccessControl();
        security.AddAccessRule(_denyNewFiles);
        _folder.SetAccessControl(security);
    }

    public void Dispose()
    {
        DirectorySecurity security = _folder.GetAccessControl();
        security.RemoveAccessRule(_denyNewFiles);
        _folder.SetAccessControl(security);
    }
}
