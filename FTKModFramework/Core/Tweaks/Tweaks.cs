using System;

namespace FTKModFramework.Core
{
    /// <summary>Process-wide tweak registry. Framework code registers descriptors; there is no
    /// Content.* surface, so mod authors cannot. Runtime wiring (config binding, lifecycle hooks)
    /// lives outside this Unity-free folder and drives Registry directly.</summary>
    internal static class Tweaks
    {
        /// <summary>Assigned by the plugin at load, before any registration. Messages raised while
        /// it is null are dropped rather than failing a registration.</summary>
        internal static Action<string> Warn { get; set; }

        internal static readonly TweakRegistry Registry = new TweakRegistry(message =>
        {
            Action<string> warn = Warn;
            if (warn != null) warn(message);
        });

        internal static int Register(TweakDescriptor descriptor)
        {
            return Registry.Register(descriptor);
        }

        internal static bool IsOn(int handle)
        {
            return Registry.IsOn(handle);
        }

        internal static bool IsOn(string id)
        {
            return Registry.IsOn(id);
        }

        internal static void Fault(int handle, Exception exception)
        {
            Registry.Fault(handle, exception);
        }
    }
}
