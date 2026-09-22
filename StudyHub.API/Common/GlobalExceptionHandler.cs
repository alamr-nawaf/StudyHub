using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Common.Exceptions;
using FluentValidation;

namespace StudyHub.API.Common
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(
            IProblemDetailsService problemDetailsService,
            ILogger<GlobalExceptionHandler> logger)
        {
            _problemDetailsService = problemDetailsService;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // The client hung up: nobody will read the response, and this is not a fault that
            // deserves a full error log
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request aborted by the client.");
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
                return true;
            }

            // The exception type decides the status code
            var (statusCode, title) = exception switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                QuotaExceededException => (StatusCodes.Status429TooManyRequests, "Quota exceeded"),
                // 502, not 500: the failure is outside this process, and its message is
                // deliberately generic because it is written into the body (§15.3)
                ExternalServiceException => (StatusCodes.Status502BadGateway, "Upstream service failed"),
                _ => (StatusCodes.Status500InternalServerError, "Server error")
            };

            // Expected failures are warnings; unexpected ones are errors with the full stack
            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception");
            else
                _logger.LogWarning("Handled: {Type}", exception.GetType().Name);

            httpContext.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                // Nothing of an unexpected exception is leaked to the client
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception.Message
            };

            // Field errors are returned grouped by field name
            if (exception is ValidationException validationException)
            {
                // The raw exception text is an internal dump; the right channel is errors
                problemDetails.Detail = "One or more validation errors occurred.";
                problemDetails.Extensions["errors"] = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            }

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception
            });
        }
    }
}
