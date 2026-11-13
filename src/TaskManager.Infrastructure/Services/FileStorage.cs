using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace TaskManager.Infrastructure.Services;

public sealed class FileStorage : IDisposable
{
    private readonly string root;
    private readonly string? bucket;
    private readonly string region;
    private readonly AmazonS3Client? s3;

    public FileStorage(IConfiguration configuration, string contentRootPath)
    {
        root = Path.GetFullPath(configuration["Storage:Root"] ?? Path.Combine(contentRootPath, "media"));
        bucket = configuration["Storage:Bucket"];
        region = configuration["Storage:Region"] ?? "us-east-1";
        if (!string.IsNullOrWhiteSpace(bucket))
        {
            s3 = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
        }
        else
        {
            Directory.CreateDirectory(root);
        }
    }

    public string Root => root;
    public bool IsLocal => s3 is null;

    public async Task<string> SaveAvatarAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var key = $"{Guid.NewGuid():N}{extension}";
        await SaveAsync(key, content, true, cancellationToken);
        return key;
    }

    public async Task<string> SaveReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = $"test_report-{id}.data";
        await using var stream = new MemoryStream("test data"u8.ToArray());
        await SaveAsync(key, stream, false, cancellationToken);
        if (s3 is null) return $"/media/{key}";
        return await s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(10)
        });
    }

    public string PublicUrl(string key) => s3 is null
        ? $"/media/{Uri.EscapeDataString(key)}"
        : $"https://{bucket}.s3.{region}.amazonaws.com/{Uri.EscapeDataString(key)}";

    private async Task SaveAsync(string key, Stream stream, bool isPublic, CancellationToken cancellationToken)
    {
        if (s3 is not null)
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                CannedACL = isPublic ? S3CannedACL.PublicRead : S3CannedACL.Private
            }, cancellationToken);
            return;
        }
        await using var target = File.Create(Path.Combine(root, key));
        await stream.CopyToAsync(target, cancellationToken);
    }

    public void Dispose() => s3?.Dispose();
}
