using RadioPremium.Core.Services;
using System.Runtime.InteropServices;
using System.Text;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Secure storage service using Windows Credential Manager
/// </summary>
public sealed class SecureStorageService : ISecureStorageService
{
    private const string CredentialPrefix = "RadioPremium_";

    public Task SetAsync(string key, string value)
    {
        var targetName = $"{CredentialPrefix}{key}";

        var credential = new CREDENTIAL
        {
            Type = CRED_TYPE.GENERIC,
            TargetName = targetName,
            CredentialBlobSize = (uint)Encoding.UTF8.GetByteCount(value),
            CredentialBlob = Marshal.StringToCoTaskMemUni(value),
            Persist = CRED_PERSIST.LOCAL_MACHINE,
            UserName = Environment.UserName
        };

        try
        {
            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"Error al guardar credencial: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(credential.CredentialBlob);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        var targetName = $"{CredentialPrefix}{key}";

        if (!CredRead(targetName, CRED_TYPE.GENERIC, 0, out var credentialPtr))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero)
            {
                return Task.FromResult<string?>(null);
            }

            var value = Marshal.PtrToStringUni(credential.CredentialBlob);
            return Task.FromResult(value);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public Task RemoveAsync(string key)
    {
        var targetName = $"{CredentialPrefix}{key}";
        CredDelete(targetName, CRED_TYPE.GENERIC, 0);
        return Task.CompletedTask;
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        var value = await GetAsync(key);
        return value is not null;
    }

    public Task ClearAllAsync()
    {
        // Enumerate and delete all credentials with our prefix
        if (CredEnumerate($"{CredentialPrefix}*", 0, out var count, out var credentialsPtr))
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var credPtr = Marshal.ReadIntPtr(credentialsPtr, i * IntPtr.Size);
                    var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                    CredDelete(credential.TargetName, CRED_TYPE.GENERIC, 0);
                }
            }
            finally
            {
                CredFree(credentialsPtr);
            }
        }

        return Task.CompletedTask;
    }

    #region Native Methods

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string targetName, CRED_TYPE type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string targetName, CRED_TYPE type, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerate(string filter, uint flags, out int count, out IntPtr credentials);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);

    private enum CRED_TYPE : uint
    {
        GENERIC = 1,
    }

    private enum CRED_PERSIST : uint
    {
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CRED_TYPE Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CRED_PERSIST Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    #endregion
}
