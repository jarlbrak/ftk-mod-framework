using System;
using System.Collections.Generic;
using System.IO;
using FTKModFramework.Core.HotReload;
static class Program
{
    static void Check(bool value) { if (!value) throw new Exception("assertion"); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (IOException) { return; } throw new Exception("expected rejection"); }
    static void Main()
    {
        string a=new string('a',64),b=new string('b',64);
        Check(SaveSetIdentity.Compute(a,"1.0.0",new Dictionary<string,string>{{"com.ftkmf.paladin",b}},
            new Dictionary<string,string>{{"dataContent","True"},{"campaignEngine","True"},{"sampleContent","False"},{"behaviorLoading","False"}})
            == "c29b7774b66ecd083cb64456430920bd6ecd70d4e92f1158b0a04f5a602f71d7");
        var packages=new Dictionary<string,string>{{"paladin",a},{"other",b}};
        var reversed=new Dictionary<string,string>{{"other",b},{"paladin",a}};
        var settings=new Dictionary<string,string>{{"b","true"},{"a","false"}};
        string id=SaveSetIdentity.Compute(a,"1",packages,settings);
        Check(id==SaveSetIdentity.Compute(a.ToUpperInvariant(),"1",reversed,settings));
        Check(id!=SaveSetIdentity.Compute(b,"1",packages,settings));
        Check(id!=SaveSetIdentity.Compute(a,"2",packages,settings));
        packages["paladin"]=b; Check(id!=SaveSetIdentity.Compute(a,"1",packages,settings)); packages["paladin"]=a;
        settings["a"]="true"; Check(id!=SaveSetIdentity.Compute(a,"1",packages,settings));
        Reject(()=>SaveSetIdentity.DirectoryFor("/tmp","../../escape"));
        string root=Path.Combine(Path.GetTempPath(),"ftk-save-set-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        try {
            string library=Path.Combine(root,"library"); Directory.CreateDirectory(library);
            string save=Path.Combine(library,"fresh.run"); File.WriteAllText(save,"test");
            Check(SaveSetIdentity.IsSavePath(library,save,true));
            string mixedCase=Path.Combine(library,"mixed.Run"); File.WriteAllText(mixedCase,"case fixture");
            Check(SaveSetIdentity.IsSavePath(library,mixedCase,true));
            Check(!SaveSetIdentity.IsSavePath(library,"fresh.run",true));
            Check(!SaveSetIdentity.IsSavePath(library,Path.Combine(root,"fresh.run"),false));
            Check(!SaveSetIdentity.IsSavePath(library,Path.Combine(library,"nested/fresh.run"),false));
            Check(!SaveSetIdentity.IsSavePath(library,Path.Combine(library,"fresh.jpg"),false));
            Check(!SaveSetIdentity.IsSavePath(library,Path.Combine(library,"missing.run"),true));
            Check(SaveSetIdentity.IsSavePath(library,Path.Combine(library,"new.run"),false));
            string external=Path.Combine(root,"external.run"); File.WriteAllText(external,"external");
            string linked=Path.Combine(library,"linked.run"); File.CreateSymbolicLink(linked,external);
            Check(!SaveSetIdentity.IsSavePath(library,linked,true));
            string alias=Path.Combine(root,"alias"); Directory.CreateSymbolicLink(alias,library);
            Check(!SaveSetIdentity.IsSavePath(alias,Path.Combine(alias,"fresh.run"),true));
            Reject(()=>SaveSetIdentity.CreateOwnedDirectory(alias));
            string empty=Path.Combine(root,"empty"); Directory.CreateDirectory(empty);
            string emptyLink=Path.Combine(root,"empty-link"); Directory.CreateSymbolicLink(emptyLink,empty);
            Reject(()=>SaveSetIdentity.ValidateOwnedDirectory(emptyLink));
            Reject(()=>SaveSetIdentity.CreateOwnedDirectory(emptyLink));
            Check(!SaveSetIdentity.IsSavePath(emptyLink,Path.Combine(emptyLink,"new.run"),false));
            string dangling=Path.Combine(root,"dangling"); Directory.CreateSymbolicLink(dangling,Path.Combine(root,"absent"));
            Reject(()=>SaveSetIdentity.CreateOwnedDirectory(dangling));
            string badState=Path.Combine(root,"bad-state"); Directory.CreateDirectory(badState);
            Directory.CreateSymbolicLink(Path.Combine(badState,"save-pins"),empty);
            Reject(()=>SaveSetIdentity.WritePin(badState,id,new string('a',32)));
            string generation=new string('a',32),other=new string('b',32);
            SaveSetIdentity.WritePin(root,id,generation);
            string path=Path.Combine(Path.Combine(root,"save-pins"),id+".json");
            string emptyId=SaveSetIdentity.Compute(a,"1",new Dictionary<string,string>(),settings);
            SaveSetIdentity.WritePin(root,emptyId,other);
            Check(File.ReadAllText(Path.Combine(Path.Combine(root,"save-pins"),emptyId+".json")).Contains(other));
            string initial=File.ReadAllText(path); Check(initial.Contains(generation));
            SaveSetIdentity.WritePin(root,id,other); Check(initial==File.ReadAllText(path));
            Check(SaveSetIdentity.DirectoryFor(root,id)==SaveSetIdentity.DirectoryFor(root,id.ToUpperInvariant()));
            File.Delete(path); File.CreateSymbolicLink(path,external);
            Reject(()=>SaveSetIdentity.WritePin(root,id,generation));
            File.Delete(path); File.CreateSymbolicLink(path,Path.Combine(root,"missing-pin"));
            Reject(()=>SaveSetIdentity.WritePin(root,id,generation));
            File.Delete(path);
            File.WriteAllText(path,"broken"); Reject(()=>SaveSetIdentity.WritePin(root,id,generation));
            Reject(()=>SaveSetIdentity.WritePin(root,id,"../escape"));
        } finally { Directory.Delete(root,true); }
        Console.WriteLine("Save identity determinism, isolation, pin retention and corruption checks passed.");
    }
}
