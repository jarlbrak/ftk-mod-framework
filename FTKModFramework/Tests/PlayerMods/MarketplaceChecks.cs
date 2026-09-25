using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

internal static class MarketplaceChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private static void Reject(Action action, string message) { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } Check(rejected, message); }
    internal static void Run(string fixture)
    {
        string root = Path.Combine(fixture, "game");
        BepInEx.Paths.GameRootPath = root;
        string stateRoot = MarketplaceRuntime.StateRoot;
        string first = "11111111111111111111111111111111";
        string second = "22222222222222222222222222222222";
        MakeGeneration(stateRoot, first, false);
        MakeGeneration(stateRoot, second, true);
        File.WriteAllText(Path.Combine(stateRoot, "state.json"), JsonConvert.SerializeObject(new MarketplaceStateRecord { SchemaVersion = 1, Current = first }));
        // Intentionally stale convenience projection must never override authoritative state.json.
        File.WriteAllText(Path.Combine(stateRoot, "runtime-state.json"), "{\"schemaVersion\":1,\"ok\":true,\"active\":null}");
        ManagedSnapshot diskActive = MarketplaceProtocol.ReadState(stateRoot).Active;
        Check(diskActive.GenerationId == first, "fallback reads authoritative pointer rather than stale runtime projection");
        Check(!diskActive.FilesVerified && MarketplaceProtocol.ReadVerifiedGeneration(stateRoot, diskActive).FilesVerified,
            "only helper-validated generation reads authorize lock-file hashes");
        CheckVerifiedPreviews(stateRoot, diskActive);
        string invalidFiles = "55555555555555555555555555555555";
        string invalidRoot = Path.Combine(stateRoot, "generations/" + invalidFiles);
        Directory.CreateDirectory(Path.Combine(invalidRoot, "content"));
        File.WriteAllText(Path.Combine(invalidRoot, "lock.json"), JsonConvert.SerializeObject(new MarketplaceGenerationLock { SchemaVersion = 1,
            Packages = new List<PackageDescriptor>(), Files = new List<MarketplaceGenerationFile> {
                new MarketplaceGenerationFile { Path = "../escape.glb", Sha256 = new string('a', 64), Size = 1 } } }));
        Reject(delegate { MarketplaceProtocol.ReadGeneration(stateRoot, invalidFiles); }, "unsafe generation file records are rejected before trust");
        string result = Path.Combine(fixture, "result.json");
        File.WriteAllText(result, "{\"schemaVersion\":2}");
        Reject(delegate { MarketplaceProtocol.ReadResult(result, null); }, "unsupported helper result schema rejected");
        File.WriteAllText(result, "{\"schemaVersion\":1,\"operationId\":\"other\"}");
        Reject(delegate { MarketplaceProtocol.ReadResult(result, "expected"); }, "cross-operation result rejected");
        File.WriteAllText(result, "{\"schemaVersion\":1,\"ok\":true,\"plan\":[{\"action\":\"keep\",\"packageId\":\"fixture\",\"notice\":\"This package was revoked upstream. The installed copy is kept until you remove it.\"}]}");
        Check(MarketplaceProtocol.ReadResult(result, null).Plan[0].Notice == "This package was revoked upstream. The installed copy is kept until you remove it.", "helper plan entry notice is deserialized");
        File.WriteAllText(result, new string(' ', MarketplaceProtocol.MaxResultBytes + 1));
        Reject(delegate { MarketplaceProtocol.ReadResult(result, null); }, "oversized helper result rejected");
        Reject(delegate { MarketplaceProtocol.ValidateSnapshot(stateRoot, new ManagedSnapshot { GenerationId = "../escape", ContentRoot = fixture }); }, "generation traversal rejected");
        Reject(delegate { MarketplaceProtocol.ValidateSnapshot(stateRoot, new ManagedSnapshot { GenerationId = first, ContentRoot = fixture }); }, "foreign managed content root rejected");
        Check(!MarketplaceProtocol.IsScreenshotPath(stateRoot, result), "screenshot outside verified cache rejected");

        UnityEngine.PlayerPrefs.SetInt(ModRegistry.PrefKeyPrefix + "market.fixture", 1);
        ModEntry managed = ModRegistry.RegisterManaged(new ModManifest { ModGuid = "market.fixture", Name = "Managed" }, new PackageDescriptor { PackageId = "fixture", Enabled = false });
        Check(!managed.Enabled && managed.IsManaged, "managed lock selection takes precedence over legacy PlayerPrefs");
        ModRegistry.SetEnabled(managed.Key, true);
        Check(!managed.Enabled && !managed.PendingEnabled.HasValue, "managed toggle cannot bypass marketplace preparation");

        if (OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: Unix process fixture on Windows"); return; }
        string helper = Path.Combine(Path.Combine(root, "BepInEx/ftkmf"), "ftkmf-launcher-helper");
        File.WriteAllText(helper, "#!/bin/sh\nexec sleep 30\n");
        File.SetUnixFileMode(helper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(helper), "helper.json"), JsonConvert.SerializeObject(new { schemaVersion = 1, protocolVersion = 1, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(helper))).ToLowerInvariant() }));
        MarketplaceProtocol.VerifyHelper(helper);
        Stopwatch timer = Stopwatch.StartNew();
        MarketplaceRuntime.InitializeBeforeDiscovery();
        Check(timer.ElapsedMilliseconds < 5500 && MarketplaceRuntime.CanDiscover, "startup checksum and activation are bounded and timed-out child is reaped");
        Check(MarketplaceRuntime.Active.GenerationId == first, "timed-out activation retains captured active generation");
        Check(MarketplaceRuntime.Start("prepare", MarketplaceRuntime.DesiredSelection(), false, null), "bounded helper preparation starts");
        File.WriteAllText(Path.Combine(stateRoot, "state.json"), JsonConvert.SerializeObject(new MarketplaceStateRecord { SchemaVersion = 1, Current = first, Pending = second }));
        MarketplaceRuntime.CancelRunning();
        Check(MarketplaceRuntime.Pending.GenerationId == second && MarketplaceRuntime.Active.GenerationId == first, "cancel reconciles committed pending state without replacing current-session active set");
        Check(!MarketplaceRuntime.Busy, "normal cancellation still reaps the helper and clears the operation");
        HotReloadCancellationChecks.Run();
        RunRecoveryProcess(Path.Combine(fixture, "recovery-valid"), false);
        RunRecoveryProcess(Path.Combine(fixture, "recovery-invalid"), true);
        // Both mismatch paths must surface as the distinct Discover state, and a readable catalog must clear it.
        InstallHelper(helper, "{\"schemaVersion\":1,\"operationId\":\"OPERATION\",\"ok\":false,\"status\":\"unsupported\",\"message\":\"unsupported catalog schema 2\",\"packages\":[]}");
        RunCatalog();
        Check(MarketplaceRuntime.CatalogUnsupported && MarketplaceRuntime.Catalog == null, "helper unsupported status marks the catalog unsupported without storing it");
        InstallHelper(helper, "{\"schemaVersion\":1,\"operationId\":\"OPERATION\",\"ok\":true,\"status\":\"empty\",\"packages\":[]}");
        RunCatalog();
        Check(!MarketplaceRuntime.CatalogUnsupported && MarketplaceRuntime.Catalog != null && MarketplaceRuntime.Catalog.Status == "empty", "readable catalog clears the unsupported state");
        InstallHelper(helper, "{\"schemaVersion\":2,\"operationId\":\"OPERATION\",\"ok\":true,\"status\":\"online\",\"packages\":[]}");
        RunCatalog();
        Check(MarketplaceRuntime.CatalogUnsupported && MarketplaceRuntime.Notice.Contains("Update or repair the framework and helper together"), "helper protocol mismatch marks the catalog unsupported with repair guidance");
        File.AppendAllText(helper, "# changed bytes\n");
        Reject(delegate { MarketplaceProtocol.VerifyHelper(helper); }, "modified helper checksum rejected before launch");
    }
    private static void CheckVerifiedPreviews(string root, ManagedSnapshot returned)
    {
        string cache = Path.Combine(root, "cache/screenshots");
        Directory.CreateDirectory(cache);
        string banner = Path.Combine(cache, "banner.png");
        File.WriteAllBytes(banner, new byte[] { 1 });
        string outside = Path.Combine(root, "outside.png");
        File.WriteAllBytes(outside, new byte[] { 1 });
        string large = Path.Combine(cache, "large.png");
        File.WriteAllBytes(large, new byte[2 * 1024 * 1024 + 1]);
        PackageDescriptor preview = returned.Packages[0];
        preview.ScreenshotPaths = new string[] { outside, large, "\0", banner, banner };
        preview.Name = "helper must not replace locked metadata";
        preview.Enabled = !preview.Enabled;
        ManagedSnapshot verified = MarketplaceProtocol.ReadVerifiedGeneration(root, returned);
        Check(verified.Packages[0].ScreenshotPaths.Length == 1 && verified.Packages[0].ScreenshotPaths[0] == banner,
            "verified generation retains only valid unique bounded cached preview paths");
        Check(verified.Packages[0].Name != preview.Name && verified.Packages[0].Enabled != preview.Enabled && verified.FilesVerified,
            "preview enrichment preserves authoritative lock metadata and content verification");
        preview.Version = "9.0.0";
        Check(MarketplaceProtocol.ReadVerifiedGeneration(root, returned).Packages[0].ScreenshotPaths == null,
            "preview from another package version cannot cross into verified generation");
        preview.Version = "1.0.0";
        preview.Sha256 = new string('f', 64);
        Check(MarketplaceProtocol.ReadVerifiedGeneration(root, returned).Packages[0].ScreenshotPaths == null,
            "preview from another archive cannot cross into verified generation");
    }

    private static void RunCatalog()
    {
        MarketplaceResult completed = null;
        Check(MarketplaceRuntime.Start("catalog", null, false, delegate(MarketplaceResult result) { completed = result; }), "catalog request starts against the fixture helper");
        Stopwatch wait = Stopwatch.StartNew();
        while (MarketplaceRuntime.Busy && wait.ElapsedMilliseconds < 10000) { MarketplaceRuntime.Poll(); Thread.Sleep(20); }
        if (completed == null) throw new Exception("Catalog fixture did not complete");
    }
    private static void InstallHelper(string helper, string payload)
    {
        string responseTemplate = Path.Combine(Path.GetDirectoryName(helper), "response.json");
        File.WriteAllText(responseTemplate, payload);
        string script = "#!/bin/sh\n" +
            "op=$(sed -n 's/.*\"operationId\":\"\\([^\"]*\\)\".*/\\1/p' \"$4\")\n" +
            "sed \"s/OPERATION/$op/\" " + ShellQuote(responseTemplate) + " > \"$6\"\n";
        File.WriteAllText(helper, script);
        File.SetUnixFileMode(helper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(helper), "helper.json"), JsonConvert.SerializeObject(new { schemaVersion = 1, protocolVersion = 1, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(helper))).ToLowerInvariant() }));
    }
    private static void RunRecoveryProcess(string root, bool invalid)
    {
        ProcessStartInfo start = new ProcessStartInfo(Environment.ProcessPath);
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(typeof(MarketplaceChecks).Assembly.Location);
        start.ArgumentList.Add("--startup-recovery");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add(invalid ? "invalid" : "valid");
        start.UseShellExecute = false;
        using (Process process = Process.Start(start))
        {
            if (!process.WaitForExit(10000)) { process.Kill(); throw new Exception("Recovery fixture timed out"); }
            Check(process.ExitCode == 0, invalid ? "invalid replacement rejected after failed prior capture" : "helper recovery runs after failed prior capture");
        }
    }

    internal static void RecoveryProcess(string root, bool invalid)
    {
        if (OperatingSystem.IsWindows()) throw new Exception("Unix fixture only");
        BepInEx.Paths.GameRootPath = root;
        string stateRoot = MarketplaceRuntime.StateRoot;
        string current = "33333333333333333333333333333333";
        string pending = "44444444444444444444444444444444";
        MakeGeneration(stateRoot, current, false);
        MakeGeneration(stateRoot, pending, true);
        File.WriteAllText(Path.Combine(stateRoot, "generations/" + current + "/lock.json"), "{\"schemaVersion\":1,\"packages\":null}");
        File.WriteAllText(Path.Combine(stateRoot, "state.json"), JsonConvert.SerializeObject(new MarketplaceStateRecord { SchemaVersion = 1, Current = current, Pending = pending }));
        ManagedSnapshot recovered = new ManagedSnapshot { GenerationId = pending, ContentRoot = Path.GetFullPath(Path.Combine(stateRoot, "generations/" + pending + "/content")),
            Packages = invalid ? null : new List<PackageDescriptor> { new PackageDescriptor { PackageId = "fixture", ModGuid = "market.fixture", Enabled = true } } };
        string payload = JsonConvert.SerializeObject(new MarketplaceResult { SchemaVersion = 1, OperationId = "OPERATION", Ok = true, Active = recovered });
        string responseTemplate = Path.Combine(root, "response.json");
        File.WriteAllText(responseTemplate, payload);
        string helper = Path.Combine(root, "BepInEx/ftkmf/ftkmf-launcher-helper");
        string script = "#!/bin/sh\n" +
            "op=$(sed -n 's/.*\"operationId\":\"\\([^\"]*\\)\".*/\\1/p' \"$4\")\n" +
            "sed \"s/OPERATION/$op/\" " + ShellQuote(responseTemplate) + " > \"$6\"\n";
        File.WriteAllText(helper, script);
        File.SetUnixFileMode(helper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(helper), "helper.json"), JsonConvert.SerializeObject(new { schemaVersion = 1, protocolVersion = 1, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(helper))).ToLowerInvariant() }));
        MarketplaceRuntime.InitializeBeforeDiscovery();
        if (invalid) Check(MarketplaceRuntime.Active == null && MarketplaceRuntime.Notice.Contains("no package selection"), "unvalidated helper snapshot is never accepted");
        else Check(MarketplaceRuntime.Active != null && MarketplaceRuntime.Active.GenerationId == pending, "valid pending generation recovered from unreadable legacy current selection");
    }
    private static string ShellQuote(string value) { return "'" + value.Replace("'", "'\"'\"'") + "'"; }

    private static void MakeGeneration(string root, string id, bool enabled)
    {
        string generation = Path.Combine(Path.Combine(root, "generations"), id);
        Directory.CreateDirectory(Path.Combine(generation, "content"));
        File.WriteAllText(Path.Combine(generation, "lock.json"), JsonConvert.SerializeObject(new MarketplaceGenerationLock { SchemaVersion = 1,
            Packages = new List<PackageDescriptor> { new PackageDescriptor { PackageId = "fixture", ModGuid = "market.fixture", Version = "1.0.0", Enabled = enabled } } }));
    }
}
