using System;

namespace Homebase.Domain
{
    /// <summary>A business rule violation. The API maps it to HTTP 422 with <see cref="Code"/> as the error code.</summary>
    public class DomainException : Exception
    {
        public DomainException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    /// <summary>Mapped to HTTP 404.</summary>
    public sealed class NotFoundException : DomainException
    {
        public NotFoundException(string resource, string id)
            : base(ErrorCodes.NotFound, resource + " '" + id + "' was not found.")
        {
        }
    }

    /// <summary>The device did not respond (driver timeout/error) or is marked offline.</summary>
    public sealed class DeviceOfflineException : DomainException
    {
        public DeviceOfflineException(string deviceName, string reason)
            : base(ErrorCodes.DeviceOffline, deviceName + " is offline" + (string.IsNullOrEmpty(reason) ? "." : ": " + reason))
        {
        }
    }

    /// <summary>Stable error codes that appear in API responses. Clients may switch on them, so never rename one.</summary>
    public static class ErrorCodes
    {
        public const string NotFound = "not_found";
        public const string ValidationFailed = "validation_failed";
        public const string InternalError = "internal_error";
        public const string ReadOnlyDevice = "read_only_device";
        public const string DeviceOffline = "device_offline";
        public const string Unauthorized = "unauthorized";
        public const string NotReportable = "not_reportable";
        public const string NotPairable = "not_pairable";
        public const string PairingFailed = "pairing_failed";
    }
}
