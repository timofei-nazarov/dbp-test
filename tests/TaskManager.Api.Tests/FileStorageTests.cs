using Microsoft.Extensions.Configuration;
using TaskManager.Infrastructure.Services;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class FileStorageTests
{
    [Fact]
    public async Task SaveAvatarWritesStreamUnderLowercasedExtensionKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = directory })
                .Build();
            using var storage = new FileStorage(configuration, Path.GetTempPath());
            using var content = new MemoryStream([1, 2, 3]);
            var key = await storage.SaveAvatarAsync(content, "Photo.PNG", CancellationToken.None);
            Assert.Matches(@"^[0-9a-f]{32}\.png$", key);
            Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(directory, key)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RootFallsBackToMediaUnderContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            using var storage = new FileStorage(configuration, contentRoot);
            Assert.Equal(Path.Combine(contentRoot, "media"), storage.Root);
        }
        finally
        {
            Directory.Delete(contentRoot, true);
        }
    }
}
