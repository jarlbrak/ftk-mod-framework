"""Run one-use native control token cases plus mutation/preservation boundary checks."""
from pathlib import Path
import subprocess
import tempfile

root=Path(__file__).parent
source=(root/'NativeWeaponControl.cs').read_text()
claim=source[source.index('internal sealed class NativeWeaponControlClaim'):source.index('public sealed partial class RuntimeModelTest')]
for required in ('RequirePackageFitIsolation();','ExactPackageFitHero(', 'PackageFitItems(', 'ExactClassRow(',
                 'FTK_itembase.ID.bluntWarHammer', 'FTK_itembase.ObjectSlot.twoHands',
                 'RequireBlacksmithInventoryPreserved(', 'PackageFitStableStats(', 'NativeControlPartySnapshot()',
                 'PackageFitFailureReceipt(error)', 'PackageFitBeginMutation(hero, "native-weapon-control")'):
    assert required in source,required
assert source.index('nativeWeaponControlClaim.Consume(') < source.index('PackageFitBeginMutation(hero,') < source.index('!backpack.Add(id)') < source.index('EquipOwnedBlacksmithItem(hero, entry,')
assert source.count('EquipOwnedBlacksmithItem(hero, entry,') == 1
assert 'native-weapon-control' in (root/'Plugin.cs').read_text()
for forbidden in ('SetValue(', '.RPC(', 'harmony.Patch', 'CreateNewShopInventory', 'Save(', 'Clear(', 'packageFitUncertain = false;\n            throw'):
    assert forbidden not in source,forbidden
program='''using System;
__CLAIM__
class Program {
 static int checks;
 static void Check(bool ok){checks++;if(!ok)throw new Exception("check "+checks);}
 static void Reject(Action action){try{action();}catch(InvalidOperationException){checks++;return;}throw new Exception("unsafe claim accepted");}
 static void Main(){
  var claim=new NativeWeaponControlClaim();
  Reject(()=>claim.Consume(null,true));Reject(()=>claim.Consume("missing",true));
  string first=claim.Inspect();Check(first.Length==32);
  Reject(()=>claim.Consume("wrong",true));Reject(()=>claim.Consume(first,false));
  claim.Consume(first,true);checks++;Reject(()=>claim.Consume(first,true));
  string second=claim.Inspect();Check(second!=first);Reject(()=>claim.Consume(first,true));
  claim.Consume(second,true);checks++;Reject(()=>claim.Consume(second,true));
  string third=claim.Inspect();string replacement=claim.Inspect();Reject(()=>claim.Consume(third,true));
  claim.Consume(replacement,true);checks++;Reject(()=>claim.Consume(replacement,true));
  Console.WriteLine(checks+" claim absence/staleness/consumption/reinspection checks passed");
 }
}'''.replace('__CLAIM__',claim)
with tempfile.TemporaryDirectory(prefix='native-weapon-control-') as temp:
    p=Path(temp)
    (p/'test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>')
    (p/'Program.cs').write_text(program)
    subprocess.run(['dotnet','run','--project',str(p/'test.csproj'),'-c','Release'],check=True)
print('Native control exact-row, preflight-before-mutation, shared uncertainty and preservation boundaries passed')
