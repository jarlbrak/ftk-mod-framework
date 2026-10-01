using System;
using FTKModFramework.Core.Data;
using Newtonsoft.Json;
internal static class Program
{
    private static int checks;
    private static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    private static ModelRendererEntry Parse(string fields,bool strict)
    {
        string json="{"+fields+"}";
        return strict ? JsonConvert.DeserializeObject<ModelRendererEntry>(json,new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error}) : JsonContentParser.Deserialize<ModelRendererEntry>(json);
    }
    private static void Main()
    {
        foreach(bool strict in new[]{false,true}) {
            Check(Parse("",strict).MetallicGlossTexture==null,"Omitted mask retains inherited material");
            Check(Parse("\"metallicGlossTexture\":null",strict).MetallicGlossTexture==null,"Explicit null retains inherited material");
            Check(Parse("\"metallicGlossTexture\":\"assets/mask.png\"",strict).MetallicGlossTexture=="assets/mask.png","Mask path preserved");
            foreach(string invalid in new[]{"true","1","1.0","{}","[]","\"\""}) {
                bool rejected=false;try{Parse("\"metallicGlossTexture\":"+invalid,strict);}catch(JsonException){rejected=true;}
                Check(rejected,"Invalid mask token rejected: "+invalid);
            }
        }
        foreach(string route in new[]{"itemModels","offHandModels","displayModels"}) {
            string json="{\"entries\":[{\"kind\":\"item\",\"id\":\"gear\",\""+route+"\":[{\"path\":\".\",\"model\":\"assets/a.glb\",\"texture\":\"assets/a.png\",\"metallicGlossTexture\":\"assets/mask.png\"}]}]}";
            var item=JsonContentParser.Deserialize<ContentFile>(json).Entries[0];
            var models=item.ItemModels??item.OffHandModels??item.DisplayModels;
            Check(models[0].MetallicGlossTexture=="assets/mask.png","Mask parses on "+route);
        }
        Console.WriteLine("PASS "+checks+" material mask JSON assertions (startup and candidate parsers).");
    }
}
namespace FTKModFramework.Core.Data { internal sealed class ItemModifierEntry {} }
