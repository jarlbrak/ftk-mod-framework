using System;
using System.IO;
using System.Reflection;
internal static class Program
{
    static void Main(string[] args)
    {
        if(args.Length!=1 || !Directory.Exists(args[0]))throw new ArgumentException("Pass installed game Managed directory");
        string managed=Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e)
        {
            string name=new AssemblyName(e.Name).Name;
            string path=Path.Combine(managed,name+".dll");
            if(File.Exists(path))return Assembly.LoadFrom(path);
            DirectoryInfo dir=new DirectoryInfo(managed);
            while(dir!=null)
            {
                path=Path.Combine(Path.Combine(dir.FullName,"BepInEx/core"),name+".dll");
                if(File.Exists(path))return Assembly.LoadFrom(path);
                dir=dir.Parent;
            }
            return null;
        };
        string plugin=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FTKModFramework.dll");
        Type clipper=Assembly.LoadFrom(plugin).GetType("FTKModFramework.Core.HeadFaceClipper",true);
        MethodInfo test=clipper.GetMethod("RunSyntheticTests",BindingFlags.NonPublic|BindingFlags.Static);
        test.Invoke(null,null);
        Console.WriteLine("PASS actual HeadFaceClipper synthetic geometry checks (installed Unity types; no game)");
    }
}
