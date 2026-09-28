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
source = (Path(__file__).parent / 'BlacksmithAppearanceFixture.cs').read_text()
method = re.search(r'    static bool BlacksmithPreservedEquals\(.*?\n    }', source, re.S).group()
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
        Check(BlacksmithPreservedEquals(before, after));
        Check(BlacksmithPreservedEquals(new JObject(), new JObject()));
        after["inventory"]["Backup"]["item"] = 1;
        Check(!BlacksmithPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["inventory"]["Bag"]["item"] = 3;
        Check(!BlacksmithPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["class"] = 2;
        Check(!BlacksmithPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["colors"][1] = 0.75;
        Check(!BlacksmithPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after["outfit"]["skin"] = 2;
        Check(!BlacksmithPreservedEquals(before, after));
        after = JObject.Parse(before.ToString()); after.Remove("outfit");
        Check(!BlacksmithPreservedEquals(before, after));
        Check(!BlacksmithPreservedEquals(JObject.Parse("{a:1,b:2}"), JObject.Parse("{b:2,a:1}")));
        Check(!BlacksmithPreservedEquals(JObject.Parse("{a:1}"), JObject.Parse("{a:'1'}")));
        Check(!BlacksmithPreservedEquals(null, new JObject()));
        Check(BlacksmithPreservedEquals(null, null));
        Console.WriteLine(checks + " preservation comparisons passed against " + typeof(JToken).Assembly.FullName);
    }
}
''')
    subprocess.run(['dotnet', 'run', '--project', str(root / 'Compat.csproj'), '-c', 'Release'], check=True)
