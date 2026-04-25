namespace Editor.Services.Dtos;

/// <summary>
/// SubmissionRequest DTO
///
/// This is the data transfer object sent from the UI to the backend API
/// when a user wants to submit code for roasting.
///
/// DTOs are simple data containers that define the contract between
/// frontend and backend. They make it easy to:
/// - Validate data
/// - Serialize/deserialize JSON
/// - Change backend implementation without affecting UI
/// - Version APIs independently
/// </summary>
public class SubmissionRequest
{
    /// <summary>
    /// The complete source code to be roasted.
    /// Can be any length (though in practice, limited by API).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The programming language of the code.
    /// Examples: "csharp", "javascript", "python", "java", etc.
    ///
    /// The backend uses this to:
    /// - Apply language-specific linting/analysis
    /// - Generate language-aware comments
    /// - Route to specialized AI models
    /// </summary>
    public string Language { get; set; } = "csharp";
}
