using System;
using System.IO;
using System.Security.Cryptography;
using FTKModFramework.Core;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

internal static class Program
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action, string message) { bool refused = false; try { action(); } catch (ArgumentException) { refused = true; } catch (IOException) { refused = true; } Check(refused, message); }
    static string Hash(string path) { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ftk-asset-admission-" + Guid.NewGuid().ToString("N"));
        try
        {
            string manual = Path.Combine(root, "manual/possum");
            string managedRoot = Path.Combine(root, "generation/content");
            string thief = Path.Combine(managedRoot, "ftkmf.thief");
            foreach (string folder in new string[] { manual, thief }) { Directory.CreateDirectory(Path.Combine(folder, "assets")); File.WriteAllText(Path.Combine(folder, "assets/model.glb"), "model fixture"); }
            string asset = Path.Combine(thief, "assets/model.glb");
            ManagedSnapshot snapshot = new ManagedSnapshot { ContentRoot = managedRoot, FilesVerified = true };
            snapshot.Packages.Add(new PackageDescriptor { PackageId = "ftkmf.thief", ModGuid = "com.ftkmf.thief" });
            snapshot.Files.Add(new MarketplaceGenerationFile { Path = "ftkmf.thief/assets/model.glb", Sha256 = Hash(asset), Size = new FileInfo(asset).Length });
            ContentAssetAdmission admission = new ContentAssetAdmission(snapshot);
            Check(admission.Register("com.ftkmf.possum", manual, "assets/model.glb") == PackageModelPaths.Register("com.ftkmf.possum", manual, "assets/model.glb"), "Manual Possum remains outside managed lock.");
            Check(admission.Register("com.ftkmf.thief", thief, "assets/model.glb") == PackageModelPaths.RegisterVerified("com.ftkmf.thief", thief, "assets/model.glb", Hash(asset), new FileInfo(asset).Length), "Managed Thief uses verified file identity.");
            Check(admission.Register("com.ftkmf.thief", Path.Combine(thief, "../ftkmf.thief"), "assets/model.glb") != null, "Canonical equivalent managed root accepted.");
            Reject(delegate { admission.Register("com.ftkmf.thief", manual, "assets/model.glb"); }, "Manual spoof of managed GUID refused.");
            Reject(delegate { admission.Register("com.ftkmf.possum", thief, "assets/model.glb"); }, "Managed source cannot pretend to be manual.");
            Reject(delegate { admission.Register("com.ftkmf.thief", thief + "-other", "assets/model.glb"); }, "Prefix sibling cannot use managed identity.");
            File.WriteAllText(Path.Combine(thief, "assets/unlocked.glb"), "unlocked");
            Reject(delegate { admission.Register("com.ftkmf.thief", thief, "assets/unlocked.glb"); }, "Missing lock entry fails closed despite existing file.");
            snapshot.Files[0].Sha256 = "not-a-canonical-hash";
            Reject(delegate { new ContentAssetAdmission(snapshot).Register("com.ftkmf.thief", thief, "assets/model.glb"); }, "Malformed managed hash fails closed.");
            snapshot.Files[0].Sha256 = Hash(asset);
            Check(PackageModelPaths.Resolve(new ContentAssetAdmission(snapshot).Register("com.ftkmf.thief", thief, "assets/model.glb")) == asset, "Verified managed identity resolves its exact source.");
            string manualIdentity = admission.Register("com.ftkmf.possum", manual, "assets/model.glb");
            File.WriteAllText(Path.Combine(manual, "assets/model.glb"), "model fixturf");
            Reject(delegate { PackageModelPaths.Resolve(manualIdentity); }, "Manual same-size hash tampering is rejected at resolve.");
            File.WriteAllText(Path.Combine(manual, "assets/model.glb"), "model fixture");
            snapshot.Files[0].Size++;
            Reject(delegate { new ContentAssetAdmission(snapshot).Register("com.ftkmf.thief", thief, "assets/model.glb"); }, "Wrong managed size fails closed.");
            snapshot.Files[0].Size--; snapshot.FilesVerified = false;
            Reject(delegate { new ContentAssetAdmission(snapshot).Register("com.ftkmf.thief", thief, "assets/model.glb"); }, "Unverified generation cannot fallback to manual admission.");
            Check(new ContentAssetAdmission(null).Register("com.ftkmf.possum", manual, "assets/model.glb") != null, "Manual-only loader remains supported.");
            Console.WriteLine("PASS: " + checks + " real loader asset-admission checks");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
