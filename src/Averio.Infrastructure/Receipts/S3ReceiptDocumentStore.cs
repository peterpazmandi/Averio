using Amazon.S3;
using Amazon.S3.Model;
using Averio.Application.Receipts;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
namespace Averio.Infrastructure.Receipts;

public sealed class S3ReceiptDocumentStore(IAmazonS3 client, IOptions<ReceiptStorageOptions> options, ILogger<S3ReceiptDocumentStore>? logger = null) : IReceiptDocumentStore
{
    private readonly ILogger<S3ReceiptDocumentStore> log = logger ?? NullLogger<S3ReceiptDocumentStore>.Instance;
    public async Task PutAsync(string key, byte[] jpeg, CancellationToken ct)
        => await RunAsync("write", async () =>
    {
        using var stream = new MemoryStream(jpeg, writable: false);
        await client.PutObjectAsync(new PutObjectRequest {
            BucketName = options.Value.Bucket, Key = key, InputStream = stream,
            ContentType = "image/jpeg", ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        }, ct);
        return 0;
    });
    public async Task<byte[]> GetAsync(string key, CancellationToken ct)
        => await RunAsync("read", async () =>
    {
        using var response = await client.GetObjectAsync(options.Value.Bucket, key, ct);
        if (response.ContentLength > ReceiptOptions.MaxUploadBytes) throw new ReceiptExtractionException("document_invalid", false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await response.ResponseStream.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > ReceiptOptions.MaxUploadBytes) throw new ReceiptExtractionException("document_invalid", false);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    });
    public async Task DeleteAsync(string key, CancellationToken ct) => await RunAsync("delete", async () => { await client.DeleteObjectAsync(options.Value.Bucket, key, ct); return 0; });
    private async Task<T> RunAsync<T>(string operation, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        log.LogDebug("Receipt object storage request started: Operation={Operation}", operation);
        try
        {
            var result = await action();
            log.LogDebug("Receipt object storage request completed: Operation={Operation}, ElapsedMs={ElapsedMs}", operation, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (AmazonS3Exception ex)
        {
            // Do not log exception messages: SDK messages can include bucket names,
            // object keys, request URLs and provider response bodies.
            var code = ex.ErrorCode switch
            {
                "AccessDenied" or "NoSuchBucket" or "NoSuchKey" or "InvalidAccessKeyId"
                    or "SignatureDoesNotMatch" or "ExpiredToken" or "SlowDown" or "RequestTimeout" => ex.ErrorCode,
                _ => "other"
            };
            log.LogWarning("Receipt object storage request failed: Operation={Operation}, HttpStatus={HttpStatus}, StorageCode={StorageCode}, ElapsedMs={ElapsedMs}", operation, (int)ex.StatusCode, code, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
