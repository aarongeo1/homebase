using System.Text.Json.Serialization;
using Homebase.Api.Contracts;
using Homebase.Api.Infrastructure;
using Homebase.Application;
using Homebase.Domain;
using Homebase.Domain.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Homebase.Api
{
    /// <summary>Registers and maps the API. Shared by Homebase.Api.Host and the in-memory test server.</summary>
    public static class HomebaseApi
    {
        public static IServiceCollection AddHomebaseApi(this IServiceCollection services, HomeServices home, IDatabaseHealthCheck health, ReportToken token)
        {
            services.AddSingleton(home.Devices);
            services.AddSingleton(home.Scenes);
            services.AddSingleton(home.Automations);
            services.AddSingleton(home.Presence);
            services.AddSingleton(health);
            services.AddSingleton(token);

            services.AddControllers(options => options.Filters.Add(new ApiExceptionFilter()))
                .AddApplicationPart(typeof(HomebaseApi).Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                })
                .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ErrorResponses.InvalidModelState);
            return services;
        }

        /// <summary>Controllers under /api, JSON 404 for unknown /api paths, JSON 500 for anything unhandled.</summary>
        public static WebApplication UseHomebaseApi(this WebApplication app)
        {
            app.UseExceptionHandler(handler => handler.Run(context =>
            {
                var error = context.Features.Get<IExceptionHandlerFeature>();
                var result = ErrorResponses.For(error == null ? null : error.Error);
                context.Response.StatusCode = result.StatusCode ?? StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsJsonAsync(result.Value);
            }));

            app.MapControllers();
            app.Map("/api/{**path}", (HttpContext context) =>
                Results.Json(new ApiErrorDto(ErrorCodes.NotFound, "No API endpoint at " + context.Request.Path + "."), statusCode: StatusCodes.Status404NotFound));
            return app;
        }
    }
}
