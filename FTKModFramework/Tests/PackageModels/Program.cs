using System;
using System.IO;
using System.Security.Cryptography;
using FTKModFramework.Core;

internal static class Program
{
    private static int checks;
    private static void Check(bool pass, string message)
    {
        checks++;
        if (!pass) throw new Exception(message);
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        catch (IOException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-package-models-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            string model = Path.Combine(root, "assets", "hammer.glb");
            File.WriteAllText(model, "original model identity fixture");
            string first = PackageModelPaths.Register("mod.one", root, "assets/hammer.glb");
            Check(PackageModelPaths.Resolve(first) == model, "Asset resolves to its owning package.");
            Check(first == PackageModelPaths.Register("mod.one", root, "assets/hammer.glb"), "Repeat is stable.");
            Check(first != PackageModelPaths.Register("mod.two", root, "assets/hammer.glb"), "Mods cannot collide.");
            string verifiedHash;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(model))
                verifiedHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            string verified = PackageModelPaths.RegisterVerified("mod.verified", root, "assets/hammer.glb", verifiedHash, new FileInfo(model).Length);
            Check(PackageModelPaths.Resolve(verified) == model, "Helper-verified immutable asset resolves without changing identity.");
            Reject(delegate { PackageModelPaths.RegisterVerified("mod.invalid", root, "assets/hammer.glb", verifiedHash, 1); },
                "Verified asset size must match its lock record.");
            Reject(delegate { PackageModelPaths.RegisterVerified("mod.invalid", root, "assets/hammer.glb", "not-a-hash", new FileInfo(model).Length); },
                "Verified asset hash must be canonical.");
            File.WriteAllText(model, "revised original model fixture");
            Reject(delegate { PackageModelPaths.Resolve(first); }, "Changed asset invalidates old identity.");
            Check(first != PackageModelPaths.Register("mod.one", root, "assets/hammer.glb"), "Changed bytes get a new identity.");
            foreach (string path in new[] { "../hammer.glb", "assets/../hammer.glb", "/assets/hammer.glb", "assets//hammer.glb", "assets/./hammer.glb", "assets\\hammer.glb", "assets/https:evil.glb", "assets/hammer.dll", "assets/hammer.GLB", "assets/missing.glb" })
                Reject(delegate { PackageModelPaths.Register("mod.one", root, path); }, "Reject " + path);
            Reject(delegate { PackageModelPaths.Resolve("package-model:unknown"); }, "Unregistered tokens fail closed.");
            string link = Path.Combine(root, "assets", "link.glb");
            File.CreateSymbolicLink(link, model);
            Reject(delegate { PackageModelPaths.Register("mod.one", root, "assets/link.glb"); }, "Reject symlink asset.");
            Check(!PackageModelPaths.IsPackagePath("ordinary.glb"), "Legacy names remain distinct.");
            string otherRoot = Path.Combine(root, "generation-two");
            Directory.CreateDirectory(Path.Combine(otherRoot, "assets"));
            File.Copy(model, Path.Combine(otherRoot, "assets", "hammer.glb"));
            string current = PackageModelPaths.Register("mod.one", root, "assets/hammer.glb");
            Reject(delegate { PackageModelPaths.Register("mod.one", otherRoot, "assets/hammer.glb"); },
                "Two roots cannot silently rebind one active generation identity.");
            Action restore = PackageModelPaths.SuspendForReload();
            Check(PackageModelPaths.ReloadPathCount == 0, "Candidate resolver starts empty.");
            string moved = PackageModelPaths.Register("mod.one", otherRoot, "assets/hammer.glb");
            Check(moved == current, "Unchanged bytes preserve identity across generations.");
            Check(PackageModelPaths.Resolve(moved) == Path.Combine(otherRoot, "assets", "hammer.glb"),
                "Candidate uses its own generation root.");
            restore();
            Check(PackageModelPaths.Resolve(current) == model, "Rollback restores exact old asset root.");
            Console.WriteLine("PASS: " + checks + " package model path checks");
        }
        finally { Directory.Delete(root, true); }
    }
}
