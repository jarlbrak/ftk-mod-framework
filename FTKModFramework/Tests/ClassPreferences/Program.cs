using System;
using System.Collections.Generic;
using System.IO;
using FTKModFramework.Core.HotReload;
sealed class Store : IClassPreferenceStore
{
    public Dictionary<string,int> Values = new Dictionary<string,int>();
    public bool FailSave;
    public bool Has(string k) { return Values.ContainsKey(k); }
    public int Get(string k) { return Has(k) ? Values[k] : -1; }
    public void Set(string k,int v) { Values[k]=v; }
    public void Delete(string k) { Values.Remove(k); }
    public void Save() { if(FailSave) throw new IOException("save failed"); }
}
static class Program
{
    static void Check(bool b) { if(!b) throw new Exception("assertion"); }
    static void Reject(Action a) { try { a(); } catch (IOException) { return; } catch(InvalidOperationException) { return; } throw new Exception("expected rejection"); }
    static void Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"ftk-preferences-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try {
            Store s=new Store(); s.Set("Player0class",2); s.Set("Player1class",-1);
            PreferenceTransaction t=new PreferenceTransaction(s,new[]{"smith","hunter","paladin"});
            t.Plan(new[]{"hunter","smith"},1); t.Prepare(root,"old","new");
            Check(s.Get("Player0class")==2); // preparation is side-effect free for preferences
            t.Complete(true);
            Check(s.Get("Player0class")==1 && s.Get("Player1class")==-1 && !s.Has("Player2class"));
            t=new PreferenceTransaction(s,new[]{"hunter","smith"}); t.Plan(new[]{"smith","hunter"},0); t.Prepare(root,"old","new");
            PreferenceTransaction.Recover(root,"new",s); Check(s.Get("Player0class")==0);
            t=new PreferenceTransaction(s,new[]{"smith","hunter"}); t.Plan(new[]{"hunter","smith"},0); t.Prepare(root,"old","new");
            PreferenceTransaction.Recover(root,"old",s); Check(s.Get("Player0class")==0);
            t=new PreferenceTransaction(s,new[]{"smith","hunter"}); t.Plan(new[]{"hunter","smith"},0); t.Prepare(root,"old","new");
            s.FailSave=true; Reject(()=>t.Complete(true)); Check(File.Exists(PreferenceTransaction.PathFor(root)));
            s.FailSave=false; PreferenceTransaction.Recover(root,"new",s); Check(s.Get("Player0class")==1);
            t=new PreferenceTransaction(s,new[]{"hunter","smith"}); t.Plan(new[]{"smith","hunter"},0); s.Set("Player0class",99);
            Reject(()=>t.Prepare(root,"old","new")); Check(!File.Exists(PreferenceTransaction.PathFor(root)));
            s.Set("Player0class",1); t.Prepare(root,"old","new"); s.Set("Player0class",99);
            Reject(()=>PreferenceTransaction.Recover(root,"new",s)); Check(s.Get("Player0class")==99);
            s.Set("Player0class",1); Reject(()=>PreferenceTransaction.Recover(root,"unrelated",s));
            t.Complete(false); Check(s.Get("Player0class")==1);
            s.Set("Player0class",50); t=new PreferenceTransaction(s,new[]{"smith"});
            Reject(()=>t.Plan(new[]{"smith"},-1));
            File.WriteAllText(PreferenceTransaction.PathFor(root),"invalid"); Reject(()=>PreferenceTransaction.Recover(root,"new",s));
            Console.WriteLine("Class preference semantic remap, fallback, rollback, crash recovery, partial writes and concurrent edit checks passed.");
        } finally { Directory.Delete(root,true); }
    }
}
