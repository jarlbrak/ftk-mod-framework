using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FTKModFramework.Core.Reporting
{
    // Match the helper's flock / LockFileEx contract, not managed FileShare semantics.
    // A separate owner prevents restart inspection from racing a live game.
    internal sealed class ReportingSessionLease : IDisposable
    {
        private FileStream held;
        private string heldRoot;
        internal bool Acquired { get { return held != null; } }
        [StructLayout(LayoutKind.Sequential)]
        private struct Overlapped { internal IntPtr Internal, InternalHigh; internal uint Offset, OffsetHigh; internal IntPtr Event; }
        [DllImport("libc", SetLastError = true)] private static extern int flock(int fd, int operation);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool LockFileEx(IntPtr file, uint flags, uint reserved, uint low, uint high, ref Overlapped overlapped);
        public void Dispose() { if (held != null) { held.Close(); held = null; } }
        internal void Acquire(string root)
        {
            root = Path.GetFullPath(root);
            if (held != null)
            {
                if (heldRoot != root) throw new IOException("Runtime lease belongs to another reporting root.");
                return;
            }
            Directory.CreateDirectory(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reporting root cannot be a symlink.");
            string lockPath = Path.Combine(root, "reporting.lock");
            try
            {
                if ((File.GetAttributes(lockPath) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reporting runtime lock cannot be a symlink.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
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
                if (!success) throw new IOException("Another game or reporting cleanup owns this installation. Close it before starting this game.");
                heldRoot = root;
                held = candidate;
            }
            catch { candidate.Close(); throw; }
        }
    }
}
