using Editor.Services.Dtos;

namespace Editor.Services;

/// <summary>
/// IRoastApiService Interface
///
/// This interface defines the contract for all API communication related to code roasting.
/// It follows the Dependency Injection pattern, which means:
///
/// 1. The interface is injected into components (e.g., CodeWorkspace.razor)
/// 2. The implementation can be swapped without changing component code
/// 3. We can use a MockRoastApiService for development
/// 4. We can use a real HttpClient-based service for production
///
/// Benefits:
/// - Testable: Components don't know if they're using mock or real service
/// - Maintainable: Service logic is centralized
/// - Flexible: Easy to add caching, logging, error handling
/// - Mockable: Perfect for unit testing without a backend
///
/// Example of how DI works:
///   In Program.cs:
///     builder.Services.AddScoped<IRoastApiService, MockRoastApiService>();
///
///   In CodeWorkspace.razor:
///     @inject IRoastApiService RoastApiService
///
///   Now RoastApiService is available in the component and will use MockRoastApiService!
/// </summary>
public interface IRoastApiService
{
    /// <summary>
    /// Submits source code to be roasted by the backend AI.
    ///
    /// This is the main entry point for getting AI-generated roast comments.
    /// The backend analyzes the code and returns a list of sarcastic comments
    /// positioned at specific line numbers.
    ///
    /// Behavior:
    /// - Takes a submission with code and language
    /// - Sends it to the backend API (or simulates it with MockService)
    /// - Returns a list of roast comments positioned on specific lines
    /// - Can be canceled via CancellationToken
    ///
    /// Example usage in CodeWorkspace.razor:
    ///     var request = new SubmissionRequest
    ///     {
    ///         Code = editorCode,
    ///         Language = "csharp"
    ///     };
    ///     var roasts = await RoastApiService.SubmitCodeAsync(request);
    ///     // roasts now contains comments like:
    ///     // Line 3: "Wow, declaring a variable that's never used. Bold strategy."
    ///     // Line 5: "This regex could match anything. Including your patience."
    ///
    /// Exception handling:
    /// - Should throw HttpRequestException on network errors (in real service)
    /// - Should throw OperationCanceledException if canceled
    /// - Should handle timeout gracefully
    ///
    /// Performance notes:
    /// - MockService simulates 2-second network latency via Task.Delay
    /// - Real service will vary based on backend complexity
    /// - Should ideally take 1-10 seconds depending on code complexity
    /// </summary>
    Task<List<RoastComment>> SubmitCodeAsync(SubmissionRequest request);

    /// <summary>
    /// Posts a user's reply to a specific roast comment thread.
    ///
    /// This enables interaction: the user can respond to the AI's roasts,
    /// creating a conversation within the inline comment threads.
    ///
    /// Behavior:
    /// - Takes a line number (identifies which roast to reply to)
    /// - Takes the user's reply text
    /// - Sends it to the backend
    /// - Returns success/failure boolean
    ///
    /// Example usage in RoastThread.razor:
    ///     var success = await RoastApiService.ReplyToThreadAsync(
    ///         lineNumber: 5,
    ///         userReply: "Actually, I fixed this in v2!"
    ///     );
    ///     if (success)
    ///     {
    ///         // Show success message to user
    ///         // Update UI to show the reply was posted
    ///     }
    ///
    /// Return values:
    /// - true: Reply was successfully posted to the backend
    /// - false: Failed to post (network error, validation error, etc.)
    ///
    /// In a real service, you might:
    /// - Return more detailed status (enum instead of bool)
    /// - Throw exceptions for network errors
    /// - Store replies in a database
    /// - Trigger notifications/webhooks
    /// </summary>
    Task<bool> ReplyToThreadAsync(int lineNumber, string userReply);
}
