using System;
using System.Collections.Generic;

namespace FTKModFramework.Core.Reporting
{
    // Copies the tweak registry into detached rows for the diagnostic snapshot. Call it on the
    // Unity thread, which is the only thread that changes the registry. It reads state and never
    // toggles, captures or clears. Unity-free, so Tests/ReportingMetadata links it with Core/Tweaks.
    internal static class ReportingTweakSource
    {
        /// <summary>Null when the registry never initialized; the snapshot then reports both tweak
        /// sections as unavailable rather than as an empty success.</summary>
        internal static ReportingTweakState Copy(TweakRegistry registry)
        {
            if (registry == null || !registry.IsInitialized) return null;
            int total = registry.Count;
            List<ReportingTweakRow> rows = new List<ReportingTweakRow>(Math.Min(total, ReportingMetadata.MaximumRows));
            for (int handle = 0; handle < total && rows.Count < ReportingMetadata.MaximumRows; handle++)
            {
                TweakDescriptor descriptor = registry.Get(handle);
                rows.Add(new ReportingTweakRow { Id = descriptor.Id, Preference = Preference(registry.Preference(handle)),
                    On = registry.IsOn(handle), Faulted = registry.IsFaulted(handle) });
            }
            return new ReportingTweakState { Rows = rows, TotalCount = total, SessionState = State(registry.SessionState),
                SessionMode = Mode(registry.SessionMode), SessionSource = registry.SessionSource };
        }

        // Fixed wire names, so renaming an enum member cannot silently change the snapshot.
        internal static string Preference(TweakPreference preference)
        {
            switch (preference)
            {
                case TweakPreference.On: return "on";
                case TweakPreference.Off: return "off";
                default: return "default";
            }
        }

        internal static string State(TweakSessionState state)
        {
            switch (state)
            {
                case TweakSessionState.Captured: return "captured";
                case TweakSessionState.Locked: return "locked";
                default: return "none";
            }
        }

        internal static string Mode(TweakSessionMode? mode)
        {
            if (!mode.HasValue) return null;
            switch (mode.Value)
            {
                case TweakSessionMode.SinglePlayer: return "single_player";
                case TweakSessionMode.LocalMultiplayer: return "local_multiplayer";
                default: return "multiplayer";
            }
        }
    }
}
