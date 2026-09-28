using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static RuntimeModelTest packageFitObserver;
    string packageFitRoot, packageFitSourceError;
    bool packageFitLoading, packageFitLoaded, packageFitDiscovered;
    int packageFitLoads;
    JObject packageFitFiles, packageFitSources, packageFitAssets;
    readonly Dictionary<string, object> packageFitRegistryRows = new Dictionary<string, object>(StringComparer.Ordinal);
    bool packageFitUncertain, packageFitMutationVerified;
    JObject packageFitFailure, packageFitMutationBefore, packageFitMutationDiagnostics;
    CharacterOverworld packageFitMutationHero;
    string packageFitMutationOperation;

    static object PackageFitMember(object owner, string name)
    {
        FieldInfo field = owner.GetType().GetField(name, Members);
        if (field != null) return field.GetValue(owner);
        return owner.GetType().GetProperty(name, Members).GetValue(owner, null);
    }
    string PackageFitConfiguredRoot()
    {
        Type plugin = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Plugin", true);
        return Path.GetFullPath((string)plugin.GetProperty("DataContentRootPath", Statics).GetValue(null, null)).TrimEnd(Path.DirectorySeparatorChar);
    }
    void InstallPackageFitObserver(Harmony harmony)
    {
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST_PACKAGE_ONLY") != "1" ||
            !File.Exists(Path.Combine(root, "model-test-package-gear.json"))) return;
        packageFitObserver = this;
        Assembly framework = CatalogAssembly("FTKModFramework");
        Type registry = framework.GetType("FTKModFramework.Core.Data.ModRegistry", true);
        foreach (object entry in (IEnumerable)registry.GetProperty("Entries", Statics).GetValue(null, null))
            if ((string)PackageFitMember(entry, "Key") == "com.ftkmf.paladin" || (string)PackageFitMember(entry, "Key") == "com.ftkmf.thief")
                throw new InvalidOperationException("Fit observer installed after candidate registration; fresh process required.");
        MethodInfo load = framework.GetType("FTKModFramework.Core.Data.ContentLoader", true).GetMethod("LoadInternal", Statics);
        MethodInfo discover = framework.GetType("FTKModFramework.Core.Data.ModDiscovery", true).GetMethod("DiscoverAll", Statics);
        if (load == null || discover == null) throw new InvalidOperationException("Exact package load observation points unavailable.");
        harmony.Patch(load, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PackageFitLoadPrefix", Statics)),
            new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PackageFitLoadPostfix", Statics)), null,
            new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PackageFitLoadFinalizer", Statics)), null);
        harmony.Patch(discover, null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PackageFitDiscoveryPostfix", Statics)));
    }
    static void PackageFitLoadPrefix(string contentRoot)
    {
        RuntimeModelTest observer = packageFitObserver;
        if (observer == null) return;
        try { observer.PackageFitBeforeLoad(contentRoot); }
        catch (Exception error) { observer.packageFitSourceError = error.ToString(); }
    }
    void PackageFitBeforeLoad(string contentRoot)
    {
        if (++packageFitLoads != 1) throw new InvalidOperationException("A second content load invalidates the fit session.");
        packageFitLoading = true;
        packageFitRoot = PackageFitConfiguredRoot();
        if (Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar) != packageFitRoot ||
            !packageFitRoot.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Initial load must use the configured owned manual content root.");
        string config = Path.Combine(root, "model-test-package-gear.json");
        PackageFitItems(CatalogHash(config));
        packageFitFiles = new JObject(); packageFitSources = new JObject();
        foreach (JObject package in (JArray)JObject.Parse(File.ReadAllText(config))["packages"])
        {
            string manifest = PackageFitFile(Str(package, "manifest"), Str(package, "manifestSha256"));
            string content = PackageFitFile(Str(package, "content"), Str(package, "contentSha256"));
            string folder = Path.GetDirectoryName(manifest);
            if (Path.GetDirectoryName(folder) != packageFitRoot) throw new InvalidOperationException("Candidate is not an immediate child of the actual configured discovery root.");
            packageFitSources[Str(package, "modGuid")] = new JObject { { "folder", folder }, { "version", Str(package, "version") },
                { "manifest", manifest }, { "content", content } };
            packageFitFiles[manifest] = CatalogHash(manifest); packageFitFiles[content] = CatalogHash(content);
            string assets = Path.Combine(folder, "assets");
            if (!Directory.Exists(assets)) throw new InvalidOperationException("Candidate assets directory required.");
            CatalogNoLinks(assets);
            // Walk without following symlink directories; bounded before registration starts.
            var pending = new Queue<string>(); pending.Enqueue(assets); int count = 0; long bytes = 0;
            while (pending.Count > 0)
            {
                string directory = pending.Dequeue(); CatalogNoLinks(directory);
                foreach (string child in Directory.GetDirectories(directory)) { CatalogNoLinks(child); pending.Enqueue(child); if (++count > 4096) throw new InvalidOperationException("Candidate asset tree exceeds bound."); }
                foreach (string path in Directory.GetFiles(directory))
                {
                    CatalogNoLinks(path); bytes = checked(bytes + new FileInfo(path).Length);
                    if (++count > 4096 || bytes > 512L * 1024 * 1024) throw new InvalidOperationException("Candidate asset bytes exceed bound.");
                    packageFitFiles[path] = CatalogHash(path);
                }
            }
        }
    }
    static void PackageFitDiscoveryPostfix(string manualRoot, object __result)
    {
        RuntimeModelTest observer = packageFitObserver;
        if (observer == null || !observer.packageFitLoading || observer.packageFitSourceError != null) return;
        try
        {
            if (Path.GetFullPath(manualRoot).TrimEnd(Path.DirectorySeparatorChar) != observer.packageFitRoot || observer.packageFitDiscovered)
                throw new InvalidOperationException("Discovery does not match the one initial content load.");
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (object mod in (IEnumerable)__result)
            {
                object manifest = PackageFitMember(mod, "Manifest");
                string guid = (string)PackageFitMember(manifest, "ModGuid");
                JObject source = observer.packageFitSources[guid] as JObject;
                if (source == null) continue;
                IList files = (IList)PackageFitMember(mod, "ContentFilePaths");
                if (!found.Add(guid) || (string)PackageFitMember(manifest, "FolderPath") != (string)source["folder"] ||
                    (string)PackageFitMember(manifest, "Version") != (string)source["version"] || files.Count != 1 ||
                    (string)files[0] != (string)source["content"] || PackageFitMember(mod, "BehaviorDllPath") != null)
                    throw new InvalidOperationException("Actual discovered candidate source/version/content differs from startup pins.");
            }
            if (found.Count != 2) throw new InvalidOperationException("Both pinned candidates must be present in actual initial discovery.");
            observer.PackageFitCheckFiles(); observer.packageFitDiscovered = true;
        }
        catch (Exception error) { observer.packageFitSourceError = error.ToString(); }
    }
    static void PackageFitLoadPostfix(object __result)
    {
        RuntimeModelTest observer = packageFitObserver;
        if (observer == null || observer.packageFitSourceError != null) return;
        try
        {
            if (!observer.packageFitDiscovered || __result == null ||
                (int)PackageFitMember(__result, "RegisteredCount") != (int)PackageFitMember(__result, "TotalCount"))
                throw new InvalidOperationException("Complete observed initial registration required.");
            observer.PackageFitCheckFiles();
            observer.PackageFitCheckRegistry(true);
            observer.packageFitAssets = observer.PackageFitRegisteredAssets();
            observer.packageFitLoaded = true;
        }
        catch (Exception error) { observer.packageFitSourceError = error.ToString(); }
        finally { observer.packageFitLoading = false; }
    }
    static Exception PackageFitLoadFinalizer(Exception __exception)
    {
        if (__exception != null && packageFitObserver != null)
        {
            packageFitObserver.packageFitSourceError = __exception.ToString();
            packageFitObserver.packageFitLoading = false;
        }
        return __exception;
    }
    void PackageFitCheckFiles()
    {
        foreach (JProperty file in packageFitFiles.Properties())
        {
            CatalogNoLinks(file.Name);
            if (!File.Exists(file.Name) || CatalogHash(file.Name) != (string)file.Value)
                throw new InvalidOperationException("Candidate file changed since initial load: " + file.Name);
        }
    }
    void PackageFitCheckRegistry(bool capture)
    {
        Type registry = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.Data.ModRegistry", true);
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (object entry in (IEnumerable)registry.GetProperty("Entries", Statics).GetValue(null, null))
        {
            string guid = (string)PackageFitMember(entry, "Key"); JObject source = packageFitSources[guid] as JObject;
            if (source == null) continue;
            if (!found.Add(guid) || !(bool)PackageFitMember(entry, "Enabled") || !(bool)PackageFitMember(entry, "FrameworkCompatible") ||
                (bool)PackageFitMember(entry, "IsManaged") || (string)PackageFitMember(entry, "Version") != (string)source["version"])
                throw new InvalidOperationException("Loaded manual candidate registry version/state differs.");
            if (capture) packageFitRegistryRows.Add(guid, entry);
            else if (!ReferenceEquals(packageFitRegistryRows[guid], entry)) throw new InvalidOperationException("Loaded candidate registry identity changed.");
        }
        if (found.Count != 2) throw new InvalidOperationException("Both loaded candidate registry entries required.");
    }
    JObject PackageFitRegisteredAssets()
    {
        Type paths = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.PackageModelPaths", true);
        JObject result = new JObject();
        foreach (DictionaryEntry pair in (IDictionary)paths.GetField("Paths", Statics).GetValue(null))
        {
            string folder = (string)PackageFitMember(pair.Value, "Root"); bool owned = false;
            foreach (JProperty source in packageFitSources.Properties()) if ((string)source.Value["folder"] == folder) owned = true;
            if (!owned) continue;
            string path = (string)PackageFitMember(pair.Value, "Path"), hash = (string)PackageFitMember(pair.Value, "Hash");
            if (packageFitFiles[path] == null || (string)packageFitFiles[path] != hash)
                throw new InvalidOperationException("Registered model/texture bytes differ from initial candidate files.");
            result[(string)pair.Key] = new JObject { { "path", path }, { "sha256", hash }, { "bytes", (long)PackageFitMember(pair.Value, "Size") } };
        }
        if (!result.HasValues) throw new InvalidOperationException("Registered candidate model/texture identities required.");
        return result;
    }
    void RequirePackageFitLoaded()
    {
        if (!packageFitLoaded || packageFitSourceError != null || packageFitLoads != 1 || packageFitLoading ||
            PackageFitConfiguredRoot() != packageFitRoot) throw new InvalidOperationException("Observed initial candidate registration required: " + packageFitSourceError);
        PackageFitCheckFiles(); PackageFitCheckRegistry(false);
        if (!BlacksmithGrantPreservedEquals(packageFitAssets, PackageFitRegisteredAssets()))
            throw new InvalidOperationException("Registered candidate asset identities changed.");
    }
    JObject PackageGearState(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        return new JObject { { "ok", true }, { "readOnly", true }, { "session", sessionId }, { "uncertain", packageFitUncertain },
            { "failure", packageFitFailure }, { "initialLoadObserved", packageFitLoaded }, { "sourceError", packageFitSourceError },
            { "configSha256", packageGearConfigHash }, { "contentRoot", packageFitRoot }, { "sources", packageFitSources },
            { "startupFileHashes", packageFitFiles }, { "registeredAssets", packageFitAssets },
            { "scope", "Initial loader/discovery/registry joins and registered model/texture file identity. Renderer application and fit require current-avatar evidence." } };
    }
    void PackageFitBeginMutation(CharacterOverworld hero, string operation)
    {
        if (packageFitUncertain) throw new InvalidOperationException("Uncertain package fit mutation; restart the isolated session.");
        JObject before = PackageFitMutationSnapshot(hero);
        packageFitMutationBefore = before; packageFitMutationDiagnostics = null;
        packageFitMutationHero = hero; packageFitMutationOperation = operation; packageFitMutationVerified = false; packageFitUncertain = true;
    }
    static JObject PackageFitMutationSnapshot(CharacterOverworld hero)
    {
        return new JObject { { "heroInstanceId", hero.GetInstanceID() },
            { "celInstanceId", hero.m_Avatar == null ? 0 : hero.m_Avatar.GetInstanceID() },
            { "skinType", (int)hero.m_SkinType }, { "preserved", BlacksmithAppearancePreserved(hero) },
            { "stats", BlacksmithAppearanceScalars(hero.m_CharacterStats) } };
    }
    static JArray PackageFitDifferences(JToken before, JToken after)
    {
        JArray result = new JArray(); PackageFitCollectDifferences(before, after, "$", result); return result;
    }
    static void PackageFitCollectDifferences(JToken before, JToken after, string path, JArray result)
    {
        if (BlacksmithGrantPreservedEquals(before, after)) return;
        JObject a = before as JObject, b = after as JObject;
        if (a != null && b != null)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty field in a.Properties()) names.Add(field.Name);
            foreach (JProperty field in b.Properties()) names.Add(field.Name);
            var ordered = new List<string>(names); ordered.Sort(StringComparer.Ordinal);
            foreach (string name in ordered) PackageFitCollectDifferences(a[name], b[name], path + "." + name, result);
            return;
        }
        JArray aa = before as JArray, bb = after as JArray;
        if (aa != null && bb != null)
        {
            for (int i = 0; i < Math.Max(aa.Count, bb.Count); i++)
                PackageFitCollectDifferences(i < aa.Count ? aa[i] : null, i < bb.Count ? bb[i] : null, path + "[" + i + "]", result);
            return;
        }
        result.Add(new JObject { { "path", path }, { "beforePresent", before != null }, { "afterPresent", after != null },
            { "before", before == null ? null : before.DeepClone() }, { "after", after == null ? null : after.DeepClone() } });
    }
    JObject PackageFitFailureReceipt(Exception error)
    {
        JObject partial = null; string observationError = null;
        try { if (packageFitMutationHero != null) partial = PackageFitMutationSnapshot(packageFitMutationHero); }
        catch (Exception observation) { observationError = observation.ToString(); }
        packageFitFailure = new JObject { { "operation", packageFitMutationOperation }, { "error", error.ToString() },
            { "before", packageFitMutationBefore }, { "partialAfter", partial },
            { "partialState", partial == null ? null : partial["preserved"] },
            { "differences", partial == null ? null : PackageFitDifferences(packageFitMutationBefore, partial) },
            { "diagnostics", packageFitMutationDiagnostics }, { "partialObservationError", observationError } };
        return new JObject { { "ok", false }, { "uncertain", true }, { "requiresFreshSession", true }, { "failure", packageFitFailure } };
    }
    JObject GuardPackageFitCommand(JObject command, int operation)
    {
        if (packageFitUncertain) throw new InvalidOperationException("Package fit state is uncertain; inspect package-gear-state and restart the isolated session.");
        try
        {
            JObject result = operation == 2 ? BlacksmithAppearanceFixture(command, true) : PackageGearFixture(command, operation == 1);
            if (packageFitUncertain && packageFitMutationVerified && result["ok"] != null && (bool)result["ok"])
                packageFitUncertain = false;
            return result;
        }
        catch (Exception error)
        {
            if (!packageFitUncertain) throw;
            return PackageFitFailureReceipt(error);
        }
    }
    void PackageFitCommandBoundary(string op)
    {
        if (!packageFitUncertain) return;
        foreach (string observation in new[] { "package-gear-state", "class-appearance-roster", "equipment-inventory", "item-visual-state",
            "blacksmith-combat-state", "player-studio", "native-inventory-capture", "world-input-state", "native-gear-metadata", "player-preview-state" })
            if (op == observation) return;
        throw new InvalidOperationException("Uncertain package fit mutation blocks further setup commands. Inspect package-gear-state; restart before further mutations.");
    }
}
