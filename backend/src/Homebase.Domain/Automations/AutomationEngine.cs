using System;
using System.Collections.Generic;
using System.Globalization;
using Homebase.Domain.Devices;
using Homebase.Domain.Sun;

namespace Homebase.Domain.Automations
{
    public enum EvaluationReason
    {
        /// <summary>Dry run: the trigger is assumed to have fired (and held for forMinutes); nothing is changed.</summary>
        Simulation,

        /// <summary>Scheduler tick for time triggers.</summary>
        TimeTick,

        /// <summary>A device's trigger-relevant state changed.</summary>
        DeviceChange,

        /// <summary>A forMinutes countdown ended; the trigger fires if the field still matches.</summary>
        HeldFor,
    }

    public sealed class EvaluationContext
    {
        public DateTime NowUtc { get; set; }

        public TimeZoneInfo TimeZone { get; set; }

        public IReadOnlyDictionary<string, Device> Devices { get; set; }

        public EvaluationReason Reason { get; set; }

        /// <summary>Set when <see cref="Reason"/> is DeviceChange.</summary>
        public DeviceChange Change { get; set; }

        /// <summary>Set when <see cref="Reason"/> is HeldFor.</summary>
        public PendingTrigger Pending { get; set; }

        public SunInfo Sun { get; set; }

        public NameLookup Names { get; set; }

        public DateTime NowLocal => ToLocal(NowUtc);

