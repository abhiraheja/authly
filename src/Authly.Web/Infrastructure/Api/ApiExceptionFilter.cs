using Authly.Modules.ApiKeys;
using Authly.Modules.Applications;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authly.Web.Infrastructure.Api;

/// <summary>
/// Translates module exceptions thrown by Management API actions into the standard error envelope
/// <c>{ error: { code, message } }</c> with the right HTTP status. Unknown exceptions become a
/// generic 500 (details are logged, never leaked to the caller).
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var (status, code, message) = context.Exception switch
        {
            UserNotFoundException or ApplicationNotFoundException or RoleNotFoundException or ApiKeyNotFoundException
                => (StatusCodes.Status404NotFound, "not_found", context.Exception.Message),

            UserEmailAlreadyExistsException or EmailAlreadyExistsException or RoleNameAlreadyExistsException
                => (StatusCodes.Status409Conflict, "conflict", context.Exception.Message),

            SystemRoleProtectedException or PublicClientHasNoSecretException
                => (StatusCodes.Status422UnprocessableEntity, "unprocessable", context.Exception.Message),

            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(context.Exception, "Unhandled Management API exception");

        context.Result = new ObjectResult(ApiErrorEnvelope.Of(code, message)) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
