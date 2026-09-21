using System;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class NativeFocusReceipt
    {
        internal string Token,HeroFID,Stage="pending",Reason;
        internal EncounterSession Encounter;
        internal UnityEngine.Object Diorama;
        internal CharacterOverworld Cow;
        internal CharacterDummy Dummy;
        internal CharacterEventListener Avatar;
        internal uiBattleStanceButtons Owner;
        internal uiBattleButton Button;
        internal int BeforeFocus,BeforeSpent,AfterFocus,AfterSpent,Slots;
        internal float Deadline;
    }
    NativeFocusReceipt nativeFocusReceipt;
    static string NativeFocusIdentity(CharacterDummy d){return d.FID.m_TurnIndex+":"+d.FID.m_PhotonID;}
    static void NativeFocusPins(NativeFocusReceipt r)
    {
        RequireSinglePlayer();
        if(r.Encounter==null || EncounterSession.Instance!=r.Encounter || !r.Encounter.m_IsInCombat ||
            r.Encounter.m_ActiveDiorama!=r.Diorama || r.Cow==null || r.Dummy==null || !r.Dummy.m_IsAlive ||
            r.Cow.GetCombatDummy()!=r.Dummy || r.Dummy.m_EventListener!=r.Avatar || r.Avatar==null ||
            r.Owner==null || FTKUI.Instance.m_BattleStanceButtons!=r.Owner || !r.Owner.m_Initialized ||
            r.Owner.CombatCow!=r.Cow || r.Dummy.m_CharacterDummyFSM.ActiveStateName!="Wait For Stance" ||
            r.Button==null || !r.Button.gameObject.activeInHierarchy || r.Button.m_Owner!=r.Owner ||
            r.Owner.m_CombatActionProfile.m_Button!=r.Button || r.Owner.m_CombatActionProfile.m_Slots!=r.Slots ||
            r.Owner.m_InputFocus==null || r.Owner.m_InputFocus.m_CurrentSelected==null ||
            r.Owner.m_InputFocus.m_CurrentSelected.gameObject!=r.Button.gameObject)
            throw new InvalidOperationException("Pinned encounter, hero, avatar or selected attack changed");
    }
    JObject NativeCombatFocus(JObject command)
    {
        CatalogKeys(command,"id","session","op","action","expectedHeroFID","expectedFocus","expectedSpentFocus","receipt");
        if(Str(command,"action")=="inspect")
        {
            if(nativeFocusReceipt==null || Str(command,"receipt")!=nativeFocusReceipt.Token)throw new ArgumentException("Exact receipt required");
            NativeCombatFocusTick();return NativeFocusReport();
        }
        if(Str(command,"action")!="spend")throw new ArgumentException("Action must be spend or inspect");
        if(nativeFocusReceipt!=null && (nativeFocusReceipt.Stage=="pending" || nativeFocusReceipt.Stage=="unknown"))
            throw new InvalidOperationException("Previous Focus attempt is pending or uncertain; no retry");
        RequireSinglePlayer();
        uiBattleStanceButtons owner=FTKUI.Instance.m_BattleStanceButtons;
        if(owner==null || owner.CombatCow==null)throw new InvalidOperationException("Native hero stance required");
        CharacterOverworld cow=owner.CombatCow;CharacterDummy dummy=cow.GetCombatDummy();
        uiBattleButton button=owner.m_CombatActionProfile.m_Button;
        if(dummy==null || button==null || EncounterSession.Instance==null)throw new InvalidOperationException("Selected native attack required");
        NativeFocusReceipt r=new NativeFocusReceipt{Token=Guid.NewGuid().ToString("N"),HeroFID=NativeFocusIdentity(dummy),Encounter=EncounterSession.Instance,
            Diorama=EncounterSession.Instance.m_ActiveDiorama,Cow=cow,Dummy=dummy,Avatar=dummy.m_EventListener,Owner=owner,Button=button,
            BeforeFocus=cow.m_CharacterStats.m_FocusPoints,BeforeSpent=cow.m_CharacterStats.SpentFocus,
            AfterFocus=cow.m_CharacterStats.m_FocusPoints,AfterSpent=cow.m_CharacterStats.SpentFocus,
            Slots=owner.m_CombatActionProfile.m_Slots,Deadline=Time.realtimeSinceStartup+10};
        NativeFocusPins(r);
        if(NativeFocusIdentity(dummy)!=Str(command,"expectedHeroFID") || r.BeforeFocus!=Int(command,"expectedFocus",-1) ||
            r.BeforeSpent!=Int(command,"expectedSpentFocus",-1))throw new InvalidOperationException("Caller hero/Focus snapshot changed");
        if((button.m_ButtonType!=uiBattleButton.BattleButtonType.attack && button.m_ButtonType!=uiBattleButton.BattleButtonType.proficiency) ||
            !button.m_CanUse || owner.m_Focusing || owner.m_FocusInterrupt || !cow.m_CharacterStats.CanFocus() ||
            owner.m_CombatActionProfile.m_NoFocus || r.BeforeFocus<1 || r.BeforeSpent<0 || r.BeforeSpent>=r.Slots)
            throw new InvalidOperationException("Selected attack cannot spend Focus");
        Type guardUi=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.GuardianActionUi",true);
        MethodInfo isGuard=guardUi.GetMethod("IsGuard",Statics);
        if(isGuard==null || (bool)isGuard.Invoke(null,new object[]{owner,button}))throw new InvalidOperationException("Guard is excluded");
        // Consume this attempt before native UI dispatch. Only the native animation callback may
        // debit Focus; uncertain completion is never retried and spent Focus is never rolled back.
        nativeFocusReceipt=r;
        try{button.OnRightClick();}
        catch(Exception e){r.Stage="unknown";r.Reason=e.ToString();throw;}
        NativeCombatFocusTick();return NativeFocusReport();
    }
    void NativeCombatFocusTick()
    {
        NativeFocusReceipt r=nativeFocusReceipt;if(r==null || r.Stage!="pending")return;
        try
        {
            NativeFocusPins(r);
            r.AfterFocus=r.Cow.m_CharacterStats.m_FocusPoints;r.AfterSpent=r.Cow.m_CharacterStats.SpentFocus;
            if(r.AfterFocus==r.BeforeFocus-1 && r.AfterSpent==r.BeforeSpent+1 && !r.Owner.m_Focusing){r.Stage="passed";return;}
            if(r.AfterFocus!=r.BeforeFocus || r.AfterSpent!=r.BeforeSpent)
                throw new InvalidOperationException("Unexpected native Focus debit");
            if(Time.realtimeSinceStartup>r.Deadline)throw new InvalidOperationException("Native Focus callback deadline elapsed");
        }
        catch(Exception e){r.Stage="unknown";r.Reason=e.Message;}
    }
    JObject NativeFocusReport()
    {
        NativeFocusReceipt r=nativeFocusReceipt;
        return new JObject{{"receipt",r.Token},{"stage",r.Stage},{"reason",r.Reason},{"heroFID",r.HeroFID},
            {"focusBefore",r.BeforeFocus},{"focusAfter",r.AfterFocus},{"spentBefore",r.BeforeSpent},{"spentAfter",r.AfterSpent},
            {"scope","one native selected-button Focus input; no attack or direct Focus mutation"}};
    }
}
