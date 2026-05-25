namespace Roaster.Api.Services;

public enum ResultStatus { Ok, NotFound, Conflict, BadRequest, NoContent }

public sealed class ServiceResult : IResult
{
    public ResultStatus Status { get; private init; }
    public string? Error { get; private init; }

    public static ServiceResult Ok()                       => new() { Status = ResultStatus.Ok };
    public static ServiceResult NoContent()                => new() { Status = ResultStatus.NoContent };
    public static ServiceResult NotFound(string? e = null) => new() { Status = ResultStatus.NotFound, Error = e };
    public static ServiceResult BadRequest(string e)       => new() { Status = ResultStatus.BadRequest, Error = e };

    public Task ExecuteAsync(HttpContext httpContext) => Status switch
    {
        ResultStatus.NotFound  => Results.NotFound(Error).ExecuteAsync(httpContext),
        ResultStatus.BadRequest => Results.BadRequest(Error).ExecuteAsync(httpContext),
        ResultStatus.NoContent => Results.NoContent().ExecuteAsync(httpContext),
        _ => Results.Ok().ExecuteAsync(httpContext)
    };
}

public sealed class ServiceResult<T> : IResult
{
    public T? Data { get; private init; }
    public ResultStatus Status { get; private init; }
    public string? Error { get; private init; }

    public static ServiceResult<T> Ok(T data)                 => new() { Data = data, Status = ResultStatus.Ok };
    public static ServiceResult<T> NoContent()                => new() { Status = ResultStatus.NoContent };
    public static ServiceResult<T> NotFound(string? e = null) => new() { Status = ResultStatus.NotFound, Error = e };
    public static ServiceResult<T> Conflict(string e)         => new() { Status = ResultStatus.Conflict, Error = e };
    public static ServiceResult<T> BadRequest(string e)       => new() { Status = ResultStatus.BadRequest, Error = e };

    public Task ExecuteAsync(HttpContext httpContext) => Status switch
    {
        ResultStatus.NotFound   => Results.NotFound(Error).ExecuteAsync(httpContext),
        ResultStatus.Conflict   => Results.Conflict(Error).ExecuteAsync(httpContext),
        ResultStatus.BadRequest => Results.BadRequest(Error).ExecuteAsync(httpContext),
        ResultStatus.NoContent  => Results.NoContent().ExecuteAsync(httpContext),
        _ => Results.Ok(Data).ExecuteAsync(httpContext)
    };
}
