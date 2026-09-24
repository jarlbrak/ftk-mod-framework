using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using FTKReportingProof;

class Program
{
    static void Require(bool value, string why) { if (!value) throw new Exception(why); }
    static byte[] Hash(string path) { return SHA256.HashData(File.ReadAllBytes(path)); }
    static void Main(string[] args)
    {
        string path = args[0];
        byte[] before = Hash(path);
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path))
        {
            var methods = assembly.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).ToArray();
            var bodies = methods.ToDictionary(m => m, m => m.Body);
            SteamGuard.Transform(assembly);
            var changed = methods.Where(m => m.Body != bodies[m]).ToArray();
            Require(changed.Length == 2, "Expected only two changed method bodies");
            foreach (var method in changed)
            {
                Require(method.DeclaringType.FullName == "Steamworks.SteamAPI", "Unexpected type");
                Require(method.Name == "Init" || method.Name == "RestartAppIfNecessary", "Unexpected method");
                Require(method.Body.Instructions.Count == 2 && method.Body.Instructions[0].OpCode == OpCodes.Ldc_I4_0
                    && method.Body.Instructions[1].OpCode == OpCodes.Ret, "Expected constant false return");
            }
        }
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path))
        {
            var api = assembly.MainModule.GetType("Steamworks.SteamAPI");
            api.Methods.Single(m => m.Name == "Init" && m.Parameters.Count == 0).Name = "RemovedInit";
            bool rejected = false;
            try { SteamGuard.Transform(assembly); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Missing signature was accepted");
            Require(api.Methods.Single(m => m.Name == "RestartAppIfNecessary").Body.Instructions.Count > 2, "Partial mutation before preflight");
        }
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path))
        {
            assembly.Name.Name = "UnexpectedAssembly";
            bool rejected = false;
            try { SteamGuard.Transform(assembly); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Wrong assembly was accepted");
        }
        Require(before.SequenceEqual(Hash(path)), "Source assembly changed on disk");
        Console.WriteLine("PASS: exact wrappers only, false return IL, missing/wrong identity rejected, source SHA-256 unchanged");
    }
}
