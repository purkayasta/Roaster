namespace Roaster.Editor.Services.Dtos;

public class RoastComment
{
    public long SubmissionId { get; set; }
    public long ReviewId { get; set; }
    public int LineNumber { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string Severity { get; set; } = "Medium";
}
