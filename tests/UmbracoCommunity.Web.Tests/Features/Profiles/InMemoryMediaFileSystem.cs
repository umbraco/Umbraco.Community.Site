using Moq;
using Umbraco.Cms.Core.IO;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>A media file system kept in memory, served under <c>/media/</c>.</summary>
internal sealed class InMemoryMediaFileSystem
{
    public InMemoryMediaFileSystem()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns((string path) => Files.ContainsKey(path));
        fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns((string path) => Files.Keys.Any(k => k.StartsWith(path + "/", StringComparison.Ordinal)));
        fileSystem.Setup(f => f.GetFiles(It.IsAny<string>()))
            .Returns((string path) => Files.Keys.Where(k => k.StartsWith(path + "/", StringComparison.Ordinal)).ToList());
        fileSystem.Setup(f => f.GetUrl(It.IsAny<string>())).Returns((string path) => "/media/" + path);
        fileSystem.Setup(f => f.AddFile(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
            .Callback((string path, Stream stream, bool _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                Files[path] = copy.ToArray();
            });
        FileSystem = fileSystem.Object;
    }

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public IFileSystem FileSystem { get; }
}
