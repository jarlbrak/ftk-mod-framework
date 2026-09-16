using System;using Newtonsoft.Json.Linq;
class Program
{
 static int count;
 static void Check(bool value){count++;if(!value)throw new Exception("Check failed");}
 static JObject Good()=>JObject.Parse("{\"materialSlots\":[{\"primitiveIndex\":1,\"nativeMaterialSlot\":0,\"textureFile\":\"a.png\",\"disableNativeEmission\":true},{\"primitiveIndex\":0,\"nativeMaterialSlot\":1}]}");
 static void Reject(Action<JObject> mutate){var obj=Good();mutate(obj);try{MaterialSlotFixture.Read(obj);}catch(InvalidOperationException){count++;return;}throw new Exception("Accepted malformed slots");}
 static void Main(string[] args){Check(MaterialSlotFixture.Read(new JObject())==null);var good=Good();var slots=MaterialSlotFixture.Read(good);Check(slots.Length==2&&slots[0].PrimitiveIndex==1&&slots[0].NativeMaterialSlot==0&&slots[0].TextureFile=="a.png"&&slots[0].DisableNativeEmission);Check(slots[1].TextureFile==null&&!slots[1].DisableNativeEmission);good["materialSlots"][0]["primitiveIndex"]=0;Check(slots[0].PrimitiveIndex==1);
 Reject(x=>x["materialSlots"]=new JValue((object)null));Reject(x=>x["materialSlots"]=new JObject());Reject(x=>x["materialSlots"]=new JArray());Reject(x=>((JArray)x["materialSlots"]).RemoveAt(0));Reject(x=>x["materialSlots"]=JArray.Parse("[{}, {}, {}, {}, {}]"));
 Reject(x=>x["textureFile"]=new JValue((object)null));Reject(x=>x["disableNativeEmission"]=false);
 Reject(x=>x["materialSlots"][0]["primitiveIndex"]=0);Reject(x=>x["materialSlots"][0]["nativeMaterialSlot"]=1);Reject(x=>x["materialSlots"][0]["primitiveIndex"]=2);Reject(x=>x["materialSlots"][0]["primitiveIndex"]=-1);Reject(x=>x["materialSlots"][0]["primitiveIndex"]=1.0);Reject(x=>x["materialSlots"][0]["primitiveIndex"]="1");Reject(x=>((JObject)x["materialSlots"][0]).Remove("nativeMaterialSlot"));
 Reject(x=>x["materialSlots"][0]["textureFile"]="../a.png");Reject(x=>x["materialSlots"][0]["textureFile"]="/a.png");Reject(x=>x["materialSlots"][0]["textureFile"]="a.jpg");Reject(x=>x["materialSlots"][0]["textureFile"]=new JValue((object)null));Reject(x=>x["materialSlots"][0]["disableNativeEmission"]=new JValue((object)null));Reject(x=>x["materialSlots"][0]["disableNativeEmission"]=1);Reject(x=>x["materialSlots"][0]["unknown"]=true);
 var full=JObject.Parse("{\"materialSlots\":[{\"primitiveIndex\":3,\"nativeMaterialSlot\":0},{\"primitiveIndex\":2,\"nativeMaterialSlot\":1},{\"primitiveIndex\":1,\"nativeMaterialSlot\":2},{\"primitiveIndex\":0,\"nativeMaterialSlot\":3}]}");Check(MaterialSlotFixture.Read(full).Length==4);
 if(args.Length>0){var catalog=JObject.Parse(System.IO.File.ReadAllText(args[0]));int rows=0;foreach(JObject p in (JArray)catalog["profiles"]){foreach(JObject r in (JArray)p["renderers"])Check(MaterialSlotFixture.Read(r)==null);rows++;}Check(rows==395);}
 Console.WriteLine("PASS "+count+" material-slot parser assertions; no live claim.");}
}
