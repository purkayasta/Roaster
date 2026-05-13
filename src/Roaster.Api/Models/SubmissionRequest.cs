using System.ComponentModel.DataAnnotations;

namespace Roaster.Api.Models;

public sealed class SubmissionRequest
{
    [Required]
    public Guid SessionId { get; init; }

    [Required]
    [StringLength(50000, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [AllowedValues("csharp", "javascript", "typescript", "python", "java", "go", "rust", "cpp", "sql")]
    public string Language { get; init; } = string.Empty;
}
