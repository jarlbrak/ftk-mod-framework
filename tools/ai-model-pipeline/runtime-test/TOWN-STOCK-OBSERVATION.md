# Existing town stock observation

`town-stock-state` reads the current world's native town stock and party gold.
It lists every town's display name and map indices, then filters both generated
and current stock to item rows whose native `m_ID` starts with `paladin_`.
Each matching item includes its actual integer identity, stable key and count.
Stock availability flags distinguish a missing stock container from an empty
Paladin result. Item prices are omitted because base prices alone would not prove
the current buyer's actual purchase price.

```sh
python3 tools/ai-model-pipeline/runtime-test/command.py \
  --root "$PWD/scratch/paladin-game" town-stock-state
```

The command accepts no additional payload fields. The existing isolated helper
session checks apply. It does not generate or refresh stock, move a hero, purchase
an item, give gold, or write to native tables. It reads
`FTKHex.Instance.GetPOIList(MiniHexInfo.MiniHexType.Town)`,
`MiniHexTown.m_ShopItemStock`, and
`m_ShopItemStockCurrent.m_CountDictionary`. Hero gold is the existing
`CharacterStats.m_Gold` value. It does not establish that a reported town is
currently reachable or that its inventory has been opened by the player.

A stock snapshot establishes selection into that existing market only. Acceptance
of normal acquisition additionally requires a native purchase or drop observation
and corresponding inventory/gold changes. Empty results do not prove that item
registration failed: normal randomized stock may omit a particular item.

Build into a separate scratch output, without deploying to a running game:

```sh
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/paladin-game" \
  -p:TestManagedDir="$PWD/scratch/paladin-game/PaladinTest.app/Contents/Resources/Data/Managed" \
  -o "$PWD/scratch/paladin-town-observer-build"
python3 tools/ai-model-pipeline/runtime-test/test_town_stock_observation_readonly.py
```

Build and authority-boundary tests are offline evidence. Live stock observation
requires separate deployment and a running isolated world.
