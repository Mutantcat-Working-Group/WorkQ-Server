namespace WorkQ.Server.Services;

public sealed class ApiException(int statusCode, string errorCode, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;

    public static ApiException BadRequest(string errorCode, string message) =>
        new(StatusCodes.Status400BadRequest, errorCode, message);

    public static ApiException Unauthorized(string errorCode, string message) =>
        new(StatusCodes.Status401Unauthorized, errorCode, message);

    public static ApiException Forbidden(string errorCode, string message) =>
        new(StatusCodes.Status403Forbidden, errorCode, message);

    public static ApiException NotFound(string errorCode, string message) =>
        new(StatusCodes.Status404NotFound, errorCode, message);

    public static ApiException Conflict(string errorCode, string message) =>
        new(StatusCodes.Status409Conflict, errorCode, message);
}
