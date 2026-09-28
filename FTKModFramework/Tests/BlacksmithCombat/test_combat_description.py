"""Check production combat text bounds; native layout remains a separate gate."""
from pathlib import Path
import re
import subprocess
import tempfile

core = Path(__file__).resolve().parents[2] / 'Core'
source = (core / 'BlacksmithRuntime.cs').read_text()
method = re.search(r'        internal static string CombatDescription\(.*?\n        }', source, re.S).group()
patch = (core / 'BlacksmithPatches.cs').read_text().split('internal static class BlacksmithActionInfoPatch')[1].split('[HarmonyPatch')[0]
assert 'BlacksmithRuntime.CombatDescription(kind, armor,' in patch
assert 'text +=' not in patch and 'BlacksmithRuntime.Status(actor)' not in patch
with tempfile.TemporaryDirectory(prefix='ftk-combat-text-') as directory:
    root = Path(directory)
    (root / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    (root / 'Program.cs').write_text('''using System;
enum BlacksmithActionKind {None, SetHammer, Overhand, Temper}
class Program {
''' + method + '''
 static void Main() {
  int checks=0;
  foreach(var kind in new[]{BlacksmithActionKind.SetHammer,BlacksmithActionKind.Overhand,BlacksmithActionKind.Temper})
   for(int armor=2;armor<=6;armor++) {
    string text=CombatDescription(kind,armor,true);string[] lines=text.Split('\\n');
    if(lines.Length != (kind==BlacksmithActionKind.Temper?2:1)) throw new Exception("Unexpected line count");
    foreach(string line in lines) if(line.Length>45) throw new Exception("Description exceeds compact bound");
    if(!text.Contains(armor.ToString()) || !text.Contains("Armor")) throw new Exception("Missing amount");
    checks++;
   }
  if(CombatDescription(BlacksmithActionKind.Temper,5,false)!="Temper used this combat.") throw new Exception("Disabled state");
  Console.WriteLine((checks+1)+" compact combat-description checks passed.");
 }
}''')
    subprocess.run(['dotnet', 'run', '--project', str(root / 'Test.csproj'), '-c', 'Release'], check=True)
