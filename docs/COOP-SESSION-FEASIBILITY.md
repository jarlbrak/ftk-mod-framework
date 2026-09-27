# Co-op session feasibility

Discovery for [Spec B0, #234](https://github.com/jarlbrak/ftk-mod-framework/issues/234)
under [epic #232](https://github.com/jarlbrak/ftk-mod-framework/issues/232).
This is a design and evidence record. It ships no runtime behavior, and no
framework patch described here exists yet.

## Evidence boundary

| Item | Value |
| --- | --- |
| Phase | Phase 1, assembly research. Phase 2 live checks have not run. |
| Repository baseline | `5d830c6d` |
| Audit date | 2026-09-27 |
| Steam app / build | 527230, build id `12395049` (local `appmanifest_527230.acf`) |
| Platform audited | macOS, 64-bit, Steam |
| `Assembly-CSharp.dll` SHA-256 | `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8` |
| `Photon3Unity3D.dll` SHA-256 | `d5a015b6e5af45233263aefcecdf8e8184822c813364a8dff435b583a2ffb610` (assembly version 4.1.1.14) |
| PUN | `PhotonNetwork.versionPUN` is `"1.85"`, compiled into `Assembly-CSharp` |
| Version string | Local `Player.log` reports `Release v1.1.00.11378 steam` for `FTKVersion.GetVersionFull` |
| HarmonyX | Reference package 2.7.0 (`FTKModFramework.csproj` via `BepInEx.Core` 5.*) |

Read-only inspection used `ilspycmd` against the installed bytes above. No game
binary or decompiled source is included here. Findings describe control flow
for those bytes. PlayMaker FSM wiring, serialized scene values and Photon
server behavior are not in the assembly and are labelled `inferred` unless
stated otherwise.

Evidence labels:

- `verified (assembly)`: read directly from the installed game assembly, the
  Photon client assembly, the HarmonyX reference package, or framework source.
- `verified (live)`: observed in a running game. No claim in this phase has
  this label.
- `inferred`: follows from assembly evidence plus an assumption about scene
  data, server behavior or documentation. Each one maps to a live gate below.

## FR-1: Version gate and room partitioning

| Claim | Evidence | Method |
| --- | --- | --- |
| Online co-op rooms publish `ver` = `FTKVersion.GetVersion()`, which returns the serialized `m_VersionNum` (`1.1.00` on this build per `Player.log`). | verified (assembly) | `StartGameFE.GameConfig.CreateOnlineRoom(bool, string, bool, string, DifficultyType, TimeOfDay)`, `FTKVersion.GetVersion` |
| `Lobby.CategorizePhotonRooms` skips any room without `ver` (logs a warning, never lists it). With `ver`, it uses ordinal string equality against `GetVersion()` only to sort rooms into compatible-open, compatible-closed, incompatible-open and incompatible-closed buckets. | verified (assembly) | `StartGameFE.Lobby.CategorizePhotonRooms` (iterator) |
| `RoomUI.Set` uses the same equality to set the row's `m_JoinButton.interactable` for open rooms and to colour the displayed version text. Its unguarded `CustomProperties["ver"]` read is only reached for rooms that passed the `ContainsKey` filter above. | verified (assembly) | `RoomUI.Set` |
| The browser version gate is leaky. `RoomUI.Select` enables `Lobby.m_JoinGame` for any open room without a version check, `Lobby.RenderRoomList` calls `Select` on the first open room it renders, and `Lobby.JoinGame` / `JoinGameActual` perform no version check. An incompatible open room is auto-selected when no compatible open room sorts ahead of it. | verified (assembly) | `RoomUI.Select`, `Lobby.RenderRoomList`, `Lobby.JoinGame`, `Lobby.JoinGameActual` |
| Whether the player can activate the main Join button in that state depends on scene wiring. | inferred | Live gate L3 |
| Open rooms are also hidden from release clients unless `dev` is present and `false`. | verified (assembly) | `Lobby.RenderRoomList` |
| Steam invites compare a separate string: the third comma field of the `joininfo` lobby data against `GetVersionFull()` by equality. `GetVersionFull()` is `"{m_BuildName} {32bit }v{m_VersionNum}.{m_SilentID} {BuildType}{PublishPlatform.PlatformName}"`; `PlatformName` is the constant `"steam"`. | verified (assembly) | `uiStartGame.OnLobbyEnter`, `FTKVersion.GetVersionFull`, `PublishPlatform.PlatformName` |
| Saves record `GameInfo.m_Version = GetVersion()`, and `uiStartGame.SupportSaveGameVersion` returns `true` unconditionally. Changing `GetVersion()` itself would therefore alter save headers, so any `ver` change must be made at the room-property write, not in `FTKVersion`. | verified (assembly) | `GameSerialize.Save`, `uiStartGame.SupportSaveGameVersion` |
| Photon `AppVersion` is `"{PhotonNetwork.gameVersion}_1.85"`. `gameVersion` comes from `uiStartGame.m_GameVersion`, a serialized field whose code default is `"1.0"`; the scene value is not in the assembly. `SetApp` trims it. | verified (assembly) | `NetworkingPeer.AppVersion`, `NetworkingPeer.SetApp`, `uiStartGame.AuthTicketCallback`, `uiStartGame.FindBestServer` |
| Photon Cloud separates matchmaking and room lists by AppId plus AppVersion, so option (c) would make framework rooms invisible to vanilla clients and unjoinable by name. | inferred | Photon documentation, not versioned for 1.85 |
| Option (a), a modified `ver`: vanilla clients see the room listed after compatible rooms, with a red version string and a disabled row button; the leaky selection above still applies. | verified (assembly) | Combination of the rows above |
| Option (b), framework lobby key only: vanilla clients see the room as compatible and joinable, so every vanilla join must be refused host-side. | verified (assembly) | `ver` unchanged, equality passes |
| Option (c), `AppVersion` partition: vanilla clients never see framework rooms. Mismatched framework players would also be invisible to each other if the digest were in `AppVersion`, which defeats the epic's "told exactly why" requirement. | inferred | Photon behavior plus the epic requirement |

**Recommendation.** Use (a) with a constant marker plus (b) for the digest:
publish `ver` as `GetVersion() + "+ftkmf"` and put the mod-set digest in a
separate framework lobby key. Vanilla players see framework rooms as a visibly
different version and cannot use the row button. Framework clients evaluate the
digest and can explain a mismatch. Vanilla players lose the ability to join
framework rooms at all, which the epic requires. Because the vanilla gate is
leaky, host-side refusal (FR-3) is still required. Reject (c): it hides the
mismatch reason and depends on a scene value that the assembly does not show.
Keep `ver` present in every framework room.

## FR-2: Refusal points on both join paths

There are exactly two managed `PhotonNetwork.JoinRoom` callers:
`Lobby.JoinGameActual` and `uiStartGame._OnJoinedLobby`. Discord join callbacks
in `DiscordController` are empty.

| Claim | Evidence | Method |
| --- | --- | --- |
| Browser chain: `Lobby.JoinGame` (password prompt through `uiSystemDialog.ShowInput` when `pw` exists, compared client-side in `PasswordEntered`) then `JoinGameActual` sets `GameMode.Multiplayer`, calls `PhotonNetwork.JoinRoom(name)`, reads `diff2` unguarded and leaves the lobby. | verified (assembly) | `StartGameFE.Lobby` |
| Invite chain: host `WaitingRoom.Show` creates a Steam lobby; `WaitingRoom.OnLobbyCreated` sets `joininfo` = `FTKUtil.EncodeJoinSecret(room.Name, region, GetVersionFull())`. Invitee `uiStartGame.GameLobbyJoinRequested` (only when `!m_GameStarted`) calls `SteamMatchmaking.JoinLobby`; `OnLobbyEnter` (only when `!IsMasterClient`) decodes, compares the version, and on a match sets `gRestartJoinGame`, `gJoinDirectRoomName`, `gJoinDirectRegion` and calls `GameLogic.RestartGame`. On a mismatch it does nothing and shows nothing. | verified (assembly) | `StartGameFE.WaitingRoom`, `uiStartGame.GameLobbyJoinRequested`, `uiStartGame.OnLobbyEnter` |
| After the reload, `uiStartGame.Update` on its first frame sees `gRestartJoinGame` and calls `JoinGameDirect`, which sets `m_JoiningOnlineGame` and `m_JoiningDirect` and calls `ConnectToServer`. `_OnJoinedLobby` then calls `PhotonNetwork.JoinRoom(gJoinDirectRoomName)`. | verified (assembly) | `uiStartGame.Update`, `JoinGameDirect`, `_OnJoinedLobby` |
| `_OnJoinedLobby` runs from the `"PHOTON / JOINED LOBBY"` FSM event raised by `OnJoinedLobby`, and `_OnJoinedRoom` from `"PHOTON / JOINED ROOM"` raised by `OnJoinedRoom`. The FSM transitions themselves are scene data. | inferred | `uiStartGame.OnJoinedLobby`, `uiStartGame.OnJoinedRoom`; live gate L1 |
| Cold start: `uiStartGame.Start` reads `+connect_lobby <id>` (only when `!gRestartJoinGame`) and calls `SteamMatchmaking.JoinLobby`; the `LobbyEnter_t` callback reaches the same `OnLobbyEnter`. A gate in `OnLobbyEnter` therefore covers warm and cold invites. | verified (assembly) | `uiStartGame.Start`, `uiStartGame.OnLobbyEnter` |
| Whether the framework's mod-set identity is ready when a cold-start `OnLobbyEnter` fires. | inferred | Live gate L5 |
| The join secret can carry a digest only inside the third field or in a separate Steam lobby key. `DecodeJoinSecret` requires exactly three comma-separated fields and `Enum.Parse`s the second, so a fourth field fails decoding. Appending a comma-free marker to the third field makes vanilla invitees fail the equality and silently ignore the invite. `SteamMatchmaking.SetLobbyData` / `GetLobbyData` accept additional keys. | verified (assembly) | `FTKUtil.DecodeJoinSecret`, `FTKUtil.EncodeJoinSecret`, `WaitingRoom.OnLobbyCreated`, `uiStartGame.OnLobbyEnter` |
| Post-join gate point: `uiStartGame._OnJoinedRoom`, in `GameMode.Multiplayer`, adopts host DLC rights from `hostdlc`, records the master client, and sends `"ShowWaitingRoom"`. Room properties are readable at this point, because vanilla reads `hostdlc` here. The earlier `uiStartGame.OnJoinedRoom` PUN callback is the managed entry that raises the FSM event. | verified (assembly) | `uiStartGame._OnJoinedRoom`, `uiStartGame.OnJoinedRoom` |
| The `hostdlc` code is not a refusal precedent. It adopts host DLC rights and swallows parse errors; no path in this build refuses a join on DLC. | verified (assembly) | `uiStartGame._OnJoinedRoom`, `GameCache.Cache.DLCRights.SetHostDLCRights`; `hostdlc` has no other reader |
| Refusal UI precedents: `ShowPlayerDisconnectDialog` and `OnCustomAuthenticationFailed` call `uiSystemDialog.Instance.Show(...)` with `ContinueFSM(CharacterBackToMain)`. `CharacterBackToMain` calls `GameLogic.RestartGame`, whose `RestartFadeOutFinish` leaves the host Steam lobby, calls `PhotonNetwork.Disconnect()` and reloads `FTK_main`. `OnPhotonJoinRoomFailed` shows localized errors including `STR_errorInProgress` and `STR_errorLobbyFull` on the lobby screen. | verified (assembly) | `uiStartGame.ShowPlayerDisconnectDialog`, `OnCustomAuthenticationFailed`, `OnPhotonJoinRoomFailed`, `GameLogic.RestartFadeOutFinish` |
| `WaitingRoom.Show` on a client reads `gamedef` and `diff2` unguarded and calls `GetPreview(gamedef).GetNewGameDefInstance()`, which throws on an unknown adventure. The post-join gate must run before `"ShowWaitingRoom"`. | verified (assembly) | `StartGameFE.WaitingRoom.Show` |
| After `PhotonNetwork.LeaveRoom()`, `uiStartGame.OnLeftRoom` only clears `m_ThisIsClient`; no managed code changes screens. If `PhotonServerSettings.JoinLobby` (auto-join) is on, a later `OnJoinedLobby` could re-enter `_OnJoinedLobby` with `m_JoiningDirect` still set and rejoin the same room. Refusal should therefore disconnect and restart rather than only leave the room. | inferred | `uiStartGame.OnLeftRoom`, `PhotonNetwork.autoJoinLobby`, `_OnJoinedLobby`; live gate L2 |
| Host view of a joiner: `uiStartGame.OnPhotonPlayerConnected` appends a `PotentialPlayers` entry (`m_Include` while under `m_ActualMaxCharCount`) and broadcasts `RefreshPlayerList`. When that player leaves before `m_GameStarted`, the host removes the entry and refreshes only if `uiScreen.gCurrent == m_WaitRoom`; otherwise the host gets `ShowPlayerDisconnectDialog` and returns to the title. | verified (assembly) | `uiStartGame.OnPhotonPlayerConnected`, `uiStartGame.OnPhotonPlayerDisconnected` |
| The host is normally on the waiting room while its room is open, so a refused joiner leaves no ghost slot. | inferred | Live gate L2 |

## FR-3: Host refusing a vanilla client

| Claim | Evidence | Method |
| --- | --- | --- |
| `PhotonNetwork.CloseConnection(PhotonPlayer)` works only for the master client: it raises event 203 targeted at that actor. The target's own PUN code calls `PhotonNetwork.LeaveRoom()` if the sender is the master, so the kick is cooperative. Vanilla clients run the same PUN code. | verified (assembly) | `PhotonNetwork.CloseConnection`, `NetworkingPeer` event 203 handler |
| Vanilla already uses this path. `uiStartGame.TrimPotentialPlayers`, called from `WaitingRoom.OnCharacterSelect`, removes unchecked players from `m_PotentialPlayers` and calls `CloseConnection` for each, then counts their disconnects through `m_WaitRemovalConfirmCount`. | verified (assembly) | `uiStartGame.TrimPotentialPlayers`, `WaitingRoom.OnCharacterSelect`, `uiStartGame.OnPhotonPlayerDisconnected` |
| Player properties set before joining reach the host before its player-connected callback. `PhotonNetwork.SetPlayerCustomProperties` caches them locally when not in a room; the game-server join sends `PhotonNetwork.player.AllProperties` as parameter 249; the host's join-event handler constructs the `PhotonPlayer` with those properties and only then dispatches `OnPhotonPlayerConnected`. | verified (assembly) | `PhotonNetwork.SetPlayerCustomProperties`, `NetworkingPeer.GetLocalActorProperties`, `LoadBalancingPeer.OpJoinRoom`, `NetworkingPeer` event 255 handler |
| A host postfix on `uiStartGame.OnPhotonPlayerConnected` can detect a missing framework player property and call `CloseConnection`. Leaving the entry in `m_PotentialPlayers` lets the vanilla waiting-room branch remove it on disconnect. | verified (assembly) | Rows above |
| The vanilla client's experience: it receives no explanation, leaves the room, and its UI stays wherever the FSM left it (the waiting room if `_OnJoinedRoom` already ran). It can use `WaitingRoom.Back`. A browser join may return to the lobby; an invite join may loop if auto-join lobby is on. | inferred | Live gate L6 |
| A closed or hidden room is not a per-player refusal: it blocks everyone. An in-game message cannot reach a vanilla client through framework code. | verified (assembly) | `Room.IsOpen` setter sends `OpSetPropertiesOfRoom` for all clients |

## FR-4: Framework client joining a vanilla host

| Claim | Evidence | Method |
| --- | --- | --- |
| Detection needs no network call: a vanilla room has `ver == GetVersion()` exactly and no framework keys in the lobby list; after joining, no framework room keys; a vanilla invite has a `joininfo` version field equal to `GetVersionFull()`. | verified (assembly) | `Lobby.CategorizePhotonRooms`, `RoomUI.Set`, `OnLobbyEnter` inputs |
| Outcome 1, refuse: the framework client greys the room and refuses at the post-join gate with a reason. No desync risk; framework users cannot play with vanilla friends. | inferred | Design consequence |
| Outcome 2, allow only when the local mod set is empty (no enabled content packages or behavior DLLs) with no session tweaks: low desync risk, limited to framework patches that still alter shared behavior on a client. | inferred | Design consequence |
| Outcome 3, allow with any mod set: high risk. Registered classes, items and adventures produce identifiers the vanilla host cannot resolve, and Session tweaks would diverge from the host. | inferred | Design consequence; see deterministic-ID rules in `AGENTS.md` |

The choice among these remains epic #232 open question 2.

## FR-5: Save record in the `GameFlow` state

| Claim | Evidence | Method |
| --- | --- | --- |
| `GameFlow` does not override `StateDataSerialize` or `StateDataDeserialize`. No type in the assembly overrides them. A save-record patch must target `FTKNetworkObject.StateDataSerialize(bool)` and `FTKNetworkObject.StateDataDeserialize(string, bool)` and filter on `__instance is GameFlow` first, because these methods run for every serialized network object. | verified (assembly) | `FTKNetworkObject`, `GameFlow : FTKNetworkNonMC` |
| The serializer builds a fresh `Dictionary<string, object>` from the `[FTKSerialize]` fields and writes it with FullSerializer. The deserializer reads only keys named by `m_SerializeFields`; unknown keys are ignored, and a missing known key only logs `New serialize field found`. Vanilla therefore ignores a framework key on load and drops it on its next save. | verified (assembly) | `FTKNetworkObject.StateDataSerialize`, `StateDataDeserialize` |
| A key containing a dot cannot collide with a C# field name. | verified (assembly) | Keys are `FieldInfo.Name` values |
| Manual save (`SaveAndQuit`) and autosave (`AutoSave`) both reach `GameLogic.Save(bool, bool)`, then `SaveGame`, `ReceivePlayerSerializeData`, `GameSerialize.Save`, `GameStatesSerialize.Serialize`, which stores `GameFlow.Instance.StateDataSerialize()` in `m_GameFlowStates`. | verified (assembly) | `GameLogic`, `GameSerialize.Save`, `GameStatesSerialize.Serialize` |
| Load: `GameSerialize.Load(string, ...)` round-trips `m_GameFlowStates` through a `Dictionary<string, object>` for old-format saves, which preserves unknown keys, then `GameStatesSerialize.Deserialize` calls `GameFlow.Instance.StateDataDeserialize`. `uiStartGame.MigrateSaveFiles2` re-serializes the whole `GameSerialize` and keeps the string. | verified (assembly) | `GameSerialize.Load`, `GameStatesSerialize.Deserialize`, `uiStartGame.MigrateSaveFiles2` |
| Client sync on resume: `GameLogic.DeserializeFinal` sends the saved `m_GameFlowStates` string verbatim in `ClientDeserializeFinalRPC`; non-master clients call `GameStatesSerialize.Deserialize`, so the key reaches every client of a resumed run. | verified (assembly) | `GameLogic.DeserializeFinal`, `GameLogic.ClientDeserializeFinalRPC` |
| `GameLogic.JoinGame` requests `SendGameData` on the `GameLogic` view only; it never carries `GameFlow` state, and it has no managed caller. It is not a carrier for this record. New runs publish the session set through room properties instead. | verified (assembly) | `GameLogic.JoinGame`, `FTKNetworkObject.SendGameData` |
| `SaveNamespace` only rewrites save directories and the `LastSave` key (transpilers, `GetSaveFileFullName` postfix, scan and load guards); it does not touch state content, so the record survives namespaced saves. | verified (assembly) | `Core/HotReload/SaveNamespace.cs` |
| The resuming host can read the record before publishing room properties. `uiStartGame.OnResumeGame` sets `m_LoadFileName` and `m_ResumeGameInfo` before `OnResumeGame2` reaches `GameConfig.CreateGame(true)` or `ConnectToDefaultServer`, and `GameSerialize.GetGameSerialize(path)` exposes `m_GameStates.m_GameFlowStates` without loading the run. | verified (assembly) | `uiStartGame.OnResumeGame`, `OnResumeGame2`, `GameConfig.CreateGame`, `GameSerialize.GetGameSerialize` |
| FullSerializer round-trips a `string` value inside `Dictionary<string, object>` as a string. | inferred | Game-free test, live gate L9 |
| Vanilla, with the framework disabled, loads a save containing the key. | inferred | Live gate L8 |
| No path filters the key, so the sidecar-file fallback is not needed. | verified (assembly) | Rows above |

The framework's existing campaign store (`CampaignStateQuest`) travels in the
quest table as a framework type, which vanilla cannot deserialize. The
`GameFlow` key is the vanilla-safe carrier.

## FR-6: Poison counter authority

| Claim | Evidence | Method |
| --- | --- | --- |
| `CharacterStats.m_PoisonTimeCounter` is a private `int` without `[FTKSerialize]` or a sync attribute; `m_PoisonLvl` is `[FTKSerialize]`. `PoisonTimeRounds` is 3. | verified (assembly) | `CharacterStats` fields |
| The counter increments in `CharacterStats.EndTurnActionSequence` when `m_HealthCurrent > 0`, the character is not waiting to respawn, and `m_PoisonLvl > 0`. It resets to 0 there when not poisoned. At the end of the sequence, a counter at or above 3 calls `RPCAllSelf("UpdatePoison", -1, false)` and resets. | verified (assembly) | `CharacterStats.EndTurnActionSequence`, `UpdatePoison` |
| In a dungeon, `CheckEndTurnAction` sends `DoRemoteEndTurnAction` through `RPCOwner` to every living character inside, so each counter advances on its owner's client. | verified (assembly) | `CharacterStats.CheckEndTurnAction`, `NextDungeonCowAction`, `CharacterOverworld.DoRemoteEndTurnAction` |
| In the overworld, `CheckEndTurnAction` starts the coroutine locally; its caller is the FSM and is assumed to run on the owner. | inferred | Live gate L15 |
| Saves serialize every character from the host's local objects (`GameLogic.SaveGame` calls `GetPlayerSerializeData` locally), so the host's copy of a remote character's counter is never advanced. | verified (assembly) | `GameLogic.SaveGame`, `GetPlayerSerializeData`, `PlayerSerialize.Serialize` |

**Recommendation for Spec E.** The vanilla authority for poison decay is the
character's owner, not the host. Keep the owner computing the counter, add an
explicit owner-to-all sync of the counter value after each change, and have the
host persist the mirrored values in the framework save record and send them
back to owners on load before the first end turn. A strictly host-authoritative
model would move decay off the owner and change vanilla timing.

## FR-7: Patch ordering with existing framework patches

Existing patches on the lobby, room, join and save paths:

| Declaring type | Target | Kind | Purpose |
| --- | --- | --- | --- |
| `HotReloadSessionEntry` | `uiStartGame`: `ShowGameConfig`, `ShowResumeBrowser`, `ShowLobby`, `OnResumeGame`, `SetLoadGame`, `SetLoadMap`, `ShowCreateCharacterMC`, `ShowCreateCharacter`, `ConnectToServer`, `CreateMap`, `StartGame`, `TutorialStartGame`, `EnterGame`, `LoadGame`, `GameLobbyJoinRequested`, `OnLobbyEnter`, `JoinGameDirect`, `_OnJoinedLobby`, `OnJoinedRoom`, `_OnJoinedRoom`, `_OnCreatedRoom`; `uiCharacterCreateRoot.CreateUI`; `GameLogic`: `CreateOnlineRoom`, `CreateOfflineRoom`, `JoinGame`, `LoadGame`, `SaveGame`, `SaveMap`, `RestartGame` | Prefix, normal priority | Always blocks when `ClassPreferences.RecoveryFaulted` or `MarketplaceRuntime.LeaseFailure`. With title-screen activation enabled, it blocks during reload work, seals activation on every entry, blocks online callbacks and online or unprotected resume entries, and pins the save namespace. |
| `HotReloadTitleNavigation` | `StartGameFE.MainScreen`: `ShowLoreStore`, `OnOptionsMenu`, `ShowLanguage`, `OnGameExit`, `OnJoinGame`; `StartGameFE.uiLoreStore.Show`; `uiOptionsMenu.Show` | Prefix | With activation enabled, blocks `OnJoinGame` with a notice and locks title navigation during reload work. |
| `HotReloadStartupJoinGuard` | `uiStartGame.Start` | Prefix | With activation enabled and `+connect_lobby` present, seals and skips `Start` entirely, which also skips registering the Steam invite callbacks. |
| `HotReloadFocusGuard`, `HotReloadCloseGuard` | `FTKInput.SetFocus`; `FTKInput.Close`, `FTKInput.LostFocus` | Prefix | Hold focus while navigation is locked. Relevant because refusal dialogs use `SetFocus`. |
| `SaveNamespace` (Harmony ID `com.ftkmf.save-namespace`, installed only when requested) | Transpilers on `uiStartGame.GenerateResumeFilename`, `ResumeBrowser.RefreshGameList`, `MainScreen.OnPreSetFocus`, `MainScreen.OnResume`, `GameLogic.Save(bool, bool)`; postfix `uiStartGame.GetSaveFileFullName`; prefixes `MainScreen.OnPreSetFocus`, `MainScreen.OnResume`, `ResumeBrowser.RefreshGameList`, `uiStartGame.OnResumeGame`, `uiStartGame.LoadGame` | Mixed | Redirect adventure saves and `LastSave` into the active content namespace and refuse out-of-namespace resumes. |
| `GameConfigShow_Patch` | `StartGameFE.GameConfig.Show` | Prefix | Ensures registered adventures appear on the host configuration screen. |
| `CacheGameDefsInitialize_Patch` | `GameCache.Cache.GameDefinitions.Initialize` | Postfix | Re-injects registered adventures, which `WaitingRoom.Show` resolves through `GetPreview`. |
| `IsValidSaveFileName_Patch` | `FTKHub.IsValidSaveFileName` | Postfix | Accepts registered adventures in the adventure and resume lists. |

Adjacent but not on these paths: `ModsButtonPatch`, `ModSplash` and
`ReportingMenu` postfix `MainScreen.OnSetFocus`, and
`TableManager_Initialize_Patch` restores registered rows when `RestartGame`
reloads `FTK_main`.

| Claim | Evidence | Method |
| --- | --- | --- |
| `HotReloadSessionEntry` is broader than the spec describes. It covers the whole invite chain, both joined-room callbacks and `GameLogic.RestartGame`, which is the refusal path's reset. | verified (assembly) | `Core/HotReload/HotReloadBoundary.cs` |
| In HarmonyX 2.7.0 every prefix runs even after an earlier prefix returns `false`; results are combined with a logical AND and exposed through `__runOriginal`. A new gate prefix that runs after a skipping prefix would still execute unless it checks `__runOriginal`. | verified (assembly) | `HarmonyLib.Public.Patching.HarmonyManipulator.WritePrefixes` in the 2.7.0 reference package |
| The HarmonyX build shipped by the installed BepInEx behaves the same way. | inferred | Live gate L13 |
| Refusing a join cannot seal title-screen activation wrongly. `HotReloadBoundary.Seal` is a no-op unless activation is enabled, and with activation enabled every online entry (`OnJoinGame`, `ConnectToServer`, `OnLobbyEnter`, `+connect_lobby`, online joined-room callbacks) is already blocked before a gate could run. | verified (assembly) | `HotReloadBoundary.Seal`, `HotReloadSessionEntry.Prefix`, `HotReloadTitleNavigation.Prefix`, `HotReloadStartupJoinGuard.Prefix` |
| No existing patch targets `GameConfig.CreateOnlineRoom`, `WaitingRoom.OnLobbyCreated`, `RoomUI.Set`, `Lobby.JoinGame`, `uiStartGame.OnPhotonPlayerConnected` or `FTKNetworkObject.StateDataSerialize` / `StateDataDeserialize`. | verified (assembly) | Search of every Harmony attribute and manual patch call in the framework sources |

**Required ordering.** On any method shared with `HotReloadSessionEntry`
(`OnLobbyEnter`, `OnJoinedRoom`, `_OnJoinedRoom`), new gate prefixes use a
priority below `Priority.Normal` so the boundary decides first, take
`bool __runOriginal`, and do nothing when it is already `false`. Gates must not
call `HotReloadBoundary.Seal`. Refusal continues through `GameLogic.RestartGame`,
which `HotReloadSessionEntry` blocks only in fault states that already block
`ConnectToServer`.

## FR-8: Lobby payload budget

| Claim | Evidence | Method |
| --- | --- | --- |
| Vanilla lobby-listed keys are the 9 fixed keys (`pw`, `creator`, `diff`, `diff2`, `dev`, `ver`, `roomid`, `gamedef`, `maxplayer`) plus one per `FTK_gameParams.ID` in the rules (at most `chaos`, `lifepool`, `inflation`, `deliver_gold`). `hostdlc` is room-only. | verified (assembly) | `GameConfig.CreateOnlineRoom`, `FTK_gameParams.ID`, `GameFlow.Rules2.SetHashTable` |
| The Photon client rejects any string whose UTF-8 length exceeds 32767 bytes and any array longer than 32767 elements. | verified (assembly) | `ExitGames.Client.Photon.Protocol16.SerializeString`, `SerializeArray` |
| The client MTU defaults to 1200 bytes; larger reliable messages fragment. | verified (assembly) | `PhotonPeer.MaximumTransferUnit` |
| Photon Cloud limits: total custom properties per room under 500 kB; per-client server buffer 500 kB; messages over about 1.2 kB fragment; rarely sent messages should stay under 10 kB; lobby-listed properties go to every lobby client, so keys should be short. | inferred | Photon Realtime FAQ (current documentation, not versioned for PUN 1.85) |

**Recommended sizes.** One lobby key whose value is at most 24 ASCII characters
(contract version plus a 16-hex-digit, 64-bit digest). Room-only manifest at
most 8 KB UTF-8 and session tweak set at most 1 KB; parsers reject larger
values and cap list lengths. The digest only preselects in the browser; the
post-join gate compares the full manifest with per-package SHA-256 values.

## FR-8a: Session membership and modes

| Claim | Evidence | Method |
| --- | --- | --- |
| Once `uiStartGame.m_GameStarted` is set (in `EnterFahrulRPC`), any player leaving ends the run for everyone. `uiStartGame.OnPhotonPlayerDisconnected` shows `ShowPlayerDisconnectDialog`, whose continuation is `CharacterBackToMain`, then `GameLogic.RestartGame`. `GameLogic.OnPhotonPlayerDisconnected` sets `m_GameAborted` through `PrepareToDisconnect`, and `GameFlowMC.OnPhotonPlayerDisconnected` prunes the player and ends their turn or combat slot on the master. The flag the spec calls `GameLogic.m_GameStarted` is `uiStartGame.m_GameStarted`. | verified (assembly) | `uiStartGame.EnterFahrulRPC`, `uiStartGame.OnPhotonPlayerDisconnected`, `GameLogic.OnPhotonPlayerDisconnected`, `GameLogic.PrepareToDisconnect`, `GameFlowMC.OnPhotonPlayerDisconnected` |
| No game type overrides `OnMasterClientSwitched`; only `PhotonView` updates scene-view ownership. PUN raises `OnMasterClientSwitched` before `OnPhotonPlayerDisconnected` when the master leaves. In the waiting room, a client whose `m_MasterClientID` left gets `ShowPlayerDisconnectDialog` and restarts, so there is no host migration. | verified (assembly) | `NetworkingPeer.HandleEventLeave`, `CheckMasterClient`, `PhotonView.OnMasterClientSwitched`, `uiStartGame.OnPhotonPlayerDisconnected` |
| No one can join an online run after character creation begins. `uiStartGame.ShowCreateCharacter` sets `PhotonNetwork.room.open = false`, which sends `OpSetPropertiesOfRoom`. It runs on clients by RPC from `WaitingRoom.OnCharacterSelect`, and on the host through `ShowCreateCharacterMC`, whose caller is the FSM. | verified (assembly) | `uiStartGame.ShowCreateCharacter`, `Room.IsOpen` setter, `WaitingRoom.OnCharacterSelect` |
| Solo and local multiplayer rooms are unjoinable. `GameLogic.CreateOnlineRoom` is only called from `_OnJoinedLobby` with `_isOpen: false` (closed and invisible, one or three slots). `GameLogic.CreateOfflineRoom` sets `PhotonNetwork.offlineMode`. Only `GameConfig.CreateOnlineRoom` creates open rooms, in `GameMode.Multiplayer`. | verified (assembly) | `GameLogic.CreateOnlineRoom`, `CreateOfflineRoom`, `uiStartGame._OnJoinedLobby`, `GameConfig.OnStartGame` |

Consequences: the session contract is fixed from room creation to the end of
the run, the host is permanent, and framework keys are published only from
`GameConfig.CreateOnlineRoom`.

## FR-8b: Build drift across platforms

| Claim | Evidence | Method |
| --- | --- | --- |
| macOS `Assembly-CSharp.dll` SHA-256 is `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`, matching the hash pinned by `HotReloadBoundary` and `SaveNamespace`. | verified (assembly) | SHA-256 of the installed file; framework source |
| No Windows assembly is available locally. Its hash and any difference in the cited methods are unknown. | inferred | Live gate L11 |
| Steam invites only work across platforms if both builds produce the same `GetVersionFull()` string, including `m_SilentID`, the 32-bit prefix and `PlatformName`. | verified (assembly) | `FTKVersion.GetVersionFull`, `uiStartGame.OnLobbyEnter` |
| The session identity cannot reuse `SaveSetIdentity.Compute` as called by `SaveNamespace`, because that call mixes in the platform assembly hash; Windows and macOS peers would never match. | verified (assembly) | `SaveSetIdentity.Compute`, `SaveNamespace.FingerprintFor` |

## FR-9: Draft contract recommendation

> **Draft.** This recommendation rests on Phase 1 assembly evidence and is
> pending the Phase 2 live checks listed below. Spec B must not treat any
> `inferred` row as settled.

**Keys.** Reserve the prefix `ftkmf.` for every framework room, lobby, player
and Steam lobby key. A dot cannot occur in a vanilla key or a C# field name.

| Key | Where | Value |
| --- | --- | --- |
| `ver` | Room, lobby-listed (existing) | `GetVersion() + "+ftkmf"`; always present |
| `ftkmf.d` | Room, appended to `CustomRoomPropertiesForLobby` | `"<contract>:<digest16>"`, at most 24 ASCII characters |
| `ftkmf.m` | Room only | Bounded manifest: framework version, contract version, packages (ID, version, SHA-256), behavior DLLs, content-affecting settings; at most 8 KB |
| `ftkmf.s` | Room only | Host session tweak set, adopted not compared; at most 1 KB |
| `ftkmf.p` | Player custom property, set before `ConnectToServer` | `"<contract>:<digest16>"` |
| `joininfo` (Steam lobby) | Existing key | Third field becomes `GetVersionFull() + " ftkmf:" + digest16`, with no commas |
| `ftkmf.session` | `GameFlow` state dictionary in saves | Versioned string holding the run's session tweak set; at most 1 KB |

The digest is a domain-separated SHA-256 (for example `ftkmf-session-v1`) over
the framework version, `GetVersion()`, enabled packages with hashes, behavior
DLLs and content-affecting settings. It excludes the platform assembly hash.

**Publishing.** Transpile the single `PhotonNetwork.CreateRoom` call in
`GameConfig.CreateOnlineRoom(bool, string, bool, string, DifficultyType, TimeOfDay)`
to pass the options through a framework decorator. Assert exactly one match, as
`SaveNamespace.RewritePaths` does, and fail closed by not hosting online if the
IL has drifted. Postfix `WaitingRoom.OnLobbyCreated` to rewrite `joininfo`.

**Gates on each join path.**

1. Browser, pre-join: postfix `RoomUI.Set` to set row joinability and a reason
   from `ver` and `ftkmf.d`; prefix `Lobby.JoinGame` to refuse an incompatible
   `m_RoomSelected`, closing the leaky auto-selection.
2. Invite, warm and cold (`+connect_lobby`): prefix `uiStartGame.OnLobbyEnter`.
   Parse `joininfo`; on a digest mismatch, show the reason and skip
   `RestartGame`. Because the cold-start path reaches the same callback, one
   gate covers both.
3. Both paths, post-join: prefix `uiStartGame.OnJoinedRoom` for
   `GameMode.Multiplayer` and `!isMasterClient`, before `"PHOTON / JOINED ROOM"`
   and therefore before `_OnJoinedRoom` and `WaitingRoom.Show`. Read `ftkmf.m`
   and `ftkmf.s` within bounds. On a mismatch or a missing key, skip the
   original, call `PhotonNetwork.Disconnect()`, and show `uiSystemDialog` with
   the reason and `ContinueFSM(CharacterBackToMain)`. On a match, adopt
   `ftkmf.s` as the effective set.
4. Host: postfix `uiStartGame.OnPhotonPlayerConnected`. A missing or different
   `ftkmf.p` leads to `PhotonNetwork.CloseConnection(newPlayer)`, leaving the
   vanilla waiting-room branch to remove the slot.

**Refusing vanilla clients.** The marked `ver` disables the row button, the
marked `joininfo` makes vanilla invitees ignore the invite before any network
join, and host-side `CloseConnection` covers the leaky browser selection.
Vanilla players receive no explanation; the game offers no channel for one.

**Harmony ordering.** Gates sharing a method with `HotReloadSessionEntry` use
a priority below `Priority.Normal`, honor `__runOriginal`, and never call
`HotReloadBoundary.Seal`. The new transpiler and postfixes target methods no
existing patch touches.

**Save key.** Transpile `FTKNetworkObject.StateDataSerialize(bool)` to insert
`ldarg.0; call Decorate(dict, self)` just before its single
`SerializationHelpers.SerializeToContent<Dictionary<string, object>, FullSerializerSerializer>`
call. `Decorate` adds `ftkmf.session` only when `self is GameFlow` and a run is
locked, so with nothing to add the output is vanilla's byte for byte. Assert
exactly one match, as `SaveNamespace.RewritePaths` does; on a mismatch, keep the
original IL, write no record and warn once. A postfix that re-serialized the
returned string was rejected, because it would round-trip object-typed values
such as `Rules2` through an unproven path. Prefix
`FTKNetworkObject.StateDataDeserialize(string, bool)` with the `GameFlow` check
first; while a resume is armed and unlocked, it parses the string with the same
FullSerializer helper to read the record. No sidecar file is needed. Spec #253
implements this for solo and local play; `docs/TWEAKS.md` documents it.

**Resumed co-op flow.**

1. The host picks a run; `uiStartGame.OnResumeGame` sets `m_LoadFileName`.
2. Before the room decorator publishes, the framework reads
   `GameSerialize.GetGameSerialize(m_LoadFileName).m_GameStates.m_GameFlowStates`
   and takes the session set from `ftkmf.session`. A save with no record
   follows epic #232 open question 3.
3. The room publishes `ftkmf.s` from the saved record, not from preferences.
4. Joiners adopt it at the post-join gate.
5. The host load and `ClientDeserializeFinalRPC` both deliver the record to
   `GameFlow` state deserialization. Each peer verifies it equals the adopted
   set and treats a difference as a contract fault.
6. Later saves write the effective set.

**Scope changes for #232.** The `hostdlc` precedent does not exist as a
refusal. Poison decay is owner-authoritative in vanilla. The session identity
must exclude the platform assembly hash. These are Phase 3 updates to #232.

## Live gates

No two-client environment is currently confirmed. Every item below is open.

- **L1** Two-client baseline with matching mod sets, through the browser and a
  Steam invite. Confirms the FSM routes `OnJoinedLobby` and `OnJoinedRoom` to
  `_OnJoinedLobby` and `_OnJoinedRoom`.
- **L2** Post-join refusal on both paths. Checks: the joiner reaches a usable
  title with no stuck FSM state and no rejoin loop; the host's waiting room
  drops the slot; the host is not sent to the title.
- **L3** Vanilla client, framework host: whether the main Join button can join
  an auto-selected incompatible room.
- **L4** Warm Steam invite with a marked `joininfo`: a framework invitee joins,
  a vanilla invitee ignores it, and a mismatched framework invitee sees a
  reason before `RestartGame`.
- **L5** Cold-start `+connect_lobby` invite: the digest is ready when
  `OnLobbyEnter` fires, with both outcomes.
- **L6** Host `CloseConnection` of a vanilla client: the vanilla client's end
  state (waiting room, lobby or loop), and the host's list.
- **L7** A player property set before joining is visible in the host's
  `OnPhotonPlayerConnected` against the live server.
- **L8** Save record: manual save, autosave and load with the key; vanilla with
  the framework disabled loads it and drops it on re-save; a resumed run
  delivers it to clients.
- **L9** Game-free FullSerializer round trip of a string in
  `Dictionary<string, object>`.
- **L10** The `uiStartGame.m_GameVersion` scene value, only if option (c) is
  reconsidered.
- **L11** Windows `Assembly-CSharp.dll` SHA-256, a diff of every method cited
  here, and a cross-platform invite.
- **L12** The host leaving the waiting room, and any player leaving mid-run,
  returns all peers to the title.
- **L13** Prefix and `__runOriginal` semantics of the HarmonyX build shipped
  with the installed BepInEx.
- **L14** The `PhotonServerSettings.JoinLobby` value, which decides auto-join
  after a refusal. Covered by L2 and L6.
- **L15** The overworld `CheckEndTurnAction` runs on the character owner.

No child issue is complete on the strength of this document.
