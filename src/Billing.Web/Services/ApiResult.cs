namespace Billing.Web.Services;

public sealed record ApiResult<T>(T? Value, string? Error)
{
    public static ApiResult<T> Success(T value) => new(value, null);
    public static ApiResult<T> Failure(string error) => new(default, error);
}
