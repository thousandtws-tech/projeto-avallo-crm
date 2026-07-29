using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace MudBlazorWebApp1.Features.Expenses;

public interface IExpenseStorage
{
    Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken);
    string CreateDownloadUrl(string objectKey, string fileName);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class BlobExpenseStorage : IExpenseStorage, IDisposable
{
    private readonly ObjectStorageOptions _options;
    private readonly BlobContainerClient _containerClient;
    private readonly StorageSharedKeyCredential? _sharedKeyCredential;

    public BlobExpenseStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ServiceUrl))
            throw new InvalidOperationException("ObjectStorage:ServiceUrl is required for blob storage.");

        var endpoint = new Uri(_options.ServiceUrl);

        // If SecretKey provided and account name can be inferred from endpoint host, use shared key credential
        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            try
            {
                var host = endpoint.Host; // e.g. account.blob.core.windows.net
                var account = host.Split('.')[0];
                _sharedKeyCredential = new StorageSharedKeyCredential(account, _options.SecretKey);
                var serviceClient = new BlobServiceClient(endpoint, _sharedKeyCredential);
                _containerClient = serviceClient.GetBlobContainerClient(_options.Bucket);
            }
            catch
            {
                // fallback to DefaultAzureCredential
                var serviceClient = new BlobServiceClient(endpoint, new DefaultAzureCredential());
                _containerClient = serviceClient.GetBlobContainerClient(_options.Bucket);
            }
        }
        else
        {
            var serviceClient = new BlobServiceClient(endpoint, new DefaultAzureCredential());
            _containerClient = serviceClient.GetBlobContainerClient(_options.Bucket);
        }

        // Ensure container exists (no-op if it already exists)
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var blobClient = _containerClient.GetBlobClient(objectKey);
        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
    }

    public string CreateDownloadUrl(string objectKey, string fileName)
    {
        EnsureEnabled();
        var blobClient = _containerClient.GetBlobClient(objectKey);

        // Prefer shared key SAS if available
        if (_sharedKeyCredential is not null)
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerClient.Name,
                BlobName = objectKey,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.DownloadUrlMinutes)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sas = sasBuilder.ToSasQueryParameters(_sharedKeyCredential).ToString();
            var uri = new UriBuilder(blobClient.Uri) { Query = sas };

            // Attach content disposition via response header parameter
            // Azure SAS supports response-content-disposition as query parameter
            var cd = System.Web.HttpUtility.UrlEncode($"attachment; filename=\"{fileName.Replace("\"", string.Empty)}\"");
            var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
            return uri.Uri + separator + "response-content-disposition=" + cd;
        }

        // Fallback: return direct blob URI (may be inaccessible without auth)
        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        await _containerClient.DeleteBlobIfExistsAsync(objectKey, cancellationToken: cancellationToken);
    }

    private void EnsureEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Object storage is not configured.");
    }

    public void Dispose()
    {
        // Blob clients do not require disposal, but implement IDisposable for parity
    }
}
