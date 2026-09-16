# Candidate topology coverage planning snapshot

{"explicitPlayerAvatarGroups": 3, "explicitResourcePrefabGroups": 4, "explicitUnsupportedGroups": 1, "groupsWithAnyIndexedDirectEnemyEvidence": 43, "groupsWithAnyIndexedOriginalDirectEnemyEvidence": 43, "groupsWithAnyIndexedOriginalResourcePrefabEvidence": 5, "groupsWithAnyIndexedPlayerEvidence": 3, "groupsWithAnyIndexedResourcePrefabEvidence": 5, "groupsWithDirectNativeEnemyRows": 43, "groupsWithExplicitOwnershipClassification": 6, "groupsWithResourcePrefabRows": 5, "topologyGroups": 49, "unresolvedEvidenceRecords": 0, "unresolvedOwnershipGroups": 0}

**Evidence presence, not PASS counts.** Direct enemy rows, resource-prefab overrides, player avatars, and unsupported empty renderers have separate routes. `Original` means an indexed authored-model record, not a full live verdict. A zero source pair is not complete coverage.

| Topology | Joints | Examples | Direct / resource pairs | Original / any / known pairs | Route or remaining work |
|---|---:|---|---:|---:|---|
|82d606d9b5a0d2e1|32|enAcidMonster|2 / 0|2/2/2|0 unrecorded exact source pair(s)|
|0db0dbdf202b8e11|7|enClam|3 / 0|1/2/3|1 unrecorded exact source pair(s)|
|db2a524a5bc700ea|37|BanditWarrior, Basey, Yeti|235 / 5|4/143/240|97 unrecorded exact source pair(s)|
|f50b09e31a8484cf|36|enScourgeLeprechaun|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|896faa557db56261|53|enBat01, enJungleBat|4 / 0|1/2/4|2 unrecorded exact source pair(s)|
|07f911c982da0402|38|enBear01, enBear02, enBear03|3 / 0|1/3/3|0 unrecorded exact source pair(s)|
|6fdb7ff148451731|47|Monster Bee, enMosquito_01, enMosquito_02|8 / 0|1/6/8|2 unrecorded exact source pair(s)|
|ba17c1398db51453|39|enCrow, enJungleBird_A, enJungleBird_B|12 / 0|1/6/12|6 unrecorded exact source pair(s)|
|b4ccd4e96e3a224b|20|ChaosSkullBottom|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|8f027145e71c9525|22|ChaosSkullTop|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|678c8066b33cf823|50|enBaseyCockatrice, enChicken, enCockatrice|4 / 2|3/5/6|1 unrecorded exact source pair(s)|
|791b63f3064c2f62|63|CrabGeo, enCrabWizard|3 / 0|1/2/3|1 unrecorded exact source pair(s)|
|c01698c74bd54910|70|enDragon|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|c3683bc2e807b15a|66|enFairy01|4 / 0|1/1/4|3 unrecorded exact source pair(s)|
|5990c51ca004ebdb|34|, enFishA, enFishB|14 / 0|1/10/14|4 unrecorded exact source pair(s)|
|73739eaf6fd8f0e4|2|EyeBody, EyeEye, enWisp|4 / 0|2/3/4|1 unrecorded exact source pair(s)|
|d1de8c46112a77d8|31|enChaosBeast|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|6d40f2e6eba19a6b|3|enIceCube, enJellyCube|7 / 0|1/1/7|6 unrecorded exact source pair(s)|
|6a28ac3cf4523c24|5|krakenHead|0 / 1|1/1/1|Resources enkrakenhead is a five-bone enKrakenHead prefab route, distinct from the seven-bone direct krakenHead enemy row. (0 unrecorded resource source pair(s))|
|e45711bff451ca73|7|kraken2|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|aae2ba644a16c223|20|KrakenGodTentacle, krakenTentacle|4 / 0|4/4/4|0 unrecorded exact source pair(s)|
|cace272650590c4f|9|mimic01|3 / 0|1/1/3|2 unrecorded exact source pair(s)|
|81f02cdbf3eefcb9|42|enMonkeyA, enMonkeyBasey, enMonkeyDiseased|4 / 0|1/3/4|1 unrecorded exact source pair(s)|
|730458bde8737934|16|enPlant01Leaves|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|1329d6985dadecee|37|enJungleNibbler_A|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|b0b93182a11758ed|43|enJungleNibbler_B, enJungleNibbler_C|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|e879df15fd3c4d59|45|enJungleNibbler_D, enPlant01, enPlant02|4 / 0|1/2/4|2 unrecorded exact source pair(s)|
|4082b6c4e777f922|6|hairBottom, hairTop|0 / 0|0/0/0|Forty source placements belong to FTK_skinset avatar prefabs; two Player_Monk hair renderers are a ResourceManager-only player_monk hierarchy without a skinset route.|
|45c7a9b9fb730195|7|hairBottom|0 / 0|0/0/0|Two seven-bone hair renderers are native Player_Herbalist and Player_FishPerson skinset-avatar members, never enemy rows. Wildbloom Herbalist now has a canonical native player route; Tideglass Fishsmith remains a separate exact Party Select preview route. Evidence does not transfer between them.|
|85c742f628ea6d37|24|playerBlacksmith, playerFrostMonk, playerFrostMonkF|0 / 0|0/0/0|Seventeen source placements belong to FTK_skinset avatar prefabs; Player_Monk body 120989 is a ResourceManager-only player_monk hierarchy without a skinset route.|
|94dbc18f21284ec2|34||0 / 0|0/0/0|Resources player_mayor has a 34-bone renderer reference but no mesh, bind pose, material, native skinset, or current strict replacement path.|
|853432a7c217ff04|36|deathKnight|7 / 0|1/2/7|5 unrecorded exact source pair(s)|
|fb84ec3e6e18f459|8|hairBottom, hairBottomBossGladiator, hairTop|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|73ef97795cc54f57|25|enBossGladiator, playerAstronomerF, playerAstronomerM|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|b65252b48fd9609d|11|bootsBossGladiator|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|11742c19aa67e6d0|26|armorBossGladiator|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|2185298a0a369e67|30|enBlowFishA|2 / 0|2/2/2|0 unrecorded exact source pair(s)|
|0f29e98795c302a5|36|enRoc01|3 / 0|1/1/3|2 unrecorded exact source pair(s)|
|1eda40629ee9aac9|60|enSeaKing|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|7b140c07befa33fd|44|enJungleSnakeC|1 / 0|1/1/1|0 unrecorded exact source pair(s)|
|7313dc39dab041dd|44|enSnake_Basey|0 / 1|1/1/1|Resources enbaseysnake is a 44-bone enBaseySnake override, distinct from the 46-bone direct snakeJungleA enemy prefab. (0 unrecorded resource source pair(s))|
|80d61d495b6a93be|46|enBananaSnake, enDesertSnakeA, enDesertSnakeB|4 / 0|1/1/4|3 unrecorded exact source pair(s)|
|963e53f60643b796|8|enSnowmanHat|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|02a6f31412bd52ab|11|enSnowmanHead|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|1322fec3354db550|13|enSnowmanScarf|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|8c73c366b064726a|16|enSnowmanBase|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|a158ab62f9dd430f|31|enSnowmanmiddleBody|2 / 0|1/2/2|0 unrecorded exact source pair(s)|
|23c62612fd16b533|65|enJugnleSpider_B, enJungleSpider_A, enSpiderA|5 / 0|1/3/5|2 unrecorded exact source pair(s)|
|c3487d422b832d2e|33|Wolfie, chaosWolf, enArmoredWolf|12 / 4|2/7/16|9 unrecorded exact source pair(s)|

