using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Homebase.Api.Contracts;
using Homebase.Domain;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Homebase.Api.Infrastructure
{
    /// <summary>
    /// The shared secret the phone sends as "Authorization: Bearer &lt;token&gt;" so nobody else on the network can
    /// pretend you left home. Created once and kept in the data folder (report-token.txt, readable only by you).
    /// </summary>
    public sealed class ReportToken
    {
        public const string FileName = "report-token.txt";
        private const string Scheme = "Bearer ";

        public ReportToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 16)
            {
                throw new ArgumentException("A report token needs at least 16 characters.", nameof(value));
            }

            Value = value.Trim();
        }

        public string Value { get; }

        public static ReportToken LoadOrCreate(string dataDirectory)
        {
            var path = Path.Combine(dataDirectory, FileName);
            if (File.Exists(path))
            {
                return new ReportToken(File.ReadAllText(path));
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            File.WriteAllText(path, token + Environment.NewLine);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            return new ReportToken(token);
        }

        /// <summary>Constant-time comparison of an Authorization header value.</summary>
        public bool Matches(string authorizationHeader)
        {
            if (authorizationHeader == null || !authorizationHeader.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var presented = Encoding.UTF8.GetBytes(authorizationHeader.Substring(Scheme.Length).Trim());
            return CryptographicOperations.FixedTimeEquals(presented, Encoding.UTF8.GetBytes(Value));
        }
    }

    /// <summary>Runs before model binding, so a request without the token learns nothing about the body rules.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RequireReportTokenAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var token = context.HttpContext.RequestServices.GetRequiredService<ReportToken>();
            if (!token.Matches(context.HttpContext.Request.Headers.Authorization.ToString()))
            {
                context.Result = ErrorResponses.Result(401, ErrorCodes.Unauthorized, "Send the report token as 'Authorization: Bearer <token>' (Presence page on the Mac).");
            }
        }
    }
}
