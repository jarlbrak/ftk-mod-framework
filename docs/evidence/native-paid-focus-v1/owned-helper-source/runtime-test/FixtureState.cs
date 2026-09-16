using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static object Field(object value,string name)
    {
        if(value==null)return null;
        var field=value.GetType().GetField(name,Members);
        return field==null?null:field.GetValue(value);
    }
    static bool NativeButtonUsable(VoteButton button)
    {
        if(button==null || !button.enabled || !button.gameObject.activeInHierarchy || button.m_Focusing)return false;
        Button native=button.GetComponent<Button>();
        return native!=null && native.enabled && native.IsInteractable();
    }
    static JObject FsmView(object value)
    {
        if(value==null)return new JObject{{"present",false}};
        Behaviour behaviour=value as Behaviour;var state=value.GetType().GetProperty("ActiveStateName",Members);
        return new JObject{{"present",true},{"enabled",behaviour!=null && behaviour.enabled},
            {"state",state==null?null:Convert.ToString(state.GetValue(value,null))}};
    }
    static JArray VoteViews()
    {
        JArray result=new JArray();object hub=Instance(typeof(FTKHub));IEnumerable party=Field(hub,"m_CharacterOverworlds")as IEnumerable;
        if(party==null)return result;
        foreach(object value in party)
        {
            CharacterOverworld cow=value as CharacterOverworld;if(cow==null)continue;
            object hud=Field(cow,"m_UIPlayMainHud"),container=Field(hud,"m_LootCollectionButtons");
            Component ui=container as Component;IDictionary table=Field(container,"m_VoteButtonTable")as IDictionary;
            JArray buttons=new JArray();
            foreach(VoteButton.VoteOption option in new[]{VoteButton.VoteOption.Collect,VoteButton.VoteOption.Ready})
            {
                VoteButton button=table!=null && table.Contains(option)?table[option]as VoteButton:null;
                if(button==null)continue;
                Button native=button.GetComponent<Button>();
                buttons.Add(new JObject{{"option",option.ToString()},{"instanceId",button.GetInstanceID()},
                    {"active",button.gameObject.activeInHierarchy},{"enabled",button.enabled},
                    {"interactable",native!=null && native.IsInteractable()},{"focusing",button.m_Focusing},
                    {"usable",NativeButtonUsable(button)},{"item",button.m_AssociatedItem.ToString()},
                    {"belongsToHero",button.m_Hud!=null && button.m_Hud.m_Cow==cow}});
            }
            CharacterStats stats=Field(cow,"m_CharacterStats")as CharacterStats;
            result.Add(new JObject{{"heroInstanceId",cow.GetInstanceID()},{"alive",stats!=null && stats.m_HealthCurrent>0},
                {"containerInstanceId",ui==null?0:ui.GetInstanceID()},{"containerActive",ui!=null && ui.gameObject.activeInHierarchy},
                {"voteType",Convert.ToString(Field(container,"m_VoteType"))},{"buttons",buttons}});
        }
        return result;
    }
    static Dictionary<int,VoteButton> RequireLootCollect()
    {
        RequireSinglePlayer();object session=Instance(typeof(EncounterSession)),master=Instance(typeof(EncounterSessionMC));
        if(!EncounterSessionMC.VoteType.Loot.Equals(Field(session,"m_VoteType")) || !EncounterSessionMC.VoteType.Loot.Equals(Field(master,"m_VoteType")))
            throw new InvalidOperationException("Both current encounter vote types must be Loot.");
        Behaviour fsm=Field(master,"m_LootCollectionFSM")as Behaviour;
        if(fsm==null || !fsm.enabled)throw new InvalidOperationException("Native loot collection FSM must be enabled.");
        object hub=Instance(typeof(FTKHub));IEnumerable party=Field(hub,"m_CharacterOverworlds")as IEnumerable;
        if(party==null)throw new InvalidOperationException("No living party.");
        Dictionary<int,VoteButton> result=new Dictionary<int,VoteButton>();int alive=0;
        foreach(object value in party)
        {
            CharacterOverworld cow=value as CharacterOverworld;if(cow==null)continue;
            CharacterStats stats=Field(cow,"m_CharacterStats")as CharacterStats;
            if(stats==null || stats.m_HealthCurrent<=0)throw new InvalidOperationException("Loot fixture requires every party member alive.");
            alive++;
            object hud=Field(cow,"m_UIPlayMainHud"),container=Field(hud,"m_LootCollectionButtons");
            Component ui=container as Component;
            if(ui==null || !ui.gameObject.activeInHierarchy || !EncounterSessionMC.VoteType.Loot.Equals(Field(container,"m_VoteType")))continue;
            IDictionary table=Field(container,"m_VoteButtonTable")as IDictionary;
            VoteButton button=table!=null && table.Contains(VoteButton.VoteOption.Collect)?table[VoteButton.VoteOption.Collect]as VoteButton:null;
            if(NativeButtonUsable(button) && button.m_Option==VoteButton.VoteOption.Collect && button.m_Hud!=null && button.m_Hud.m_Cow==cow)
                result.Add(cow.GetInstanceID(),button);
        }
        if(alive==0 || result.Count==0)throw new InvalidOperationException("No active, interactable native Collect button in a current Loot container.");
        return result;
    }
    JObject CollectLoot(JObject command)
    {
        Dictionary<int,VoteButton> buttons=RequireLootCollect();int hero=Int(command,"heroInstanceId",0);
        if(hero==0)
        {
            if(buttons.Count!=1)throw new ArgumentException("Multiple current Collect buttons; supply exact heroInstanceId.");
            foreach(int id in buttons.Keys)hero=id;
        }
        VoteButton button;if(!buttons.TryGetValue(hero,out button))throw new ArgumentException("Hero has no usable current Collect button.");
        string item=button.m_AssociatedItem.ToString();int buttonId=button.GetInstanceID();
        typeof(VoteButton).GetMethod("OnLeftClick",Members,null,Type.EmptyTypes,null).Invoke(button,null);
        return new JObject{{"ok",true},{"status","clicked"},{"heroInstanceId",hero},{"buttonInstanceId",buttonId},
            {"item",item},{"method","VoteButton.OnLeftClick(Collect)"},{"note","One native current Loot vote submitted; observe fixture-state again before any further collection."}};
    }
    static JObject DungeonSlotView(MiniHexDungeon dungeon)
    {
        if(dungeon==null)return null;
        JObject view=new JObject{{"level",dungeon.m_Level},{"room",dungeon.m_RoomIndex},
            {"activeDungeonEncounterType",dungeon.m_EncounterType.ToString()}};
        try
        {
            int levels=dungeon.GetLevelCount();
            bool levelValid=dungeon.m_Level>=0 && dungeon.m_Level<levels;
            int definitionRooms=levelValid?dungeon.GetRoomCount(dungeon.m_Level):-1;
            List<MiniHexDungeon.RoomInfo> rooms=null;
            if(dungeon.m_DungeonEncounters!=null)dungeon.m_DungeonEncounters.TryGetValue(dungeon.m_Level,out rooms);
            bool queuedValid=rooms!=null && dungeon.m_RoomIndex>=0 && dungeon.m_RoomIndex<rooms.Count;
            MiniHexDungeon.RoomInfo queued=queuedValid?rooms[dungeon.m_RoomIndex]:null;
            bool inDefinition=levelValid && dungeon.m_RoomIndex>=0 && dungeon.m_RoomIndex<definitionRooms;
            view["nextLevelGenerated"]=dungeon.m_DungeonEncounters!=null && dungeon.m_DungeonEncounters.ContainsKey(dungeon.m_Level+1);
            view["isAtLastLevel"]=dungeon.IsAtLastLevel();view["isDungeonCleared"]=dungeon.IsDungeonCleared();
            view["definitionLevelCount"]=levels;view["definitionRoomCount"]=definitionRooms;
            view["generatedRoomCount"]=rooms==null?new JValue((object)null):new JValue(rooms.Count);
            view["queuedSlotValid"]=queuedValid;view["withinDefinitionRoomBounds"]=inDefinition;
            view["queuedRoomType"]=queued==null?null:queued.m_Type.ToString();
            view["queuedContext"]=queued==null?new JValue((object)null):new JValue(queued.m_Context);
            JArray objects=new JArray();if(queued!=null && queued.m_EncounterObjects!=null)foreach(string entry in queued.m_EncounterObjects)objects.Add(entry);
            view["queuedObjects"]=objects;
            view["slotAllowsEnemySubstitution"]=inDefinition && queued!=null && queued.m_Type==MiniHexDungeon.EncounterType.Enemy
                && dungeon.m_EncounterType!=MiniHexDungeon.EncounterType.Stair
                && dungeon.m_EncounterType!=MiniHexDungeon.EncounterType.ExitRoom
                && dungeon.m_EncounterType!=MiniHexDungeon.EncounterType.Cleared;
            view["note"]="Slot eligibility only; stage-next-enemy also requires strict native Ready. Appended staircase is outside definition room bounds.";
        }
        catch(Exception error){view["slotAllowsEnemySubstitution"]=false;view["error"]=error.Message;}
        return view;
    }
    JObject FixtureState()
    {
        JObject ready,loot;
        try{ReadyContext context=RequireReadyPreparation();ready=new JObject{{"ok",true},{"level",context.dungeon.m_Level},{"room",context.dungeon.m_RoomIndex},{"buttonCount",context.buttons.Count}};}
        catch(Exception error){ready=new JObject{{"ok",false},{"reason",error.Message}};}
        try{Dictionary<int,VoteButton> buttons=RequireLootCollect();loot=new JObject{{"ok",true},{"buttonCount",buttons.Count}};}
        catch(Exception error){loot=new JObject{{"ok",false},{"reason",error.Message}};}
        object session=Instance(typeof(EncounterSession)),master=Instance(typeof(EncounterSessionMC)),flow=Instance(typeof(GameFlow));
        IList order=Field(master,"m_FightOrder")as IList;MiniHexDungeon dungeon=Field(flow,"m_DungeonEntered")as MiniHexDungeon;
        JArray scopes=new JArray();
        foreach(string scope in new[]{"player-overworld","player-combat","player-preview"})
        {
            JArray avatars=new JArray();
            try
            {
                foreach(AvatarOwner owner in Owners(scope))
                {
                    int total=0,usable=0;
                    foreach(SkinnedMeshRenderer renderer in owner.cel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        total++;
                        try{FindOwner(renderer,null);if(renderer.sharedMesh!=null)usable++;}catch(InvalidOperationException){}
                    }
                    avatars.Add(new JObject{{"ownerInstanceId",owner.owner.GetInstanceID()},{"celInstanceId",owner.cel.GetInstanceID()},
                        {"ownerActive",owner.owner.gameObject.activeInHierarchy},{"avatarActive",owner.cel.gameObject.activeInHierarchy},
                        {"renderers",total},{"usableRenderers",usable}});
                }
                scopes.Add(new JObject{{"scope",scope},{"avatars",avatars}});
            }catch(Exception error){scopes.Add(new JObject{{"scope",scope},{"error",error.Message}});}
        }
        return new JObject{{"ok",true},{"provenance","read-only fixture readiness snapshot"},{"strictReady",ready},{"strictLootCollect",loot},
            {"voteSurfaces",VoteViews()},{"playerScopes",scopes},
            {"identity",new JObject{{"root",root},{"rootName",new System.IO.DirectoryInfo(root).Name},{"session",sessionId},{"isolatedSavePath",isolatedSavePath}}},
            {"sessions",new JObject{{"esInCombat",Convert.ToBoolean(Field(session,"m_IsInCombat"))},{"mcInCombat",Convert.ToBoolean(Field(master,"m_IsInCombat"))},
                {"mcEncounterType",Convert.ToString(Field(master,"m_EncounterType"))},{"esVoteType",Convert.ToString(Field(session,"m_VoteType"))},
                {"mcVoteType",Convert.ToString(Field(master,"m_VoteType"))},{"fightOrderCount",order==null?-1:order.Count},
                {"esFsm",FsmView(Field(session,"m_FSM"))},{"voteFsm",FsmView(Field(master,"m_VoteFSM"))},{"lootFsm",FsmView(Field(master,"m_LootCollectionFSM"))}}},
            {"dungeon",DungeonSlotView(dungeon)}};
    }
}
