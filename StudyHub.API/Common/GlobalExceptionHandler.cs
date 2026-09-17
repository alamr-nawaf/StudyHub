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
            // العميل قطع الاتصال: لا أحد سيقرأ الرد، وليس عطلًا يستحق سجل خطأ كامل
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request aborted by the client.");
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
                return true;
            }

            // ترجمة نوع الاستثناء إلى رمز HTTP
            var (statusCode, title) = exception switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                _ => (StatusCodes.Status500InternalServerError, "Server error")
            };

            // المتوقَّع يُسجَّل تحذيرًا، وغير المتوقَّع خطأً كاملًا
            if (statusCode == StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception");
            else
                _logger.LogWarning("Handled: {Type}", exception.GetType().Name);

            httpContext.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                // لا نسرّب تفاصيل الاستثناءات غير المتوقعة للعميل
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception.Message
            };

            // أخطاء الحقول تُرجَع مجمّعة باسم الحقل
            if (exception is ValidationException validationException)
            {
                // نص الاستثناء الخام تفريغ داخلي؛ القناة الصحيحة هي errors
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
