using System.Runtime.InteropServices;

namespace NexusScholar.Search.Providers.Live;

public interface IProviderCredentialResolver
{
    string? Resolve(string providerAlias);
}

public sealed class CompositeProviderCredentialResolver : IProviderCredentialResolver
{
    private readonly IReadOnlyList<IProviderCredentialResolver> resolvers;

    public CompositeProviderCredentialResolver(params IProviderCredentialResolver[] resolvers)
    {
        this.resolvers = (resolvers ?? throw new ArgumentNullException(nameof(resolvers))).ToArray();
    }

    public string? Resolve(string providerAlias)
    {
        foreach (var resolver in resolvers)
        {
            var credential = resolver.Resolve(providerAlias);
            if (!string.IsNullOrWhiteSpace(credential))
            {
                return credential;
            }
        }

        return null;
    }

    public static CompositeProviderCredentialResolver CreateDefault() =>
        new(new WindowsCredentialManagerResolver(), new EnvironmentProviderCredentialResolver());
}

public sealed class EnvironmentProviderCredentialResolver : IProviderCredentialResolver
{
    public string? Resolve(string providerAlias)
    {
        var variable = providerAlias switch
        {
            "openalex" => "OPENALEX_API_KEY",
            "semantic_scholar" => "S2_API_KEY",
            _ => null
        };
        return variable is null ? null : Environment.GetEnvironmentVariable(variable);
    }
}

public sealed class WindowsCredentialManagerResolver : IProviderCredentialResolver
{
    private const int GenericCredentialType = 1;

    public string? Resolve(string providerAlias)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var target = providerAlias switch
        {
            "openalex" => "NexusScholar.OpenAlex",
            "semantic_scholar" => "NexusScholar.SemanticScholar",
            _ => null
        };
        if (target is null || !CredRead(target, GenericCredentialType, 0, out var pointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            return credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0
                ? null
                : Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / sizeof(char)));
        }
        finally
        {
            CredFree(pointer);
        }
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        int type,
        int flags,
        out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPointer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
