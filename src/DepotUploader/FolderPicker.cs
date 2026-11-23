using System.Runtime.InteropServices;

namespace DepotUploader;

internal static class FolderPicker
{
    private const uint PickFoldersOption = 0x20;
    private const uint FileSystemOnlyOption = 0x40;
    private const uint FileSystemPathName = 0x80058000;

    public static string? Show(IWin32Window owner, string title, string startFolder)
    {
        IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialog();
        try
        {
            dialog.GetOptions(out uint options);
            dialog.SetOptions(options | PickFoldersOption | FileSystemOnlyOption);
            dialog.SetTitle(title);
            Guid shellItemInterfaceId = typeof(IShellItem).GUID;
            if (Directory.Exists(startFolder) && SHCreateItemFromParsingName(Path.GetFullPath(startFolder),
                    IntPtr.Zero, ref shellItemInterfaceId, out IShellItem startFolderItem) == 0)
            {
                dialog.SetFolder(startFolderItem);
            }

            if (dialog.Show(owner.Handle) != 0)
            {
                return null;
            }

            dialog.GetResult(out IShellItem pickedItem);
            pickedItem.GetDisplayName(FileSystemPathName, out string pickedFolder);

            return pickedFolder;
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid interfaceId,
        out IShellItem shellItem);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialog;

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr handler);

        void GetParent(out IShellItem parent);

        void GetDisplayName(uint nameKind, [MarshalAs(UnmanagedType.LPWStr)] out string name);

        void GetAttributes(uint mask, out uint attributes);

        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig]
        int Show(IntPtr owner);

        void SetFileTypes(uint count, IntPtr filterSpec);

        void SetFileTypeIndex(uint index);

        void GetFileTypeIndex(out uint index);

        void Advise(IntPtr events, out uint cookie);

        void Unadvise(uint cookie);

        void SetOptions(uint options);

        void GetOptions(out uint options);

        void SetDefaultFolder(IShellItem folder);

        void SetFolder(IShellItem folder);

        void GetFolder(out IShellItem folder);

        void GetCurrentSelection(out IShellItem item);

        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

        void GetResult(out IShellItem item);
    }
}
