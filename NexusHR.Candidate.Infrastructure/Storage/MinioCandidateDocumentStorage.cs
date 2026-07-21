using Minio;
using Minio.DataModel.Args;
using NexusHR.Candidate.Application.Abstractions.Storage;

namespace NexusHR.Candidate.Infrastructure.Storage;

internal sealed class MinioCandidateDocumentStorage(
    IMinioClient minioClient,
    MinioStorageOptions options)
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
                stream.CopyTo(memoryStream);
            });

        await minioClient.GetObjectAsync(
            getObjectArgs,
            cancellationToken);

        return memoryStream.ToArray();
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