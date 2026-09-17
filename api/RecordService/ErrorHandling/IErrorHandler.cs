using Microsoft.AspNetCore.Mvc;

namespace RecordService.ErrorHandling;

public interface IErrorHandler
{
    IActionResult BadRequest(string message);
    IActionResult Unauthorized(string message);
    IActionResult Forbidden(string message);
    IActionResult NotFound(string message);
    IActionResult InternalServerError(string message, string? errorDetail = null);
    IActionResult Created(string message, object data);
    IActionResult NoContent(string message);
    IActionResult Ok(object data);
}

public class DefaultErrorHandler : IErrorHandler
{
    public IActionResult BadRequest(string message)
    {
        return new BadRequestObjectResult(new { message });
    }

    public IActionResult Unauthorized(string message)
    {
        return new UnauthorizedObjectResult(new { message });
    }

    public IActionResult Forbidden(string message)
    {
        return new ForbidResult();
    }

    public IActionResult NotFound(string message)
    {
        return new NotFoundObjectResult(new { message });
    }

    public IActionResult InternalServerError(string message, string? errorDetail = null)
    {
        return new ObjectResult(new { message, error = errorDetail })
        {
            StatusCode = 500
        };
    }

    public IActionResult Created(string message, object data)
    {
        return new CreatedResult(string.Empty, new { message, data });
    }

    public IActionResult NoContent(string message)
    {
        return new NoContentResult();
    }

    public IActionResult Ok(object data)
    {
        return new OkObjectResult(data);
    }
}
