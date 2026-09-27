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

        /// <summary>Assigned by the plugin at load. Used by the Session lifecycle's probe trace and
        /// self-test lines; dropped while null.</summary>
        internal static Action<string> Info { get; set; }
        internal static Action<string> Error { get; set; }

        internal static readonly TweakRegistry Registry = new TweakRegistry(message => Emit(Warn, message));

        /// <summary>Capture, lock and clear, called by the lifecycle patches.</summary>
        internal static readonly TweakSessionLifecycle Session = new TweakSessionLifecycle(Registry,
            message => Emit(Warn, message), message => Emit(Info, message), message => Emit(Error, message),
            () => FrameworkTweaks.SessionProbe);

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

        private static void Emit(Action<string> sink, string message)
        {
            if (sink != null) sink(message);
        }
    }
}
