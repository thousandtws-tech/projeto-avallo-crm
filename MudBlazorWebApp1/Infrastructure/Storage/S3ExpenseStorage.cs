using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace MudBlazorWebApp1.Features.Expenses;

public interface IExpenseStorage
{
    Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken);
    string CreateDownloadUrl(string objectKey, string fileName);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class S3ExpenseStorage : IExpenseStorage, IDisposable
{
    private readonly ObjectStorageOptions _options;
    private readonly AmazonS3Client _client;

    public S3ExpenseStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        AWSConfigsS3.UseSignatureVersion4 = true;
        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            AuthenticationRegion = _options.Region,
            ForcePathStyle = false
        };
        _client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = false,
            UseChunkEncoding = false
        }, cancellationToken);
    }

    public string CreateDownloadUrl(string objectKey, string fileName)
    {
        EnsureEnabled();
        return _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddMinutes(_options.DownloadUrlMinutes),
            Verb = HttpVerb.GET,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{fileName.Replace("\"", string.Empty)}\""
            }
        });
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        await _client.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Object storage is not configured.");
    }

    public void Dispose() => _client.Dispose();
}
