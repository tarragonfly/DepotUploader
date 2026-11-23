using System.Security.AccessControl;
using System.Security.Principal;

namespace DepotUploader.Tests;

internal static class TestFileMetadata
{
    public static readonly byte[] AlternateStreamBytes = { 1, 2, 3, 250, 251, 252 };

    public static string GiveOwnAccessRules(string filePath)
    {
        FileInfo file = new FileInfo(filePath);
        FileSecurity security = file.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(security);

        return AccessRulesOf(filePath);
    }

    public static string AccessRulesOf(string filePath)
    {
        return new FileInfo(filePath).GetAccessControl(AccessControlSections.Access)
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
    }

    public static void WriteAlternateStream(string filePath)
    {
        File.WriteAllBytes(AlternateStreamPath(filePath), AlternateStreamBytes);
    }

    public static byte[] ReadAlternateStream(string filePath)
    {
        return File.ReadAllBytes(AlternateStreamPath(filePath));
    }

    public static bool TryEncrypt(string filePath)
    {
        try
        {
            File.Encrypt(filePath);

            return File.GetAttributes(filePath).HasFlag(FileAttributes.Encrypted);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string AlternateStreamPath(string filePath)
    {
        return filePath + ":note";
    }
}