        public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone ?? TimeZoneInfo.Utc);
    }

    public sealed class Evaluation
    {
        public Evaluation()
        {
            Trace = new List<TraceStep>();
            Planned = new List<HomeAction>();
        }

        /// <summary>The trigger fired (or was assumed to, in a simulation). Only triggered evaluations become runs.</summary>
        public bool Triggered { get; set; }

        /// <summary>A forMinutes trigger just started matching; the caller starts the countdown instead of running.</summary>
        public bool Deferred { get; set; }

        public bool ConditionsPassed { get; set; }

        public bool WouldRun { get; set; }

        public List<TraceStep> Trace { get; }

        public List<HomeAction> Planned { get; }
    }

    /// <summary>
    /// Pure decision logic for automations: did the trigger fire, do the conditions pass, which actions run.
    /// Executing actions, countdowns and persisting runs is Homebase.Application's job.
    /// </summary>
    public static class AutomationEngine
    {
        public static Evaluation Evaluate(Automation automation, EvaluationContext context)
        {
            var result = new Evaluation();
            var names = context.Names ?? new NameLookup(null, null, null);
            var nowLocal = context.NowLocal;

            if (!automation.Enabled)
            {
                result.Trace.Add(new TraceStep("Automation enabled", false, "This automation is turned off, so it will not run on its own."));
                if (context.Reason != EvaluationReason.Simulation)
                {
                    return result;
                }
            }

            var trigger = EvaluateTrigger(automation, context, names, nowLocal);
            result.Trace.Add(trigger.Step);
            result.Triggered = trigger.Fired;
            result.Deferred = trigger.Deferred;
            if (!trigger.Fired)
            {
                return result;
            }

            var conditionsPassed = true;
            foreach (var condition in automation.Conditions)
            {
                var step = EvaluateCondition(condition, context, names, nowLocal);
                result.Trace.Add(step);
                conditionsPassed &= step.Passed;
            }

            result.ConditionsPassed = conditionsPassed;
            result.WouldRun = automation.Enabled && conditionsPassed;
            if (!conditionsPassed)
            {
                return result;
            }

            foreach (var action in automation.Actions)
            {
                result.Planned.Add(action);
                result.Trace.Add(new TraceStep("Action: " + Describe.Action(action, names), true, "Planned."));
            }

            return result;
        }

        /// <summary>skipped if it did not run; failed if any action failed (others still ran); else executed.</summary>
        public static RunOutcome DecideOutcome(bool wouldRun, int failedCount)
        {
            if (!wouldRun) return RunOutcome.Skipped;
            if (failedCount > 0) return RunOutcome.Failed;
            return RunOutcome.Executed;
        }

        /// <summary>After is inclusive, before exclusive. When after &gt; before the window wraps midnight.</summary>
        public static bool InTimeWindow(ClockTime now, ClockTime after, ClockTime before)
        {
            return after.CompareTo(before) <= 0
                ? now.CompareTo(after) >= 0 && now.CompareTo(before) < 0
                : now.CompareTo(after) >= 0 || now.CompareTo(before) < 0;
        }

        /// <summary>Boolean fields only: matches when the value is known and equals <paramref name="equals"/>.</summary>
        public static bool Matches(DeviceField field, DeviceState state, bool? equals)
        {
            if (state == null || !equals.HasValue)
            {
                return false;
            }

            var value = DeviceCapabilities.BooleanValue(state, field);
            return value.HasValue && value.Value == equals.Value;
        }

        public static bool TriggerMatches(Trigger trigger, DeviceState state) =>
            trigger.Field.HasValue && Matches(trigger.Field.Value, state, trigger.EqualsValue);

        public static bool IsDue(Trigger trigger, DateTime nowLocal)
        {
            ClockTime at;
            return trigger.Kind == TriggerKind.Time
                && ClockTime.TryParse(trigger.At, out at)
                && ClockTime.Of(nowLocal).Equals(at)
                && (trigger.Days == null || trigger.Days.Count == 0 || trigger.Days.Contains(nowLocal.DayOfWeek));
        }

        private static TriggerResult EvaluateTrigger(Automation automation, EvaluationContext context, NameLookup names, DateTime nowLocal)
        {
            var trigger = automation.Trigger;
            var label = "Trigger: " + Describe.Trigger(trigger, names);

            if (context.Reason == EvaluationReason.Simulation)
            {
                var held = trigger.Kind == TriggerKind.Device && trigger.ForMinutes.HasValue ? " and to have held for " + trigger.ForMinutes.Value + " min" : string.Empty;
                return TriggerResult.Fire(label, "Assumed to have fired now" + held + " (simulation). Household time is " + Clock(nowLocal) + ".");
            }

            if (context.Reason == EvaluationReason.TimeTick)
            {
                if (trigger.Kind != TriggerKind.Time || !IsDue(trigger, nowLocal))
                {
                    return TriggerResult.NotFired(label, "Not due at " + Clock(nowLocal) + ".");
                }

                // At most once per automation per household-local minute (the scheduler ticks several times a minute).
                if (automation.LastRunAt.HasValue
                    && ClockTime.Of(context.ToLocal(automation.LastRunAt.Value)).Equals(ClockTime.Of(nowLocal))
                    && (context.NowUtc - automation.LastRunAt.Value) < TimeSpan.FromMinutes(1))
                {
                    return TriggerResult.NotFired(label, "Already ran at " + Clock(nowLocal) + ".");
                }

                return TriggerResult.Fire(label, "It is " + Clock(nowLocal) + " on " + nowLocal.DayOfWeek.ToString().Substring(0, 3) + " (household time).");
            }

            if (context.Reason == EvaluationReason.HeldFor)
            {
                return EvaluateHeld(trigger, context, names, label);
            }

            var change = context.Change;
            if (trigger.Kind != TriggerKind.Device || change == null || !trigger.Field.HasValue
                || !string.Equals(trigger.DeviceId, change.Device.Id, StringComparison.Ordinal))
            {
                return TriggerResult.NotFired(label, "Not triggered by this change.");
            }

            if (change.Chain.Contains(automation.Id))
            {
                return TriggerResult.NotFired(label, "Loop guard: this automation already ran in this chain (" + change.Chain + ").");
            }

            if (change.Chain.Depth >= AutomationChain.MaxDepth)
            {
                return TriggerResult.NotFired(label, "Loop guard: chain depth " + AutomationChain.MaxDepth + " reached (" + change.Chain + ").");
            }

            var field = trigger.Field.Value;
            var before = TriggerMatches(trigger, change.Previous);
            var after = TriggerMatches(trigger, change.Current);
            var observed = names.Device(change.Device.Id) + " " + FieldNames.Of(field) + " changed from "
                + Describe.FieldValue(change.Previous, field) + " to " + Describe.FieldValue(change.Current, field) + ".";

            // Edge-triggered: fire only on the transition into matching, never on every matching report.
            if (!before && after)
            {
                return trigger.ForMinutes.HasValue
                    ? TriggerResult.Defer(label, observed + " Waiting " + trigger.ForMinutes.Value + " min to make sure it stays that way.")
                    : TriggerResult.Fire(label, observed);
            }

            return TriggerResult.NotFired(label, observed + (after ? " It already matched before." : " It does not match."));
        }

        private static TriggerResult EvaluateHeld(Trigger trigger, EvaluationContext context, NameLookup names, string label)
        {
            Device device;
            if (trigger.DeviceId == null || !context.Devices.TryGetValue(trigger.DeviceId, out device) || !trigger.Field.HasValue)
            {
                return TriggerResult.NotFired(label, "Device " + trigger.DeviceId + " was not found.");
            }

            var state = Describe.StateWord(trigger.Field, trigger.EqualsValue);
            if (!TriggerMatches(trigger, device.State))
            {
                return TriggerResult.NotFired(label, names.Device(device.Id) + " is no longer " + state + " ("
                    + FieldNames.Of(trigger.Field.Value) + " is " + Describe.FieldValue(device.State, trigger.Field.Value) + ").");
            }

            var since = context.Pending == null ? context.NowUtc : context.Pending.Since;
            var minutes = (int)Math.Round((context.NowUtc - since).TotalMinutes);
            return TriggerResult.Fire(label, names.Device(device.Id) + " has been " + state + " since " + Clock(context.ToLocal(since)) + " (" + minutes + " min).");
        }

        private static TraceStep EvaluateCondition(Condition condition, EvaluationContext context, NameLookup names, DateTime nowLocal)
        {
            var label = "Condition: " + Describe.Condition(condition, names);
            switch (condition.Kind)
            {
                case ConditionKind.TimeWindow:
                {
                    ClockTime after;
                    ClockTime before;
                    if (!ClockTime.TryParse(condition.After, out after) || !ClockTime.TryParse(condition.Before, out before))
                    {
                        return new TraceStep(label, false, "Invalid time window.");
                    }

                    return new TraceStep(label, InTimeWindow(ClockTime.Of(nowLocal), after, before), "Now is " + Clock(nowLocal) + " (household time).");
                }

                case ConditionKind.Dark:
                {
                    var sun = context.Sun ?? SunCalculator.Info(context.NowUtc, null, context.TimeZone);
                    var wantDark = condition.EqualsValue ?? true;
                    return new TraceStep(label, sun.Dark == wantDark, DarkDetail(sun, nowLocal));
                }

                default:
                {
                    Device device;
                    if (condition.DeviceId == null || !context.Devices.TryGetValue(condition.DeviceId, out device) || !condition.Field.HasValue)
                    {
                        return new TraceStep(label, false, "Device " + condition.DeviceId + " was not found.");
                    }

                    var field = condition.Field.Value;
                    var passed = Matches(field, device.State, condition.EqualsValue);
                    return new TraceStep(label, passed, device.Name + " " + FieldNames.Of(field) + " is " + Describe.FieldValue(device.State, field) + ".");
                }
            }
        }

        private static string DarkDetail(SunInfo sun, DateTime nowLocal)
        {
            var now = "now is " + Clock(nowLocal) + " (household time).";
            if (!sun.LocationSet)
            {
                return "No location set (HOMEBASE_LATITUDE/HOMEBASE_LONGITUDE), so dark means "
                    + SunCalculator.DefaultDarkFrom + "–" + SunCalculator.DefaultDarkUntil + "; " + now;
            }

            if (!sun.Sunrise.HasValue || !sun.Sunset.HasValue)
            {
                return "The sun does not rise or set today; it is " + (sun.Dark ? "dark" : "light") + ".";
            }

            return "Sunrise " + sun.Sunrise.Value + ", sunset " + sun.Sunset.Value + "; " + now;
        }

        private static string Clock(DateTime local) => local.ToString("HH:mm", CultureInfo.InvariantCulture);

        private sealed class TriggerResult
        {
            private TriggerResult(bool fired, bool deferred, TraceStep step)
            {
                Fired = fired;
                Deferred = deferred;
                Step = step;
            }

            public bool Fired { get; }

            public bool Deferred { get; }

            public TraceStep Step { get; }

            public static TriggerResult Fire(string label, string detail) => new TriggerResult(true, false, new TraceStep(label, true, detail));

            public static TriggerResult Defer(string label, string detail) => new TriggerResult(false, true, new TraceStep(label, true, detail));

            public static TriggerResult NotFired(string label, string detail) => new TriggerResult(false, false, new TraceStep(label, false, detail));
        }
    }
}
