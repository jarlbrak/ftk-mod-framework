using System;
using Newtonsoft.Json.Linq;
class Program
{
 static int tests;
 static void Check(bool v,string m){tests++;if(!v)throw new Exception(m);}
 static void Reject(Action a,string m){try{a();}catch(InvalidOperationException){tests++;return;}throw new Exception("Accepted "+m);}
 static JObject State()=>JObject.Parse("{\"adventure\":\"HollowMire\",\"dungeonKey\":\"FloodedCrypt\",\"heroInstanceId\":5,\"dungeonInstanceId\":9,\"hexInstanceId\":11,\"movementInstanceId\":7,\"gameDefinitionIdentity\":31,\"livingSingleParty\":true,\"outsideCombat\":true,\"outsideDungeon\":true,\"inSession\":true,\"locked\":false,\"deactivated\":false,\"targetOwnsHex\":true,\"moving\":false,\"syncWalkingCow\":0,\"waiting\":false,\"movementEnabled\":true,\"movementState\":\"Tracking\",\"stopAtHexCheckActive\":false,\"turnQuestCheckActive\":false,\"questCheckEnabled\":false,\"storedCheckConsumed\":true,\"storedSetupConsumed\":true,\"storyComplete\":true,\"atTargetHex\":true,\"expectedQuestRegistered\":true,\"expectedQuestDefinition\":true,\"expectedQuestDestination\":true}");
 static void Main()
 {
  var s=State();EntryPreparationPolicy.Quiescent(s);Check(EntryPreparationPolicy.Ready(s,1,0,true),"Exact completion accepted");
  foreach(string key in new[]{"livingSingleParty","outsideCombat","outsideDungeon","inSession","targetOwnsHex","movementEnabled","storedCheckConsumed","storedSetupConsumed","storyComplete"}){var bad=State();bad[key]=false;Reject(()=>EntryPreparationPolicy.Quiescent(bad),key);}
  foreach(string key in new[]{"locked","deactivated","moving","waiting","stopAtHexCheckActive","turnQuestCheckActive","questCheckEnabled"}){var bad=State();bad[key]=true;Reject(()=>EntryPreparationPolicy.Quiescent(bad),key);}
  foreach(string key in new[]{"heroInstanceId","dungeonInstanceId","hexInstanceId"}){var bad=State();bad[key]=0;Reject(()=>EntryPreparationPolicy.Quiescent(bad),key);}
  foreach(string state in new[]{"OnStopAtHex","CheckQuest","unknown"}){var bad=State();bad["movementState"]=state;Reject(()=>EntryPreparationPolicy.Quiescent(bad),state);}
  foreach(string key in new[]{"adventure","dungeonKey"}){var bad=State();bad[key]="other";Reject(()=>EntryPreparationPolicy.Quiescent(bad),key);}
  s=State();s["syncWalkingCow"]=1;Reject(()=>EntryPreparationPolicy.Quiescent(s),"walking");s=State();s["movementState"]="NoMoreActions";Check(EntryPreparationPolicy.Ready(s,1,0,true),"Native NoMoreActions allowed");
  foreach(string key in new[]{"heroInstanceId","dungeonInstanceId","hexInstanceId","movementInstanceId","gameDefinitionIdentity"}){var bad=State();bad[key]=999;Reject(()=>EntryPreparationPolicy.Match(bad,State()),key);bad[key]="5";Reject(()=>EntryPreparationPolicy.Match(bad,State()),"not integer");}
  foreach(string key in new[]{"expectedQuestRegistered","expectedQuestDefinition","expectedQuestDestination"}){var bad=State();bad[key]=false;Check(!EntryPreparationPolicy.Ready(bad,1,0,true),key+" does not complete");}
  Check(!EntryPreparationPolicy.Ready(State(),0,0,true),"Unobserved callback");Check(!EntryPreparationPolicy.Ready(State(),2,0,true),"Double callback");Check(!EntryPreparationPolicy.Ready(State(),1,1,true),"Unconsumed continuation");Check(!EntryPreparationPolicy.Ready(State(),1,0,false),"Foreign continuation");
  s=State();s["atTargetHex"]=false;Reject(()=>EntryPreparationPolicy.Ready(s,1,0,true),"wrong hex");s=State();s.Remove("moving");Reject(()=>EntryPreparationPolicy.Quiescent(s),"missing field");
  var p=new EntryPreparationProgress();p.ClaimPosition();Reject(p.ClaimPosition,"duplicate position intent");p.phase="position-uncertain";Reject(p.ClaimDiscovery,"uncertain position cannot discover");
  p=new EntryPreparationProgress();p.ClaimPosition();p.PositionSubmitted();p.ClaimDiscovery();Reject(p.ClaimDiscovery,"duplicate discovery intent");p.phase="discover-uncertain";Reject(p.ClaimDiscovery,"uncertain discovery retry");
  p=new EntryPreparationProgress();p.ClaimPosition();p.PositionSubmitted();p.ClaimDiscovery();p.RecordCallback(10,2);p.DiscoverySubmitted();Check(p.callbacks==1&&p.phase=="discover-submitted","Synchronous callback before native call returns");
  p=new EntryPreparationProgress();p.ClaimPosition();p.PositionSubmitted();p.ClaimDiscovery();p.DiscoverySubmitted();p.RecordCallback(11,3);Check(p.callbacks==1&&p.callbackFrame==11,"Asynchronous callback after submission");p.RecordCallback(12,4);Check(!p.valid&&p.phase=="invalid-double-callback","Double callback permanently invalidates");p.RecordCallback(13,5);Check(p.ignoredCallbacks==1&&p.callbacks==2&&p.callbackFrame==12,"Invalidated callback harmless");
  Console.WriteLine("PASS "+tests+" actual linked entry policy/progress assertions (shipped Newtonsoft; no native Unity integration).");
 }
}