## First unrecorded exact route per incomplete topology

Each row is a deterministic starting point for the next authoring and live-trial package. It does not make sibling routes covered; the final column states how many further exact routes remain in that topology.

| Topology | Native route | Renderer / source | Controller | Further exact routes |
|---|---|---|---|---:|
|0db0dbdf202b8e11|`clamB`|`enClam` / 121306|`ClamController` / 5932|0|
|db2a524a5bc700ea|`aztecBossEasy`|`enJungleElder` / 121503|`player_2H_Magic_Combat` / 5993|96|
|896faa557db56261|`batB`|`enBat01` / 121105|`batController` / 5940|1|
|6fdb7ff148451731|`beeB`|`Monster Bee` / 121226|`beeController` / 5942|1|
|ba17c1398db51453|`birdA`|`enCrow` / 121179|`birdController` / 5943|5|
|678c8066b33cf823|`cockatriceB`|`enChicken` / 121540|`chickenController` / 5948|0|
|791b63f3064c2f62|`crabC`|`CrabGeo` / 121635|`crabController` / 5950|0|
|c3683bc2e807b15a|`fairyB`|`enFairy01` / 121396|`fairyController` / 5953|2|
|5990c51ca004ebdb|`fishBossA`|`enFishE` / 121685|`fishStaffController` / 5957|3|
|73739eaf6fd8f0e4|`wispB`|`enWisp` / 121121|`floatingEyeController` / 5959|0|
|6d40f2e6eba19a6b|`cubeB`|`enJellyCube` / 121013|`jellyCubeController` / 5972|5|
|cace272650590c4f|`mimicB`|`mimic01` / 121192|`mimicController` / 5978|1|
|81f02cdbf3eefcb9|`monkeyB`|`enMonkeyBasey` / 121301|`monkeyController` / 5979|0|
|e879df15fd3c4d59|`plantB`|`enPlant02` / 121243|`plantController` / 5980|1|
|853432a7c217ff04|`deathknightB`|`deathKnight` / 121218|`player_1H_Blunt_Combat` / 5983|4|
|0f29e98795c302a5|`rocB`|`enRoc01` / 121239|`rocController` / 6000|1|
|80d61d495b6a93be|`snakeDesertB`|`enDesertSnakeB` / 121447|`snakeController` / 6002|2|
|23c62612fd16b533|`spiderC`|`enSpiderC` / 121604|`spiderController` / 6004|1|
|c3487d422b832d2e|`chaosHoundEasy`|`chaosWolf` / 121191|`wolfController` / 6007|8|

Inspect every exact record before planning a new capture. A source-pair evidence count includes failures and partial trials; it does not approve a topology, related controller, or sibling renderer.
