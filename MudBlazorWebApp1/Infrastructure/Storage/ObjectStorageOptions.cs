using System.ComponentModel.DataAnnotations;

namespace MudBlazorWebApp1.Features.Expenses;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";
    public bool Enabled { get; init; }
    [Required] public string ServiceUrl { get; init; } = string.Empty;
    [Required] public string Region { get; init; } = "auto";
    [Required] public string Bucket { get; init; } = string.Empty;
    [Required] public string AccessKey { get; init; } = string.Empty;
    [Required] public string SecretKey { get; init; } = string.Empty;
    [Range(1, 60)] public int DownloadUrlMinutes { get; init; } = 10;
}
