using Amazon.S3;
using Amazon.S3.Model;
using Averio.Application.Receipts;
using Microsoft.Extensions.Options;
namespace Averio.Infrastructure.Receipts;

public sealed class S3ReceiptDocumentStore(IAmazonS3 client, IOptions<ReceiptStorageOptions> options) : IReceiptDocumentStore
{
    public async Task PutAsync(string key, byte[] jpeg, CancellationToken ct)
    {
        using var stream = new MemoryStream(jpeg, writable: false);
        await client.PutObjectAsync(new PutObjectRequest {
            BucketName = options.Value.Bucket, Key = key, InputStream = stream,
            ContentType = "image/jpeg", ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        }, ct);
    }
    public async Task<byte[]> GetAsync(string key, CancellationToken ct)
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
    }
    public async Task DeleteAsync(string key, CancellationToken ct) => await client.DeleteObjectAsync(options.Value.Bucket, key, ct);
}
