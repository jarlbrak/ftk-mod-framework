using System;
using System.IO;
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
            Console.WriteLine("PASS: " + checks + " package model path checks");
        }
        finally { Directory.Delete(root, true); }
    }
}
