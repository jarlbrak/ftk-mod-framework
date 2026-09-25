using System;
using System.Collections.Generic;
using FTKModFramework.Core;
using Newtonsoft.Json.Linq;

namespace UnityEngine
{
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color black { get { return new Color(0,0,0,1); } }
    }
    public sealed class Material
    {
        public string name;
        public readonly HashSet<string> properties=new HashSet<string>();
        public readonly HashSet<string> keywords=new HashSet<string>();
        public readonly Dictionary<string,object> textures=new Dictionary<string,object>();
        public readonly Dictionary<string,Color> colors=new Dictionary<string,Color>();
        public int writes;
        public Material(){}
        public Material(Material source)
        {
            name=source.name;
            properties.UnionWith(source.properties);keywords.UnionWith(source.keywords);
            foreach(var item in source.textures)textures[item.Key]=item.Value;
            foreach(var item in source.colors)colors[item.Key]=item.Value;
        }
        public bool HasProperty(string property){return properties.Contains(property);}
        public void DisableKeyword(string keyword){keywords.Remove(keyword);writes++;}
        public void SetColor(string property,Color value){if(!HasProperty(property))throw new Exception("Missing color property");colors[property]=value;writes++;}
        public void SetTexture(string property,object value){if(!HasProperty(property))throw new Exception("Missing texture property");textures[property]=value;writes++;}
    }
}

static class Program
{
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Main()
    {
        var oldConstructor=typeof(EnemyRendererMesh).GetConstructor(new[]{typeof(string),typeof(string),typeof(string)});
        Check(oldConstructor!=null,"Old binary constructor retained");
        Check(!new EnemyRendererMesh("body","body.glb").DisableNativeEmission,"Default preserves emission");
        Check(!new EnemyRendererMesh("body","body.glb","body.png",false).DisableNativeEmission,"Explicit false preserves emission");
        var assignment=new EnemyRendererMesh("body","body.glb","body.png",true);
        Check(assignment.DisableNativeEmission,"Explicit option retained");
        Check(assignment.RendererKind==EnemyRendererKind.SkinnedMeshRenderer,"Existing constructors retain skinned target kind");
        var rigid=EnemyRendererMesh.ForStaticRenderer("eye","eye.glb","eye.png",true);
        Check(rigid.RendererKind==EnemyRendererKind.MeshRenderer&&rigid.DisableNativeEmission,"Rigid descriptor records exact target kind and material option");
        var snapshot=(EnemyRendererMesh[])new[]{assignment}.Clone();
        Check(snapshot[0].DisableNativeEmission,"Immutable descriptor survives registration array snapshot");
        var native=new UnityEngine.Material();native.properties.UnionWith(new[]{"_MainTex","_EmissionMap","_EmissionColor"});
        native.keywords.UnionWith(new[]{"_EMISSION","_NORMALMAP"});object albedo=new object(),emission=new object();
        native.textures["_MainTex"]=albedo;native.textures["_EmissionMap"]=emission;native.colors["_EmissionColor"]=new UnityEngine.Color(2,2,2,1);
        var preserved=new UnityEngine.Material(native);ExplicitMaterialOptions.Apply(preserved,false);
        Check(preserved.writes==0 && preserved.keywords.Contains("_EMISSION") && preserved.colors["_EmissionColor"].r==2 && preserved.textures["_EmissionMap"]==emission,"Default clone unchanged");
        var privateCopy=new UnityEngine.Material(native);ExplicitMaterialOptions.Apply(privateCopy,true);
        Check(!privateCopy.keywords.Contains("_EMISSION"),"Keyword disabled");
        var color=privateCopy.colors["_EmissionColor"];
        Check(color.r==0 && color.g==0 && color.b==0 && color.a==1,"Emission black");
        Check(privateCopy.textures["_EmissionMap"]==null,"Emission map cleared");
        Check(privateCopy.textures["_MainTex"]==albedo && privateCopy.keywords.Contains("_NORMALMAP"),"Other material state preserved");
        Check(native.writes==0 && native.keywords.Contains("_EMISSION") && native.textures["_EmissionMap"]==emission && native.colors["_EmissionColor"].r==2,"Source material unchanged");
        ExplicitMaterialOptions.Apply(privateCopy,true);
        Check(privateCopy.textures["_EmissionMap"]==null && privateCopy.colors["_EmissionColor"].r==0,"Repeated option is idempotent");
        var unsupported=new UnityEngine.Material();unsupported.keywords.Add("_EMISSION");ExplicitMaterialOptions.Apply(unsupported,true);
        Check(unsupported.writes==1 && !unsupported.keywords.Contains("_EMISSION"),"Shader without emission properties handled");
        foreach (string suffix in new[] { "_skin", "_hair", "_main" })
        {
            var source = new UnityEngine.Material { name = "native" + suffix };
            source.properties.Add("_Color"); source.colors["_Color"] = new UnityEngine.Color(.1f, .8f, .2f, 1f);
            var owned = new UnityEngine.Material(source);
            ExplicitMaterialOptions.PreserveMainPalette(owned);
            Check(ExplicitMaterialOptions.HasAuthoredPalette(owned) == (suffix == "_main"), "ordinary palette rule stays main-only: " + suffix);
            ExplicitMaterialOptions.PreservePalette(owned);
            Check(ExplicitMaterialOptions.HasAuthoredPalette(owned) && owned.colors["_Color"].r == 1f &&
                owned.colors["_Color"].g == 1f && owned.colors["_Color"].b == 1f, "race palette neutralizes native tint: " + suffix);
            string marker = owned.name; ExplicitMaterialOptions.PreservePalette(owned);
            Check(owned.name == marker && ExplicitMaterialOptions.HasAuthoredPalette(new UnityEngine.Material(owned)),
                "race palette marker is idempotent and survives material copy: " + suffix);
            Check(source.name == "native" + suffix && source.colors["_Color"].g == .8f && source.writes == 0,
                "source palette remains untouched: " + suffix);
        }
        Check(!RendererEmissionFixture.Read(null),"Absent JSON defaults false");
        Check(!RendererEmissionFixture.Read(new JValue(false)) && RendererEmissionFixture.Read(new JValue(true)),"JSON boolean accepted");
        foreach(var invalid in new[]{new JValue((object)null),new JValue("true"),new JValue(1),new JValue(0.0)})
        {
            bool rejected=false;try{RendererEmissionFixture.Read(invalid);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Nonboolean JSON rejected");
        }
        Console.WriteLine("PASS "+checks+" material-option assertions (Unity stand-ins; no runtime evidence).");
    }
}
