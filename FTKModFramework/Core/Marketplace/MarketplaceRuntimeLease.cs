using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FTKModFramework.Core.Marketplace
{
    // Match the helper's flock / LockFileEx contract, not managed FileShare semantics.
    // Held until process exit so an offline collector cannot remove live generation files.
    internal static class MarketplaceRuntimeLease
    {
        private static FileStream held;
        private static string heldRoot;
        internal static bool Acquired { get { return held != null; } }
        [StructLayout(LayoutKind.Sequential)]
        private struct Overlapped { internal IntPtr Internal, InternalHigh; internal uint Offset, OffsetHigh; internal IntPtr Event; }
        [DllImport("libc", SetLastError = true)] private static extern int flock(int fd, int operation);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool LockFileEx(IntPtr file, uint flags, uint reserved, uint low, uint high, ref Overlapped overlapped);
        internal static void Acquire(string root)
        {
            root = Path.GetFullPath(root);
            if (held != null)
            {
                if (heldRoot != root) throw new IOException("Runtime lease belongs to another marketplace root.");
                return;
            }
            Directory.CreateDirectory(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Marketplace root cannot be a symlink.");
            string lockPath = Path.Combine(root, "runtime.lock");
            if (File.Exists(lockPath) && (File.GetAttributes(lockPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Marketplace runtime lock cannot be a symlink.");
            FileStream candidate = new FileStream(lockPath, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.ReadWrite);
            try
            {
                // Shipped Mono creates a NEW owning wrapper on every SafeFileHandle
                // getter without retaining it. Its finalizer closes the live stream's FD.
                // Handle borrows the descriptor; held remains its sole lifetime owner.
#pragma warning disable 618
                IntPtr handle = candidate.Handle;
#pragma warning restore 618
                bool success;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    Overlapped overlapped = new Overlapped();
                    success = LockFileEx(handle, 3, 0, 1, 0, ref overlapped);
                }
                else success = flock(handle.ToInt32(), 6) == 0;
                if (!success) throw new IOException("Another game or marketplace cleanup owns this installation. Close it before starting this game.");
                heldRoot = root;
                held = candidate;
            }
            catch { candidate.Close(); throw; }
        }
    }
}
