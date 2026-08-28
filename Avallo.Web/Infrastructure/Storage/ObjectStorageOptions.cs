using System.ComponentModel.DataAnnotations;

namespace Avallo.Web.Features.Expenses;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";
    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string ContainerName { get; init; } = "uploads";
    [Range(1, 60)] public int DownloadUrlMinutes { get; init; } = 10;
}
