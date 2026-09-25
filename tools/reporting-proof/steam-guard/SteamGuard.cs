using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FTKReportingProof
{
    // BepInEx discovers this preloader contract without instantiating a Unity object.
    public static class SteamGuard
    {
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                try { VerifyRoute(); }
                catch (Exception error) { Fail("Steam guard route failed: " + error.Message); }
                return new[] { "Assembly-CSharp-firstpass.dll" };
            }
        }

        private static void VerifyRoute()
        {
            string root = Environment.GetEnvironmentVariable("FTK_REPORTING_ROOT");
            if (Environment.GetEnvironmentVariable("FTK_REPORTING_PROOF") != "1" || String.IsNullOrEmpty(root))
                Fail("Steam guard only runs in an explicitly armed disposable process");
            DirectoryInfo directory = new DirectoryInfo(root);
            if (!directory.Exists || directory.Parent == null || directory.Parent.Name != "scratch"
                || (directory.Attributes & FileAttributes.ReparsePoint) != 0
                || !Path.GetFullPath(typeof(SteamGuard).Assembly.Location).StartsWith(
                    Path.Combine(directory.FullName, "BepInEx/patchers") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                Fail("Steam guard disposable root does not match process base directory");
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            try
            {
                VerifyRoute();
                Transform(assembly);
                string session = Environment.GetEnvironmentVariable("FTK_REPORTING_SESSION");
                if (String.IsNullOrEmpty(session)) throw new InvalidOperationException("Missing proof session");
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("FTK_REPORTING_ROOT"), "proof-steam-guard.receipt"), session);
            }
            catch (Exception error)
            {
                // BepInEx may recover from patcher exceptions by loading vanilla bytes.
                // Terminate this disposable process instead of allowing unguarded Steam.
                Fail("Steam guard refused assembly: " + error.Message);
            }
        }

        public static void Transform(AssemblyDefinition assembly)
        {
                if (assembly.Name.Name != "Assembly-CSharp-firstpass")
                    throw new InvalidOperationException("Unexpected Steam assembly");
                TypeDefinition api = assembly.MainModule.GetType("Steamworks.SteamAPI");
                if (api == null) throw new InvalidOperationException("Missing Steamworks.SteamAPI");
                MethodDefinition init = null;
                MethodDefinition restart = null;
                foreach (MethodDefinition method in api.Methods)
                {
                    if (method.Name == "Init" && method.IsStatic && method.ReturnType.FullName == "System.Boolean"
                        && method.Parameters.Count == 0 && method.HasBody)
                    {
                        if (init != null) throw new InvalidOperationException("Ambiguous Steam Init");
                        init = method;
                    }
                    if (method.Name == "RestartAppIfNecessary" && method.IsStatic && method.ReturnType.FullName == "System.Boolean"
                        && method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "Steamworks.AppId_t" && method.HasBody)
                    {
                        if (restart != null) throw new InvalidOperationException("Ambiguous Steam Restart");
                        restart = method;
                    }
                }
                if (init == null || restart == null) throw new InvalidOperationException("Missing exact Steam signatures");
                Block(init);
                Block(restart);
        }

        private static void Block(MethodDefinition method)
        {
            method.Body = new MethodBody(method);
            ILProcessor il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldc_I4_0));
            il.Append(il.Create(OpCodes.Ret));
        }

        private static void Fail(string reason)
        {
            Console.Error.WriteLine(reason);
            Environment.Exit(72);
            throw new InvalidOperationException(reason);
        }
    }
}
