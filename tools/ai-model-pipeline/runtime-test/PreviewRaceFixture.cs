using System;
using System.Reflection;
using UnityEngine;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class PreviewRacePin
    {
        internal uiQuickPlayerCreate owner;
        internal int classId,changedFrame;
        internal FTK_playerGameStart.SkinType original;
        internal PlayerInventory inventory;
        internal JToken preserved;
        internal CharacterEventListener retired;
        internal bool restoring;
    }
    PreviewRacePin previewRace;
    static int PreviewRaceIndex(JObject command,string key)
    {
        JToken value=command[key];
        if(value==null || value.Type!=JTokenType.Integer)throw new ArgumentException("Exact integer required: "+key);
        int result=(int)value;if(result<0)throw new ArgumentException("Nonnegative index required: "+key);return result;
    }
    static MethodInfo PreviewRaceRebuildMethod()
    {return typeof(uiQuickPlayerCreate).GetMethod("SetClass",Members,null,new[]{typeof(int)},null);}
    static JArray PreviewRaceColor(Color color){return new JArray(color.r,color.g,color.b,color.a);}
    static JToken PreviewRacePreserved(uiQuickPlayerCreate preview)
    {
        return new JObject{{"inventory",PreviewInventory(preview)},
            {"outfit",preview.m_CustomOutfit==null?null:new JArray((int)preview.m_CustomOutfit.m_ArmorID,(int)preview.m_CustomOutfit.m_HelmetID,(int)preview.m_CustomOutfit.m_BackpackID)},
            {"indices",new JArray(preview.m_MainColorIndex,preview.m_SkinColorIndex,preview.m_HairColorIndex)},
            {"main",PreviewRaceColor(preview.m_ImageColorMain.color)},
            {"skin",PreviewRaceColor(preview.m_ImageColorSkin.color)},{"hair",PreviewRaceColor(preview.m_ImageColorHair.color)}};
    }
    bool PreviewRaceSettled()
    {
        return previewRace!=null && previewRace.owner!=null && Time.frameCount>previewRace.changedFrame &&
            previewRace.retired==null && previewRace.owner.m_Avatar!=null &&
            previewRace.owner.m_Avatar.gameObject.activeInHierarchy &&
            previewRace.owner.m_Avatar.m_uiQuickPlayerCreate==previewRace.owner;
    }
    void PreviewRaceRequireStudioReady(int ownerId)
    {
        if(previewRace!=null && previewRace.owner!=null && previewRace.owner.GetInstanceID()==ownerId && !PreviewRaceSettled())
            throw new InvalidOperationException("Race preview rebuild has not settled; inspect on a later frame.");
    }
    void PreviewRaceCheckPreserved()
    {
        if(previewRace.owner.m_ClassID!=previewRace.classId ||
            !object.ReferenceEquals(previewRace.owner.m_PlayerInventory,previewRace.inventory) ||
            !JToken.DeepEquals(previewRace.preserved,PreviewRacePreserved(previewRace.owner)))
            throw new InvalidOperationException("Native preview rebuild changed pinned class, inventory, outfit or colors.");
    }
    void PreviewRaceRebuild(FTK_playerGameStart.SkinType skin)
    {
        MethodInfo method=PreviewRaceRebuildMethod();
        if(method==null || method.ReturnType!=typeof(void))throw new InvalidOperationException("Exact native SetClass(int) unavailable");
        previewRace.retired=previewRace.owner.m_Avatar;previewRace.changedFrame=Time.frameCount;
        previewRace.owner.m_SkinType=skin;
        method.Invoke(previewRace.owner,new object[]{previewRace.classId});
        PreviewRaceCheckPreserved();
        if(previewRace.owner.m_SkinType!=skin)throw new InvalidOperationException("Native preview did not retain requested supported skin.");
    }
    void PreviewRaceCleanup()
    {
        if(previewRace==null)return;
        try
        {
            if(previewRace.owner!=null && SceneOwner(previewRace.owner) && previewRace.owner.m_ClassID==previewRace.classId && previewRace.owner.m_SkinType!=previewRace.original)
                PreviewRaceRebuild(previewRace.original);
        }
        catch(Exception e){Logger.LogWarning("Preview race restoration failed: "+e.GetType().Name);}
        previewRace=null;
    }
    JObject PreviewRaceFixture(JObject command)
    {
        CatalogKeys(command,"id","session","op","action","ownerInstanceId","classId","skinType");
        RequireSinglePlayer();CatalogNoLinks(root);
        int ownerId=LeaseObservationPin.ExactId(command,"ownerInstanceId",false);
        int classId=PreviewRaceIndex(command,"classId");
        uiQuickPlayerCreate owner=null;
        foreach(uiQuickPlayerCreate candidate in Resources.FindObjectsOfTypeAll<uiQuickPlayerCreate>())
            if(candidate!=null && SceneOwner(candidate) && candidate.GetInstanceID()==ownerId)owner=candidate;
        if(owner==null || !owner.gameObject.activeInHierarchy || owner.m_ClassID!=classId || owner.m_Avatar==null || owner.m_Avatar.m_uiQuickPlayerCreate!=owner)
            throw new InvalidOperationException("Exact active native preview and class required.");
        if(previewRace!=null && (previewRace.owner!=owner || previewRace.classId!=classId))
            throw new InvalidOperationException("Another race fixture is active; restore its exact preview first.");
        FTK_playerGameStart row=FTK_playerGameStartDB.GetDB().GetEntry((FTK_playerGameStart.ID)classId);
        if(row==null || row.m_Skinsets==null)throw new InvalidOperationException("Native class skinsets unavailable.");
        string action=Str(command,"action");
        if(action=="apply")
        {
            int skin=PreviewRaceIndex(command,"skinType");
            if(skin<0 || skin>6 || skin>=row.m_Skinsets.Length || row.m_Skinsets[skin]==FTK_skinset.ID.None || FTK_skinsetDB.Get(row.m_Skinsets[skin])==null)
                throw new ArgumentException("Requested native skin is unsupported by this class.");
            if(previewRace!=null && !PreviewRaceSettled())throw new InvalidOperationException("Prior rebuild has not settled.");
            int original=(int)owner.m_SkinType;
            if(original<0 || original>=row.m_Skinsets.Length || row.m_Skinsets[original]==FTK_skinset.ID.None)
                throw new InvalidOperationException("Original native skin is not restorable.");
            if(previewRace==null)previewRace=new PreviewRacePin{owner=owner,classId=classId,original=owner.m_SkinType,inventory=owner.m_PlayerInventory,preserved=PreviewRacePreserved(owner)};
            else PreviewRaceCheckPreserved();
            previewRace.restoring=false;
            try{PreviewRaceRebuild((FTK_playerGameStart.SkinType)skin);}
            catch{try{previewRace.restoring=true;PreviewRaceRebuild(previewRace.original);}catch(Exception e){Logger.LogWarning("Preview race rollback failed: "+e.GetType().Name);}throw;}
        }
        else if(action=="restore")
        {
            if(previewRace!=null)
            {
                if(!PreviewRaceSettled())throw new InvalidOperationException("Prior rebuild has not settled.");
                PreviewRaceCheckPreserved();previewRace.restoring=true;PreviewRaceRebuild(previewRace.original);
            }
        }
        else if(action!="inspect")throw new ArgumentException("action must be inspect, apply or restore");
        bool settled=previewRace==null || PreviewRaceSettled();
        if(previewRace!=null && settled){PreviewRaceCheckPreserved();if(previewRace.restoring)previewRace=null;}
        JArray skins=new JArray();
        for(int i=0;i<7;i++)
        {
            FTK_skinset skin=i>=row.m_Skinsets.Length || row.m_Skinsets[i]==FTK_skinset.ID.None?null:FTK_skinsetDB.Get(row.m_Skinsets[i]);
            skins.Add(new JObject{{"skinType",i},{"name",((FTK_playerGameStart.SkinType)i).ToString()},
                {"skinset",skin==null?null:skin.m_ID},{"supported",skin!=null},
                {"nativeUnlocked",FTK_loreExtraUnlockDB.GetDB().IsSkinUnlocked((FTK_playerGameStart.SkinType)i)}});
        }
        return new JObject{{"ok",true},{"ownerInstanceId",ownerId},{"classId",classId},
            {"celInstanceId",owner.m_Avatar==null?0:owner.m_Avatar.GetInstanceID()},{"skinType",(int)owner.m_SkinType},
            {"originalSkinType",previewRace==null?new JValue((object)null):new JValue((int)previewRace.original)},
            {"fixtureActive",previewRace!=null},{"restoring",previewRace!=null && previewRace.restoring},{"studioReady",settled},
            {"skins",skins},{"scope","Isolated native preview fit fixture; no unlocks, preferences, inventory changes or game start. Restore before leaving preview."}};
    }
}
