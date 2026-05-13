using System.ComponentModel.DataAnnotations;

namespace Roaster.Api.Models;

public class ReplyRequest
{
    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}
