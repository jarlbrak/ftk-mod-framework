"""Run the production snapshot comparison against a supplied game Newtonsoft DLL."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--newtonsoft', required=True, type=Path)
args = parser.parse_args()
assembly = args.newtonsoft.resolve(strict=True)
source = (Path(__file__).parent / 'BlacksmithGearFixture.cs').read_text()
method = re.search(r'    static bool BlacksmithGrantPreservedEquals\(.*?\n    }', source, re.S).group()
with tempfile.TemporaryDirectory(prefix='ftk-preservation-') as directory:
    root = Path(directory)
    (root / 'Compat.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
<ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>''' + escape(str(assembly)) + '''</HintPath></Reference></ItemGroup>
</Project>''')
    (root / 'Program.cs').write_text('''using System;
using Newtonsoft.Json.Linq;
class Program {
''' + method + '''
    static int checks;
    static void Check(bool value) { if (!value) throw new Exception("Comparison check " + (checks + 1)); checks++; }
    static void Main() {
        JObject before = JObject.Parse("{class:1,inventory:{Main:{},Backup:{},Bag:{item:2}},outfit:{},colors:[1,0.5,0]}");
        JObject after = JObject.Parse(before.ToString());
        Check(BlacksmithGrantPreservedEquals(before, after));
        Check(BlacksmithGrantPreservedEquals(new JObject(), new JObject()));
        after["inventory"]["Backup"]["item"] = 1;
        Check(!BlacksmithGrantPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["inventory"]["Bag"]["item"] = 3;
        Check(!BlacksmithGrantPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["class"] = 2;
        Check(!BlacksmithGrantPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["colors"][1] = 0.75;
        Check(!BlacksmithGrantPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["outfit"]["skin"] = 2;
        Check(!BlacksmithGrantPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after.Remove("outfit");
        Check(!BlacksmithGrantPreservedEquals(before, after));
        Check(BlacksmithGrantPreservedEquals(JObject.Parse("{a:1,b:2}"), JObject.Parse("{b:2,a:1}")));
        Check(BlacksmithGrantPreservedEquals(JObject.Parse("{inventory:{Backpack:{old:1,bow:1,newHammer:1,newKit:1}}}"), JObject.Parse("{inventory:{Backpack:{old:1,newHammer:1,newKit:1,bow:1}}}")));
        Check(!BlacksmithGrantPreservedEquals(JObject.Parse("{a:[1,2]}"), JObject.Parse("{a:[2,1]}")));
        Check(!BlacksmithGrantPreservedEquals(JObject.Parse("{a:null}"), new JObject()));
        Check(!BlacksmithGrantPreservedEquals(new JObject(), JObject.Parse("{a:null}")));
        Check(!BlacksmithGrantPreservedEquals(JObject.Parse("{a:1}"), JObject.Parse("{a:1.0}")));
        Check(!BlacksmithGrantPreservedEquals(JObject.Parse("{a:1}"), JObject.Parse("{a:'1'}")));
        Check(!BlacksmithGrantPreservedEquals(null, new JObject()));
        Check(BlacksmithGrantPreservedEquals(null, null));
        Console.WriteLine(checks + " preservation comparisons passed against " + typeof(JToken).Assembly.FullName);
    }
}
''')
    subprocess.run(['dotnet', 'run', '--project', str(root / 'Compat.csproj'), '-c', 'Release'], check=True)
