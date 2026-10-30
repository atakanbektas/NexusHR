using Minio;
using Minio.DataModel.Args;
using NexusHR.Candidate.Application.Abstractions.Storage;

namespace NexusHR.Candidate.Infrastructure.Storage;

internal sealed class MinioCandidateDocumentStorage(
    IMinioClient minioClient,
    MinioStorageOptions options, NexusHR.Candidate.Application.CvAnalyses.ICvDocumentScanner scanner)
    : ICandidateDocumentStorage
{
    private readonly SemaphoreSlim bucketLock = new(1, 1);

    public async Task UploadAsync(
        string objectName,
        byte[] fileContent,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        await using var stream = new MemoryStream(
            fileContent,
            writable: false);

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await minioClient.PutObjectAsync(
            putObjectArgs,
            cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(
        string objectName,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        await using var memoryStream = new MemoryStream();

        var getObjectArgs = new GetObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                var buffer = new byte[8192]; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (memoryStream.Length + count > 5 * 1024 * 1024) throw new InvalidDataException("Stored CV exceeds size limit.");
                    memoryStream.Write(buffer, 0, count);
                }
            });

        await minioClient.GetObjectAsync(
            getObjectArgs,
            cancellationToken);

        var content = memoryStream.ToArray(); await scanner.ScanAsync(content, cancellationToken); return content;
    }

    public async Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var removeObjectArgs = new RemoveObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(objectName);

        await minioClient.RemoveObjectAsync(
            removeObjectArgs,
            cancellationToken);
    }

    private async Task EnsureBucketExistsAsync(
        CancellationToken cancellationToken)
    {
        await bucketLock.WaitAsync(cancellationToken);

        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(options.BucketName);

            var bucketExists =
                await minioClient.BucketExistsAsync(
                    bucketExistsArgs,
                    cancellationToken);

            if (bucketExists)
            {
                return;
            }

            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(options.BucketName);

            await minioClient.MakeBucketAsync(
                makeBucketArgs,
                cancellationToken);
        }
        finally
        {
            bucketLock.Release();
        }
    }
}
