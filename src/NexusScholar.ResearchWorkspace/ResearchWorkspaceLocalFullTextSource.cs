namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceLocalFullTextSource
{
    public static byte[] ReadBytes(string localPath, long maximumBytes)
    {
        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("A local Full Text path is required.", nameof(localPath));
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        var supplied = localPath.Trim();
        if (Uri.TryCreate(supplied, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile || uri.IsUnc)
                throw new InvalidOperationException("Full Text local intake rejects network URLs and network file references.");
            supplied = uri.LocalPath;
        }
        else if (supplied.StartsWith("\\\\", StringComparison.Ordinal) || supplied.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Full Text local intake rejects network file references.");
        }

        var file = new FileInfo(supplied);
        if (!file.Exists)
            throw new FileNotFoundException("The local Full Text input does not exist.", supplied);
        if (file.Length > maximumBytes)
            throw new InvalidOperationException("The local Full Text input exceeds the configured byte limit.");
        var bytes = File.ReadAllBytes(file.FullName);
        if (bytes.LongLength > maximumBytes)
            throw new InvalidOperationException("The local Full Text input exceeds the configured byte limit.");
        return bytes;
    }
}
