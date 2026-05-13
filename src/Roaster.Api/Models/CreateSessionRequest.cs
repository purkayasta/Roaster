using System.ComponentModel.DataAnnotations;

namespace Roaster.Api.Models;

public sealed class CreateSessionRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;
}
