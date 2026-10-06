namespace ShahinBilling.Api.Infrastructure;

public class AppException(string message, int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class NotFoundException(string what) : AppException($"{what} was not found.", StatusCodes.Status404NotFound);
