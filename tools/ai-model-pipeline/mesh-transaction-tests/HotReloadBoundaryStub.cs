// Resource-ledger deferred destruction is exercised separately by HotReloadResources.
// This suite keeps its immediate Unity destruction double for existing renderer contracts.
namespace FTKModFramework.Core.HotReload
{
    internal static class PaladinResourceState
    {
        internal static void DestroyTracked(UnityEngine.Object value) { UnityEngine.Object.Destroy(value); }
    }
}
