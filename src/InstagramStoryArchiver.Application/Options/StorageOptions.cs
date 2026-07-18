using System.ComponentModel.DataAnnotations;

namespace InstagramStoryArchiver.Application.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string ArchiveRootPath { get; set; } = "archive";

    [Required]
    public string TemporaryRootPath { get; set; } = "data/tmp";

    [Required]
    public string LockFilePath { get; set; } = "data/worker.lock";
}
