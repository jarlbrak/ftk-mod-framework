using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class ReadyContext
    {
        public MiniHexDungeon dungeon;
        public readonly Dictionary<int,VoteButton> buttons=new Dictionary<int,VoteButton>();
    }
    static ReadyContext RequireReadyPreparation()
    {
        RequireSinglePlayer();
        object session=Instance(typeof(EncounterSession)),master=Instance(typeof(EncounterSessionMC));
        if(session==null || master==null)throw new InvalidOperationException("Both encounter sessions required.");
        if((bool)typeof(EncounterSession).GetField("m_IsInCombat",Members).GetValue(session))
            throw new InvalidOperationException("Actual combat is active.");
        object encounter=typeof(EncounterSessionMC).GetField("m_EncounterType",Members).GetValue(master);
        if(!MiniHexDungeon.EncounterType.Ready.Equals(encounter))
            throw new InvalidOperationException("Native Ready preparation required; Break and other encounters are excluded.");
        if(!EncounterSessionMC.VoteType.Ready.Equals(typeof(EncounterSessionMC).GetField("m_VoteType",Members).GetValue(master))
            || !EncounterSessionMC.VoteType.Ready.Equals(typeof(EncounterSession).GetField("m_VoteType",Members).GetValue(session)))
            throw new InvalidOperationException("Both native vote types must be Ready.");
        Behaviour voteFsm=typeof(EncounterSessionMC).GetField("m_VoteFSM",Members).GetValue(master)as Behaviour;
        if(voteFsm==null || !voteFsm.enabled)throw new InvalidOperationException("Native Ready vote FSM is not enabled.");
        IList order=typeof(EncounterSessionMC).GetField("m_FightOrder",Members).GetValue(master)as IList;
        if(order==null || order.Count!=0)throw new InvalidOperationException("Ready requires an existing empty fight order.");
        object flow=Instance(typeof(GameFlow));
        MiniHexDungeon dungeon=typeof(GameFlow).GetField("m_DungeonEntered",Members).GetValue(flow)as MiniHexDungeon;
        if(dungeon==null)throw new InvalidOperationException("No entered dungeon.");
        object hub=Instance(typeof(FTKHub));IEnumerable party=typeof(FTKHub).GetField("m_CharacterOverworlds",Members).GetValue(hub)as IEnumerable;
        if(party==null)throw new InvalidOperationException("No live party.");
        ReadyContext context=new ReadyContext{dungeon=dungeon};int living=0;
        foreach(object value in party)
        {
            CharacterOverworld cow=value as CharacterOverworld;if(cow==null)continue;
            CharacterStats stats=typeof(CharacterOverworld).GetField("m_CharacterStats",Members).GetValue(cow)as CharacterStats;
            if(stats==null || stats.m_HealthCurrent<=0)throw new InvalidOperationException("Every party member must be alive.");
            living++;
            object hud=typeof(CharacterOverworld).GetField("m_UIPlayMainHud",Members).GetValue(cow);
            object container=hud==null?null:hud.GetType().GetField("m_LootCollectionButtons",Members).GetValue(hud);
            if(container==null || !EncounterSessionMC.VoteType.Ready.Equals(container.GetType().GetField("m_VoteType",Members).GetValue(container)))continue;
            IDictionary table=container.GetType().GetField("m_VoteButtonTable",Members).GetValue(container)as IDictionary;
            if(table==null || !table.Contains(VoteButton.VoteOption.Ready))continue;
            VoteButton button=table[VoteButton.VoteOption.Ready]as VoteButton;
            if(NativeButtonUsable(button) && button.m_Option==VoteButton.VoteOption.Ready && button.m_Hud!=null && button.m_Hud.m_Cow==cow)context.buttons.Add(cow.GetInstanceID(),button);
        }
        if(living==0 || context.buttons.Count==0)throw new InvalidOperationException("Alive party and an active native Ready button required.");
        return context;
    }
    JObject StageNextEnemy(JObject command)
    {
        ReadyContext context=RequireReadyPreparation();
        if(command["regenerate"]!=null && (bool)command["regenerate"])throw new ArgumentException("Ready staging never regenerates rooms.");
        int level=Int(command,"level",-1),room=Int(command,"room",-1);
        if(level!=context.dungeon.m_Level || room!=context.dungeon.m_RoomIndex)
            throw new ArgumentException("Supplied level/room must match the current native preparation slot exactly.");
        List<MiniHexDungeon.RoomInfo> rooms;
        if(context.dungeon.m_DungeonEncounters==null || !context.dungeon.m_DungeonEncounters.TryGetValue(level,out rooms)
            || room<0 || room>=rooms.Count)throw new InvalidOperationException("Current generated room is invalid.");
        string enemy=Str(command,"enemy");if(string.IsNullOrEmpty(enemy))throw new ArgumentException("Exact native enemy ID required.");
        FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID(enemy);
        if(row==null || row.m_ID!=enemy || row.m_EnemyAsset==null || row.m_WeaponAsset==null)
            throw new InvalidOperationException("Enemy must resolve exactly with native enemy and weapon assets.");
        MiniHexDungeon.RoomInfo previous=rooms[room];
        // Native generation appends Stair beyond GetRoomCount; never replace floor flow.
        if(context.dungeon.m_EncounterType==MiniHexDungeon.EncounterType.Stair
            || context.dungeon.m_EncounterType==MiniHexDungeon.EncounterType.ExitRoom
            || context.dungeon.m_EncounterType==MiniHexDungeon.EncounterType.Cleared
            || level<0 || level>=context.dungeon.GetLevelCount()
            || room>=context.dungeon.GetRoomCount(level)
            || previous==null || previous.m_Type!=MiniHexDungeon.EncounterType.Enemy)
            throw new InvalidOperationException("Only a native queued Enemy inside definition room bounds may be staged; preserve Stair/ExitRoom/Cleared and other non-enemy slots through native floor flow.");
        rooms[room]=new MiniHexDungeon.RoomInfo(MiniHexDungeon.EncounterType.Enemy,null,new[]{row.m_ID},-1);
        return new JObject{{"ok",true},{"enemy",row.m_ID},{"level",level},{"room",room},
            {"previousType",previous==null?null:previous.m_Type.ToString()},
            {"provenance","native Ready slot fixture; no indices, encounter sessions, FSMs or acknowledgments changed"},
            {"next","Use ready to click the existing native Ready button."}};
    }
    JObject ClickReady(JObject command)
    {
        ReadyContext context=RequireReadyPreparation();int hero=Int(command,"heroInstanceId",0);
        if(hero==0)
        {
            if(context.buttons.Count!=1)throw new ArgumentException("Multiple active Ready buttons; supply exact heroInstanceId.");
            foreach(int id in context.buttons.Keys)hero=id;
        }
        VoteButton button;
        if(!context.buttons.TryGetValue(hero,out button))throw new ArgumentException("Requested hero has no active native Ready button.");
        typeof(VoteButton).GetMethod("OnLeftClick",Members,null,Type.EmptyTypes,null).Invoke(button,null);
        return new JObject{{"ok",true},{"status","clicked"},{"heroInstanceId",hero},{"method","VoteButton.OnLeftClick(Ready)"},
            {"note","Native Ready vote submitted; poll the normal encounter flow. No raw FSM or acknowledgment was sent."}};
    }
}
