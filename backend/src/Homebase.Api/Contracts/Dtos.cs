using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// Wire shapes for contracts/openapi.yaml. Serialized camelCase; property names here ARE the contract.
// Change the YAML first (change-api-contract skill), then these, then Mapping/ApiMapper.cs.
// Nullable properties are always written (null when a device type does not have the field).
namespace Homebase.Api.Contracts
{
    public sealed class ApiErrorDto
    {
        public ApiErrorDto(string code, string message)
        {
            Error = new ApiErrorBody { Code = code, Message = message };
        }

        public ApiErrorBody Error { get; set; }
    }

    public sealed class ApiErrorBody
    {
        public string Code { get; set; }

        public string Message { get; set; }
    }

    public sealed class HealthDto
    {
        public string Status { get; set; }

        public string Database { get; set; }
    }

    public sealed class SunDto
    {
        public bool Dark { get; set; }

        public string Sunrise { get; set; }

        public string Sunset { get; set; }

        public bool LocationSet { get; set; }
    }

    public sealed class PresenceDto
    {
        public bool? Home { get; set; }

        public string PhoneDeviceId { get; set; }

        public string TvDeviceId { get; set; }

        public SunDto Sun { get; set; }

        public string ReportToken { get; set; }
    }

    public sealed class RoomDto
    {
        public string Id { get; set; }

        public string Name { get; set; }
    }

    public sealed class DeviceStateDto
    {
        public bool? On { get; set; }

        public int? Brightness { get; set; }

        public int? ColorTempK { get; set; }

        public bool? Home { get; set; }

        public bool? Charging { get; set; }

        public bool? Asleep { get; set; }
    }

    public sealed class DeviceDto
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string RoomId { get; set; }

        public string Type { get; set; }

        public string Driver { get; set; }

        public bool Online { get; set; }

        public bool NeedsPairing { get; set; }

        public DeviceStateDto State { get; set; }

        public DateTime LastChangedAt { get; set; }
    }

    public sealed class DeviceCommandDto
    {
        public bool? On { get; set; }

        public int? Brightness { get; set; }

        public int? ColorTempK { get; set; }
    }

    public sealed class DeviceSettingsDto
    {
        public string Name { get; set; }

        public string RoomId { get; set; }
    }

    public sealed class DeviceReportDto
    {
        public bool? Home { get; set; }

        public bool? Charging { get; set; }

        public bool? Asleep { get; set; }
    }

    public sealed class DiscoveryDto
    {
        public List<DeviceDto> Added { get; set; }

        public List<DeviceDto> Updated { get; set; }

        public List<string> Notes { get; set; }
    }

    public sealed class PairingRequestDto
    {
        public string Address { get; set; }
    }

    public sealed class PairingDto
    {
        public string DeviceId { get; set; }

        public string Message { get; set; }
    }

    public sealed class PairingCodeDto
    {
        public string Code { get; set; }
    }

    public sealed class ActionDto
    {
        public string Kind { get; set; }

        public string DeviceId { get; set; }

        public string RoomId { get; set; }

        public string SceneId { get; set; }

        public bool? On { get; set; }

        public int? Brightness { get; set; }

        public int? ColorTempK { get; set; }

        public string Summary { get; set; }
    }

    public sealed class SceneDto
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<ActionDto> Actions { get; set; }
    }

    public sealed class ActionResultDto
    {
        public ActionDto Action { get; set; }

        public bool Ok { get; set; }

        public string ErrorCode { get; set; }
    }

    public sealed class SceneActivationDto
    {
        public string SceneId { get; set; }

        public List<ActionResultDto> Results { get; set; }
    }

    public sealed class TriggerDto
    {
        public string Kind { get; set; }

        public string DeviceId { get; set; }

        public string Field { get; set; }

        [JsonPropertyName("equals")]
        public bool? EqualsValue { get; set; }

        public int? ForMinutes { get; set; }

        public string At { get; set; }

        public List<string> Days { get; set; }

        public string Summary { get; set; }
    }

    public sealed class ConditionDto
    {
        public string Kind { get; set; }

        public string After { get; set; }

        public string Before { get; set; }

        public string DeviceId { get; set; }

        public string Field { get; set; }

        [JsonPropertyName("equals")]
        public bool? EqualsValue { get; set; }

        public string Summary { get; set; }
    }

    public sealed class AutomationDto
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool Enabled { get; set; }

        public TriggerDto Trigger { get; set; }

        public List<ConditionDto> Conditions { get; set; }

        public List<ActionDto> Actions { get; set; }

        public DateTime? LastRunAt { get; set; }

        public string LastOutcome { get; set; }

        public DateTime? WaitingUntil { get; set; }
    }

    public sealed class SetEnabledRequestDto
    {
        public bool? Enabled { get; set; }
    }

    public sealed class TraceStepDto
    {
        public string Label { get; set; }

        public bool Passed { get; set; }

        public string Detail { get; set; }
    }

    public sealed class SimulationDto
    {
        public string AutomationId { get; set; }

        public bool WouldRun { get; set; }

        public DateTime EvaluatedAt { get; set; }

        public List<TraceStepDto> Trace { get; set; }

        public List<ActionDto> PlannedActions { get; set; }
    }

    public sealed class AutomationRunDto
    {
        public string Id { get; set; }

        public string AutomationId { get; set; }

        public string AutomationName { get; set; }

        public DateTime FiredAt { get; set; }

        public string Outcome { get; set; }

        public List<TraceStepDto> Trace { get; set; }

        public int ActionsExecuted { get; set; }
    }
}
