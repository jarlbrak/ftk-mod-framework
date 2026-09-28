"""Exercise production multiplicity guards with a one-copy native swap stand-in."""
from pathlib import Path
import re
import subprocess
import tempfile

source = (Path(__file__).parent / 'BlacksmithGearFixture.cs').read_text()
names = ('OwnedAcrossEquipment', 'BlacksmithInventoryTotals', 'RequireBlacksmithInventoryPreserved',
         'PreflightOwnedBlacksmithItem', 'EquipOwnedBlacksmithItem')
methods = '\n'.join(re.search(r'    static [^\n]+ ' + name + r'\(.*?\n    }', source, re.S).group() for name in names)
assert 'if (owned > 1)' not in source
assert 'if (OwnedAcrossEquipment(hero, entry.itemId) != 0) continue;' in source
assert 'RequireBlacksmithInventoryPreserved(inventoryBefore, BlacksmithInventoryTotals(hero));' in source
assert 'Native two-handed ForceEquip left a shield in the off hand.' in source
with tempfile.TemporaryDirectory(prefix='ftk-multiplicity-') as directory:
    root = Path(directory)
    (root / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    (root / 'Program.cs').write_text('''using System;
using System.Collections.Generic;
class FTK_itembase { public enum ID { Shield, OtherShield, Hammer, Maul } }
class ItemContainer { public Dictionary<FTK_itembase.ID,int> m_ItemCounts = new Dictionary<FTK_itembase.ID,int>(); }
class PlayerInventory {
 public enum ContainerID { Backpack, LeftHand, RightHand, Body }
 Dictionary<ContainerID,ItemContainer> slots = new Dictionary<ContainerID,ItemContainer>();
 public ItemContainer Get(ContainerID slot) { if (!slots.ContainsKey(slot)) slots[slot] = new ItemContainer(); return slots[slot]; }
}
class CharacterOverworld {
 public PlayerInventory m_PlayerInventory = new PlayerInventory(); public int calls;
 public void ForceEquip(FTK_itembase.ID item, bool unused) {
  calls++; var bag=m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
  var slot=m_PlayerInventory.Get(item==FTK_itembase.ID.Shield || item==FTK_itembase.ID.OtherShield ? PlayerInventory.ContainerID.LeftHand : PlayerInventory.ContainerID.RightHand);
  ReturnToBag(slot,bag); bag.m_ItemCounts[item]--; slot.m_ItemCounts[item]=1;
  if(item==FTK_itembase.ID.Maul) ReturnToBag(m_PlayerInventory.Get(PlayerInventory.ContainerID.LeftHand),bag);
 }
 static void ReturnToBag(ItemContainer slot,ItemContainer bag) {
  foreach(var item in slot.m_ItemCounts) { int n;bag.m_ItemCounts.TryGetValue(item.Key,out n);bag.m_ItemCounts[item.Key]=n+item.Value; }
  slot.m_ItemCounts.Clear();
 }
}
class Program {
 class EquipRpcTrace {}
 static void ObserveEquipRpc(EquipRpcTrace trace,CharacterOverworld hero,BlacksmithGearEntry entry,string phase) { if(trace!=null) throw new Exception("Unexpected diagnostic"); }
 class BlacksmithGearEntry { public string stringId;public FTK_itembase.ID itemId;public PlayerInventory.ContainerID container;public int ownedCount; }
 static int ExactCount(ItemContainer c,FTK_itembase.ID id) { int n;return c!=null && c.m_ItemCounts.TryGetValue(id,out n)?n:0; }
''' + methods + '''
 static int checks;
 static void Check(bool condition) { if(!condition) throw new Exception("Check " + (checks+1));checks++; }
 static void Reject(Action action) { try {action();}catch(InvalidOperationException){checks++;return;}throw new Exception("Expected rejection"); }
 static BlacksmithGearEntry Entry(FTK_itembase.ID id,PlayerInventory.ContainerID slot) { return new BlacksmithGearEntry {itemId=id,stringId=id.ToString(),container=slot}; }
 static void Swap(CharacterOverworld hero, BlacksmithGearEntry entry) {
  PreflightOwnedBlacksmithItem(hero,entry);var before=BlacksmithInventoryTotals(hero);EquipOwnedBlacksmithItem(hero,entry);
  RequireBlacksmithInventoryPreserved(before,BlacksmithInventoryTotals(hero));checks++;
 }
 static void Main() {
  var hero=new CharacterOverworld();var bag=hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
  bag.m_ItemCounts[FTK_itembase.ID.Shield]=2;bag.m_ItemCounts[FTK_itembase.ID.OtherShield]=1;
  bag.m_ItemCounts[FTK_itembase.ID.Hammer]=1;bag.m_ItemCounts[FTK_itembase.ID.Maul]=1;
  var shield=Entry(FTK_itembase.ID.Shield,PlayerInventory.ContainerID.LeftHand);
  Swap(hero,shield);Check(hero.calls==1 && shield.ownedCount==2 && ExactCount(bag,FTK_itembase.ID.Shield)==1);
  Swap(hero,shield);Check(hero.calls==1); // One equipped and one Backpack copy is already correct.
  Swap(hero,Entry(FTK_itembase.ID.OtherShield,PlayerInventory.ContainerID.LeftHand));Check(ExactCount(bag,FTK_itembase.ID.Shield)==2);
  Swap(hero,shield);Check(ExactCount(bag,FTK_itembase.ID.OtherShield)==1);
  Swap(hero,Entry(FTK_itembase.ID.Maul,PlayerInventory.ContainerID.RightHand));
  Check(ExactCount(bag,FTK_itembase.ID.Shield)==2 && ExactCount(hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.LeftHand),FTK_itembase.ID.Shield)==0);
  Swap(hero,Entry(FTK_itembase.ID.Hammer,PlayerInventory.ContainerID.RightHand));Swap(hero,shield);
  Check(ExactCount(bag,FTK_itembase.ID.Shield)==1 && ExactCount(bag,FTK_itembase.ID.Maul)==1);
  var before=BlacksmithInventoryTotals(hero);bag.m_ItemCounts[FTK_itembase.ID.OtherShield]=0;
  Reject(()=>RequireBlacksmithInventoryPreserved(before,BlacksmithInventoryTotals(hero)));
  bag.m_ItemCounts[FTK_itembase.ID.OtherShield]=1;
  hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Body).m_ItemCounts[FTK_itembase.ID.Shield]=1;
  Reject(()=>PreflightOwnedBlacksmithItem(hero,shield));
  hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Body).m_ItemCounts.Clear();
  hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.LeftHand).m_ItemCounts[FTK_itembase.ID.Shield]=2;
  Reject(()=>PreflightOwnedBlacksmithItem(hero,shield));
  hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.LeftHand).m_ItemCounts[FTK_itembase.ID.Shield]=1;
  before=BlacksmithInventoryTotals(hero);hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Body).m_ItemCounts[FTK_itembase.ID.Shield]=0;
  RequireBlacksmithInventoryPreserved(before,BlacksmithInventoryTotals(hero));checks++;
  Console.WriteLine(checks+" multiplicity and all-item preservation checks passed.");
 }
}''')
    subprocess.run(['dotnet', 'run', '--project', str(root / 'Test.csproj'), '-c', 'Release'], check=True)
