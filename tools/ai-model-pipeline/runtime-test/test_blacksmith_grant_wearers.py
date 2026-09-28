"""Exercise exact grant class/item allowlists; retain grant-only opt-in boundary."""
from pathlib import Path
import re
import subprocess
import tempfile

source = (Path(__file__).parent / "BlacksmithGearFixture.cs").read_text()
methods = "\n".join(re.search(r"    static bool " + name + r"\(.*?\n    }", source, re.S).group()
                    for name in ("BlacksmithGrantClassAllowed", "ApprovedBlacksmithGrantId"))
grant = source.split("JObject BlacksmithGearGrant", 1)[1].split("JObject BlacksmithGearEquip", 1)[0]
equip = source.split("JObject BlacksmithGearEquip", 1)[1]
assert "allowNonBlacksmith" not in equip
assert "ExactBlacksmithHero(Int(command" in equip
for guard in ("RequireSinglePlayer();", "RequireOutsideCombat();", "JTokenType.Boolean",
              "ApprovedBlacksmithGrantId(stringId)", "BlacksmithAppearanceScalars(hero.m_CharacterStats)",
              "BlacksmithGrantPreservedEquals(expected, preservedAfter)"):
    assert guard in grant, guard
assert "return ExactBlacksmithGrantHero(heroInstanceId, false);" in source
with tempfile.TemporaryDirectory(prefix="ftk-wearer-grant-") as directory:
    root = Path(directory)
    (root / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    (root / "Program.cs").write_text("using System; class Program {\n" + methods + '''
    static int checks;
    static void Check(bool value) { if (!value) throw new Exception("Failed check " + checks); checks++; }
    static void Main() {
        Check(BlacksmithGrantClassAllowed("blacksmith", false));
        Check(BlacksmithGrantClassAllowed("blacksmith", true));
        foreach (string id in new[] {"hunter", "scholar"}) {
            Check(!BlacksmithGrantClassAllowed(id, false)); Check(BlacksmithGrantClassAllowed(id, true));
        }
        foreach (string id in new[] {null, "", "Hunter", "thief", "custom_blacksmith"}) {
            Check(!BlacksmithGrantClassAllowed(id, false)); Check(!BlacksmithGrantClassAllowed(id, true));
        }
        foreach (string tier in new[] {"coalmark", "bellowsworn", "rivetwatch", "kilnward"})
            foreach (string kind in new[] {"hammer_1h", "hammer_2h", "shield", "helmet", "trinket", "necklace", "armor", "boots"})
                Check(ApprovedBlacksmithGrantId("blacksmith_" + kind + "_" + tier));
        foreach (string id in new[] {null, "blacksmith_other_kilnward", "blacksmith_armor_other", "blacksmith_armor_Kilnward", "armorHeavy5", "blacksmith_hammer_1h_coalmark_extra"})
            Check(!ApprovedBlacksmithGrantId(id));
        Console.WriteLine(checks + " exact grant class and approved ID checks passed.");
    }
}''')
    subprocess.run(["dotnet", "run", "--project", str(root / "Test.csproj"), "-c", "Release"], check=True)
print("Grant-only opt-in, strict boolean, no-combat, and preservation boundaries passed.")
