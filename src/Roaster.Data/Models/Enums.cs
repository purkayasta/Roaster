namespace Roaster.Data.Models;

public enum SubmissionStatus
{
    Submitted  = 10,
    Processing = 20,
    Completed  = 30,
    Failed     = 40
}

public enum ReviewSeverity
{
    Gentle = 10,
    Medium = 20,
    Brutal = 30
}

public enum EventType
{
    Submitted         = 10,
    ProcessingStarted = 20,
    ReviewCompleted   = 30,
    Failed            = 40,
    UserReplied       = 50,
    AiReplied         = 60
}
