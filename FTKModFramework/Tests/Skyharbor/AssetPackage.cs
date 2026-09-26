using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

static class AssetPackage
{
    internal static void Verify(string directory,string dll)
    {
        var assembly=Assembly.LoadFile(Path.GetFullPath(dll));
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"manifest.json")));
        foreach(var resource in manifest.RootElement.GetProperty("resources").EnumerateObject())
        {
            byte[] expected=File.ReadAllBytes(Path.Combine(directory,resource.Name));
            using var input=assembly.GetManifestResourceStream("FTKModFramework.assets.skyharbor."+resource.Name);
            if(input==null) throw new Exception("Missing embedded resource "+resource.Name);
            using var bytes=new MemoryStream();input.CopyTo(bytes);
            var actual=bytes.ToArray();
            string hash=Convert.ToHexString(SHA256.HashData(actual)).ToLowerInvariant();
            if(hash!=resource.Value.GetProperty("sha256").GetString()||actual.Length!=resource.Value.GetProperty("bytes").GetInt32()||!actual.AsSpan().SequenceEqual(expected))
                throw new Exception("Embedded resource differs from manifest "+resource.Name);
        }
        using var sceneFile=File.OpenRead(Path.Combine(directory,"diorama.json.gz"));
        using var gzip=new GZipStream(sceneFile,CompressionMode.Decompress);
        using var scene=JsonDocument.Parse(gzip);
        int vertices=0,triangles=0,meshes=0;
        foreach(var mesh in scene.RootElement.GetProperty("meshes").EnumerateArray())
        {
            meshes++;int count=mesh.GetProperty("vertices").GetArrayLength()/3;vertices+=count;
            if(count<3||count>65000) throw new Exception("Mesh budget");
            foreach(var index in mesh.GetProperty("triangles").EnumerateArray())
                if(index.GetInt32()<0||index.GetInt32()>=count) throw new Exception("Mesh index");
            triangles+=mesh.GetProperty("triangles").GetArrayLength()/3;
            if(mesh.TryGetProperty("texture",out var name)&&!manifest.RootElement.GetProperty("resources").TryGetProperty(name.GetString(),out _))
                throw new Exception("Missing scene texture "+name.GetString());
        }
        if(vertices>500000||vertices!=manifest.RootElement.GetProperty("vertices").GetInt32()||meshes!=manifest.RootElement.GetProperty("meshCount").GetInt32()||triangles!=manifest.RootElement.GetProperty("triangles").GetInt32())
            throw new Exception("Scene totals differ from manifest");
        Console.WriteLine("Embedded scene/texture hashes, mesh budgets and references passed.");
    }
}
