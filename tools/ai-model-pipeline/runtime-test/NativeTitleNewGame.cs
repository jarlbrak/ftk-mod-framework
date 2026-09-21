using System;
using System.Collections.Generic;
using StartGameFE;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    bool nativeTitleNewGameConsumed;
    string nativeTitleNewGameToken;
    MainScreen nativeTitleNewGameScreen;
    uiStartGame nativeTitleNewGameMenu;
    Button nativeTitleNewGameButton;

    static string TitlePath(Transform value,Transform root)
    {
        if(value==null)return null;List<string> parts=new List<string>();
        Transform current=value;
        for(;current!=null && current!=root;current=current.parent)parts.Add(current.name);
        if(current!=root)return null;parts.Reverse();return string.Join("/",parts.ToArray());
    }
    JObject InspectNativeTitleNewGame(out MainScreen screen,out uiStartGame menu,out List<Button> candidates)
    {
        screen=uiScreen.gCurrent as MainScreen;menu=uiStartGame.Instance;candidates=new List<Button>();
        if(screen==null || !SceneOwner(screen) || !screen.gameObject.activeInHierarchy || menu==null ||
            !SceneOwner(menu) || !menu.gameObject.activeInHierarchy || menu.m_GameConfig==null ||
            FTKInput.Instance==null || FTKInput.Instance.m_CurrentInputFocus!=screen)
            throw new InvalidOperationException("Active native Main Screen with its input focus and game configuration is required.");
        JArray observed=new JArray();
        foreach(Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if(button==null || !SceneOwner(button) || !button.gameObject.activeInHierarchy || !button.isActiveAndEnabled ||
                !button.IsInteractable() || button.onClick.GetPersistentEventCount()!=1 ||
                button.onClick.GetPersistentTarget(0)!=screen || button.onClick.GetPersistentMethodName(0)!="OnNewGame")continue;
            candidates.Add(button);observed.Add(new JObject{{"instanceId",button.GetInstanceID()},{"path",TitlePath(button.transform,screen.transform)},
                {"siblingIndex",button.transform.GetSiblingIndex()},{"parentId",button.transform.parent==null?0:button.transform.parent.GetInstanceID()}});
        }
        return new JObject{{"screenInstanceId",screen.GetInstanceID()},{"menuInstanceId",menu.GetInstanceID()},
            {"isResume",menu.m_GameConfig.m_IsResume},{"fsmState",menu.m_FSM==null?null:menu.m_FSM.ActiveStateName},{"candidates",observed}};
    }
    JObject NativeTitleNewGame(JObject command)
    {
        CatalogKeys(command,"id","session","op","action","inspectionToken","buttonInstanceId");
        CatalogNoLinks(root);string action=Str(command,"action");
        if(action!="inspect" && action!="submit")throw new ArgumentException("Use inspect or submit.");
        if(nativeTitleNewGameConsumed)throw new InvalidOperationException("Native title New Game already consumed for this process.");
        MainScreen screen;uiStartGame menu;List<Button> candidates;JObject current=InspectNativeTitleNewGame(out screen,out menu,out candidates);
        if(action=="inspect")
        {
            nativeTitleNewGameToken=Guid.NewGuid().ToString("N");nativeTitleNewGameScreen=screen;nativeTitleNewGameMenu=menu;
            nativeTitleNewGameButton=null;int requested=Int(command,"buttonInstanceId",0);
            if(requested!=0)foreach(Button button in candidates)if(button.GetInstanceID()==requested)
            {nativeTitleNewGameButton=button;break;}
            current["inspectionToken"]=nativeTitleNewGameToken;current["selectedButtonInstanceId"]=nativeTitleNewGameButton==null?0:nativeTitleNewGameButton.GetInstanceID();
            current["eligible"]=nativeTitleNewGameButton!=null;return new JObject{{"ok",true},{"readOnly",true},{"status",nativeTitleNewGameButton==null?"native_title_new_game_selection_required":"native_title_new_game_eligible"},{"pins",current}};
        }
        if(nativeTitleNewGameScreen!=screen || nativeTitleNewGameMenu!=menu || nativeTitleNewGameButton==null ||
            Str(command,"inspectionToken")!=nativeTitleNewGameToken || Int(command,"buttonInstanceId",0)!=nativeTitleNewGameButton.GetInstanceID() || !candidates.Contains(nativeTitleNewGameButton))
            throw new InvalidOperationException("Exact inspected native title New Game control changed.");
        nativeTitleNewGameConsumed=true;screen.OnNewGame();
        return new JObject{{"ok",true},{"status","native_title_new_game_submitted"},{"callback","StartGameFE.MainScreen.OnNewGame"},
            {"buttonInstanceId",nativeTitleNewGameButton.GetInstanceID()},{"screenInstanceId",screen.GetInstanceID()},{"menuInstanceId",menu.GetInstanceID()},
            {"isResume",menu.m_GameConfig.m_IsResume},{"fsmState",menu.m_FSM==null?null:menu.m_FSM.ActiveStateName},
            {"scope","One inspected native title New Game callback. The next native Create Game route must establish configuration readiness separately."}};
    }
}
