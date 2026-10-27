using System;
using System.Linq;
using Homebase.Api.Contracts;
using Homebase.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Homebase.Api.Infrastructure
{
    /// <summary>
    /// The only place exceptions become HTTP responses: NotFoundException -> 404, unauthorized -> 401, any other
    /// DomainException -> 422, anything else -> 500 with a generic message. Stack traces never leave the process.
    /// </summary>
    public static class ErrorResponses
    {
        public static ObjectResult For(Exception exception)
        {
            var notFound = exception as NotFoundException;
            if (notFound != null)
            {
                return Result(StatusCodes.Status404NotFound, notFound.Code, notFound.Message);
            }

            var domain = exception as DomainException;
            if (domain != null)
            {
                var status = domain.Code == ErrorCodes.Unauthorized ? StatusCodes.Status401Unauthorized : StatusCodes.Status422UnprocessableEntity;
                return Result(status, domain.Code, domain.Message);
            }

            return Result(StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred.");
        }

        public static ObjectResult Result(int status, string code, string message) =>
            new ObjectResult(new ApiErrorDto(code, message)) { StatusCode = status };

        /// <summary>Malformed JSON or unparseable values (e.g. limit=abc) become 422 validation_failed, not a framework error page.</summary>
        public static IActionResult InvalidModelState(ActionContext context)
        {
            var first = context.ModelState
                .Where(kv => kv.Value.Errors.Count > 0)
                .Select(kv => (kv.Key.Length > 0 ? kv.Key + ": " : string.Empty)
                    + (kv.Value.Errors[0].ErrorMessage.Length > 0 ? kv.Value.Errors[0].ErrorMessage : "invalid value"))
                .FirstOrDefault();
            return Result(StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed, "Invalid request. " + first);
        }
    }

    public sealed class ApiExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            context.Result = ErrorResponses.For(context.Exception);
            context.ExceptionHandled = true;
        }
    }
}
