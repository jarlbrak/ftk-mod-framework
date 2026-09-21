using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    readonly HashSet<string> guardianIncapacityClaims=new HashSet<string>();

    static JObject GuardianIncapacityView(CharacterDummy dummy)
    {
        Type runtime=GuardianFixtureRuntime();object state=GuardianFixtureState();
        string identity=GuardianFixtureIdentity(dummy);
        return new JObject{{"identity",identity},{"dummyInstanceId",dummy.GetInstanceID()},
            {"hp",dummy.GetCurrentHealth()},{"stunned",dummy.Stunned},{"petrified",dummy.Petrified},
            {"canAct",(bool)GuardianQuery(runtime,null,"CanAct",dummy)},
            {"active",(bool)GuardianQuery(state.GetType(),state,"IsActive",identity)},
            {"designatedAlly",(string)GuardianQuery(state.GetType(),state,"DesignatedAlly",identity)},
            {"rescueAvailable",GuardianFixtureRescue(dummy)}};
    }

    JObject GuardianIncapacityFixture(JObject command)
    {
        CatalogKeys(command,"id","session","op","encounterInstanceId","guardianInstanceId","category");
        CatalogNoLinks(root);RequireSinglePlayer();
        EncounterSession encounter=EncounterSession.Instance;
        uiBattleStanceButtons buttons=FTKUI.Instance.m_BattleStanceButtons;
        if(encounter==null || !encounter.m_IsInCombat || encounter.GetInstanceID()!=LeaseObservationPin.ExactId(command,"encounterInstanceId",true)
            || buttons==null || !buttons.m_Initialized || buttons.CombatCow==null
            || buttons.CombatCow.GetCombatDummy().m_CharacterDummyFSM.ActiveStateName!="Wait For Stance")
            throw new InvalidOperationException("Exact current combat at stable native hero stance required.");
        if(guardianDamageReceipt!=null && guardianDamageReceipt.Policy.Active)
            throw new InvalidOperationException("A damage fixture is still active.");
        int id=LeaseObservationPin.ExactId(command,"guardianInstanceId",true);
        CharacterDummy guardian=null;
        foreach(CharacterDummy candidate in encounter.m_PlayerDummies.Values)
            if(candidate!=null && candidate.GetInstanceID()==id)
            {if(guardian!=null)throw new InvalidOperationException("Ambiguous guardian.");guardian=candidate;}
        if(guardian==null || guardian.m_CharacterOverworld==null || !guardian.m_CharacterOverworld.IsOwner
            || !(bool)GuardianQuery(GuardianFixtureRuntime(),null,"IsGuardian",guardian))
            throw new InvalidOperationException("Exact owned current Guardian required.");
        string category=Str(command,"category");
        FTK_proficiencyTable.ID proficiency;
        ProficiencyBase.Category nativeCategory;
        if(category=="Stunned"){proficiency=FTK_proficiencyTable.ID.enStun;nativeCategory=ProficiencyBase.Category.Stunned;}
        // Petrify needs a surviving native hit and asynchronous visual completion.
        // A synchronous Add/Remove fixture cannot establish that lifecycle.
        else throw new ArgumentException("Only Stunned is supported by this synchronous fixture.");
        ProficiencyBase effect=ProficiencyManager.Instance.Get(proficiency);
        JObject before=GuardianIncapacityView(guardian);
        if(effect==null || effect.m_Category!=nativeCategory || effect.IsImmune(guardian)
            || !(bool)before["canAct"] || !(bool)before["active"] || before["designatedAlly"].Type!=JTokenType.String
            || guardian.m_SufferingProficiencies.Count!=0)
            throw new InvalidOperationException("Active eligible Guard without existing proficiency effects required.");
        string claim=encounter.GetInstanceID()+":"+id+":"+category;
        if(!guardianIncapacityClaims.Add(claim))throw new InvalidOperationException("This guardian/category fixture was already attempted in this encounter.");
        JObject applied=null,recovered=null;
        try
        {
            guardian.AddProfToDummy(new[]{proficiency},false,false);
            applied=GuardianIncapacityView(guardian);
        }
        finally
        {
            // Remove only the newly introduced category through its native End path.
            guardian.RemoveSpecificProficiency(nativeCategory);
            recovered=GuardianIncapacityView(guardian);
        }
        bool passed=!(bool)applied["canAct"] && !(bool)applied["active"]
            && (bool)recovered["canAct"] && !(bool)recovered["active"]
            && JToken.DeepEquals(before["designatedAlly"],recovered["designatedAlly"])
            && JToken.DeepEquals(before["rescueAvailable"],recovered["rescueAvailable"]);
        return new JObject{{"ok",passed},{"status",passed?"passed":"failed"},{"category",category},
            {"proficiency",proficiency.ToString()},{"encounterInstanceId",encounter.GetInstanceID()},
            {"before",before},{"applied",applied},{"recovered",recovered},
            {"scope","Explicit native AddProfToDummy/RemoveSpecificProficiency fixture. No enemy attack, natural duration, turn progression or online claim. Guardian state is queried only, never set by this helper."}};
    }
}
