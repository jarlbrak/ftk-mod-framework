using System;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace FtkResourcePrototype {
public partial class Plugin {
 JObject ScrollFixture(){
  GameObject go=new GameObject("OwnedScrollingFixture");go.SetActive(false);
  Material original=new Material(Shader.Find("Unlit/Texture")),foreign=null,nativeCopy=null;
  Material owned=null;JArray cases=new JArray();
  try{
   MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=original;
   ScrollingUVs scroller=go.AddComponent<ScrollingUVs>();scroller.uvAnimationRate=new Vector2(.125f,-.25f);scroller.textureName="_MainTex";
   CharacterEventListener cel=go.AddComponent<CharacterEventListener>();
   Assembly core=null;foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="FTKModFramework")core=a;
   MethodInfo setup=core.GetType("FTKModFramework.Core.LegacyVisualResources",true).GetMethod("Materials",BindingFlags.NonPublic|BindingFlags.Static);
   setup.Invoke(null,new object[]{cel,renderer,new Action<Material>(delegate(Material m){}),null});owned=renderer.sharedMaterial;
   UnityEngine.Object.DestroyImmediate(cel);scroller.enabled=false;go.SetActive(true);
   FieldInfo phase=typeof(ScrollingUVs).GetField("uvOffset",BindingFlags.NonPublic|BindingFlags.Instance);
   Action tick=(Action)Delegate.CreateDelegate(typeof(Action),scroller,typeof(ScrollingUVs).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance));
   foreach(string mode in new[]{"enabled","disabled","re-enabled","foreign-fallback"}){
    renderer.enabled=mode!="disabled";
    if(mode=="foreign-fallback"){foreign=new Material(original);renderer.sharedMaterial=foreign;}
    Vector2 expected=(Vector2)phase.GetValue(scroller),previous=renderer.sharedMaterial.GetTextureOffset("_MainTex");
    int count=mode=="foreign-fallback"?1:1000;
    for(int i=0;i<count;i++){expected+=scroller.uvAnimationRate*Time.deltaTime;tick();}
    Vector2 actual=(Vector2)phase.GetValue(scroller),material=renderer.sharedMaterial.GetTextureOffset("_MainTex");
    bool phaseEqual=actual.x==expected.x&&actual.y==expected.y;
    Vector2 wanted=renderer.enabled?expected:previous;
    bool materialEqual=material.x==wanted.x&&material.y==wanted.y;
    if(!phaseEqual||!materialEqual)throw new Exception("Scrolling result mismatch: "+mode);
    cases.Add(new JObject{{"mode",mode},{"calls",count},{"phaseEqual",phaseEqual},{"materialEqual",materialEqual},{"ownedMaterialUnchanged",renderer.sharedMaterial==owned}});
   }
   nativeCopy=renderer.sharedMaterial;
   return new JObject{{"cases",cases},{"deltaTime",Time.deltaTime},{"originalOffsetUnchanged",original.GetTextureOffset("_MainTex")==Vector2.zero}};
  }finally{Destroy(go);Destroy(original);if(foreign!=null)Destroy(foreign);if(nativeCopy!=null&&nativeCopy!=owned&&nativeCopy!=foreign)Destroy(nativeCopy);}
 }
}}
