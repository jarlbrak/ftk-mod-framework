using System;
using System.IO;
using System.Collections.Generic;
using FTKModFramework.Core.UI;
using FTKModFramework.Core.HotReload;
using FTKModFramework.Core.Marketplace;
namespace UnityEngine { static class Application { internal static string persistentDataPath; } }
namespace FTKModFramework.Core { static class Plugin {internal const string Version="1";} }
namespace FTKModFramework.Core.Marketplace {
 class PackageDescriptor {internal string Name="Paladin",ModGuid="paladin",Version="1";internal bool Enabled=true;}
 class ManagedSnapshot {internal string GenerationId;internal List<PackageDescriptor> Packages=new List<PackageDescriptor>{new PackageDescriptor()};}
 class MarketplaceResult {internal string Message,Status;internal bool Ok;}
 static class MarketplaceProtocol {internal static ManagedSnapshot ReadGeneration(string root,string id){return new ManagedSnapshot{GenerationId=id};}}
 static class MarketplaceRuntime {internal static string StateRoot,Notice;internal static ManagedSnapshot Active,Pending; internal static bool RestoreSavedSet(string a,string b,string c,Action<MarketplaceResult>d){return false;}}
}
namespace FTKModFramework.Core.HotReload {
 static class SaveNamespace {internal static string Expected;internal static string FingerprintFor(ManagedSnapshot s){return Expected;}}
 static class HotReloadCoordinator {internal static string UnavailableReason(){return null;}}
}
namespace FTKModFramework.Core.UI {
 internal sealed partial class ModsPanel {
  int _page;string _message="";static bool PanelBusy=false;
  void TextLine(string s,int i,int j){} void Navigate(string s){} void Refresh(){} void PageButtons(int i){}
  void ActionButton(string s,Action a,bool b=true){} void PrimaryButton(string s,Action a,bool b=true){}
 }
}
class Program {
 static void Check(bool b){if(!b)throw new Exception("assertion");}
 static void Main(){string root=Path.Combine(Path.GetTempPath(),"ftk-saved-sets-"+Guid.NewGuid());Directory.CreateDirectory(root);try{
  MarketplaceRuntime.StateRoot=root;UnityEngine.Application.persistentDataPath=root;
  string fp=new string('a',64),gen=new string('b',32);SaveNamespace.Expected=fp;
  string pins=Path.Combine(root,"save-pins");Directory.CreateDirectory(pins);string pin=Path.Combine(pins,fp+".json");
  File.WriteAllText(pin,"{\"schemaVersion\":1,\"generation\":\""+gen+"\"}");
  string folder=Path.Combine(Path.Combine(root,"generations"),gen);Directory.CreateDirectory(folder);string record=Path.Combine(folder,"lock.json");File.WriteAllText(record,"{\"frameworkVersion\":\"1\"}");
  string library=SaveSetIdentity.DirectoryFor(root,fp);Directory.CreateDirectory(library);File.WriteAllText(Path.Combine(library,"test.Run"),"NOT A SAVE; MUST NEVER BE PARSED");
  var item=ModsPanel.ReadSavedSet(pin);Check(item.Unavailable==null&&item.SaveCount==1&&item.Summary.Contains("Paladin"));
  SaveNamespace.Expected=new string('c',64);Check(ModsPanel.ReadSavedSet(pin).Unavailable!=null);SaveNamespace.Expected=fp;
  File.WriteAllText(record,"{\"frameworkVersion\":\"other\"}");Check(ModsPanel.ReadSavedSet(pin).Unavailable!=null);File.WriteAllText(record,"{\"frameworkVersion\":\"1\"}");
  File.CreateSymbolicLink(Path.Combine(library,"linked.RUN"),pin);Check(ModsPanel.ReadSavedSet(pin).Unavailable!=null);File.Delete(Path.Combine(library,"linked.RUN"));
  File.WriteAllText(pin,"broken");Check(ModsPanel.ReadSavedSet(pin).Unavailable!=null);
  File.WriteAllText(pin,new string('a',4097));Check(ModsPanel.ReadSavedSet(pin).Unavailable!=null);
  File.Delete(pin);File.CreateSymbolicLink(pin,record);Check(ModsPanel.ReadSavedSet(pin).Unavailable!=null);
  Console.WriteLine("Saved-set reader counts filenames without parsing saves; incompatible, corrupt, oversized and linked records fail closed.");
 }finally{Directory.Delete(root,true);}}
}
