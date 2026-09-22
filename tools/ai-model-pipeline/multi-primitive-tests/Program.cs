using System;using System.Collections.Generic;using System.IO;using FTKModFramework.Core;using UnityEngine;
namespace UnityEngine {
 public class Object {public static void Destroy(Object value){} }
 public class Transform {public string name;}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;} }
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
 public struct Vector4 {public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;} }
 public struct Matrix4x4 {float[] v;public float this[int i]{get{return v==null?0:v[i];}set{if(v==null)v=new float[16];v[i]=value;}} public float this[int r,int c]{get{return this[c*4+r];}set{this[c*4+r]=value;}} public Matrix4x4 inverse=>this;public Vector4 GetColumn(int c)=>new Vector4(this[0,c],this[1,c],this[2,c],this[3,c]); }
 public struct BoneWeight {public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
 public class Mesh:Object {public string name;public Vector3[] vertices,normals;public Vector2[] uv;public int[] triangles;public BoneWeight[] boneWeights;public Matrix4x4[] bindposes;public int subMeshCount;public Dictionary<int,int[]> parts=new Dictionary<int,int[]>();public void SetTriangles(int[] values,int slot){parts[slot]=values;}public void RecalculateNormals(){}public void RecalculateBounds(){} }
}
namespace FTKModFramework.Core {static class CustomModelLoader {public static string ResolveModelPath(string path)=>path;}static class Plugin {public static LogSink Log=new LogSink();public class LogSink {public void LogWarning(string value){Console.WriteLine(value);}public void LogInfo(string value){}}}}
class Program {
 static int n;static void Check(bool ok,string message){n++;if(!ok)throw new Exception(message);}
 static void Reject(Action action,string message){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,message);}
 static void ValidatePackageAssets(string directory){int staticCount=0,skinCount=0;
 foreach(string path in Directory.GetFiles(directory,"*.glb")){
 byte[] bytes=File.ReadAllBytes(path);int jsonLength=BitConverter.ToInt32(bytes,12);using(var doc=System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes,20,jsonLength))){var root=doc.RootElement;
 if(!root.TryGetProperty("skins",out var skins)||skins.GetArrayLength()==0){RuntimeGltfMeshLoader.Preflight(path,null,null);staticCount++;}
 else {var skin=skins[0];var joints=skin.GetProperty("joints");var names=new Transform[joints.GetArrayLength()];for(int i=0;i<names.Length;i++)names[i]=new Transform{name=root.GetProperty("nodes")[joints[i].GetInt32()].GetProperty("name").GetString()};
 var accessor=root.GetProperty("accessors")[skin.GetProperty("inverseBindMatrices").GetInt32()];var view=root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];int offset=28+jsonLength+(view.TryGetProperty("byteOffset",out var v)?v.GetInt32():0)+(accessor.TryGetProperty("byteOffset",out var a)?a.GetInt32():0);var matrices=new Matrix4x4[accessor.GetProperty("count").GetInt32()];for(int i=0;i<matrices.Length;i++)for(int k=0;k<16;k++)matrices[i][k]=BitConverter.ToSingle(bytes,offset+64*i+4*k);
 RuntimeGltfMeshLoader.Preflight(path,names,matrices);skinCount++;}
 }}
 Console.WriteLine("PASS package structure: "+staticCount+" static and "+skinCount+" skinned GLBs; skin self-consistency only, not native binding.");}
 static void Main(string[] args){var root=args[0];var bones=new[]{new Transform{name="root"}};var ibm=new Matrix4x4();for(int i=0;i<4;i++)ibm[i,i]=1;var binds=new[]{ibm};
 var mesh=RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"two.glb"),bones,binds,true,new[]{1,0});Check(mesh!=null&&mesh.subMeshCount==2,"actual loader accepts two primitives");Check(mesh.parts[1][0]==0&&mesh.parts[0][0]==3,"actual loader remaps primitive indices to explicit native slots");Check(mesh.uv[0].y==.5f&&mesh.boneWeights.Length==6&&mesh.bindposes.Length==1,"one shared skin/attributes");
 Check(RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"two.glb"),bones,binds,true)==null,"legacy strict mode rejects multi primitive");
 foreach(var map in new[]{new[]{0,0},new[]{0,2},new[]{0,1,2}})Check(RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"two.glb"),bones,binds,true,map)==null,"invalid mapping rejected");
 foreach(var path in Directory.GetFiles(root,"bad-*.glb"))if(!Path.GetFileName(path).StartsWith("bad-static-"))Check(RuntimeGltfMeshLoader.LoadSkinnedGlb(path,bones,binds,true,new[]{0,1})==null,"malformed second primitive rejected: "+path);
 var legacy=RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"legacy.glb"),bones,binds,true);Check(legacy!=null&&legacy.triangles.Length==6,"single-primitive path preserved");
 var rigid=RuntimeGltfMeshLoader.LoadStaticGlb(Path.Combine(root,"static.glb"),true);Check(rigid!=null&&rigid.vertices.Length==3&&rigid.triangles.Length==3&&rigid.uv[0].y==.8f,"actual loader accepts one unskinned rigid primitive in MeshFilter local space");
 foreach(var path in Directory.GetFiles(root,"bad-static-*.glb"))Check(RuntimeGltfMeshLoader.LoadStaticGlb(path,true)==null,"malformed static primitive rejected: "+path);
 RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"static.glb"),null,null);Check(true,"pure static preflight accepts valid rigid fixture");
 RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"legacy.glb"),bones,binds);Check(true,"pure skin preflight accepts exact native binding fixture");
 Reject(()=>RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"legacy.glb"),null,null),"static preflight rejects skinned data");
 Reject(()=>RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"static.glb"),bones,binds),"skin preflight rejects static data");
 Reject(()=>RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"legacy.glb"),new Transform[0],new Matrix4x4[0]),"unbound native garment fails closed");
 Reject(()=>RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"legacy.glb"),new[]{new Transform{name="wrong"}},binds),"weighted native bone mismatch fails");
 var wrongIbm=new Matrix4x4();Reject(()=>RuntimeGltfMeshLoader.Preflight(Path.Combine(root,"legacy.glb"),bones,new[]{wrongIbm}),"native inverse bind matrix mismatch fails");
 foreach(var path in Directory.GetFiles(root,"bad-static-*.glb"))Reject(()=>RuntimeGltfMeshLoader.Preflight(path,null,null),"pure preflight rejects malformed static data");
 foreach(var path in Directory.GetFiles(root,"bad-*.glb"))if(!Path.GetFileName(path).StartsWith("bad-static-"))Reject(()=>RuntimeGltfMeshLoader.Preflight(path,bones,binds),"pure preflight rejects malformed/multiple skin primitives");
 RuntimeGltfMeshLoader.PreflightResolved(Path.Combine(root,"static.glb"),null,null);
 RuntimeGltfMeshLoader.PreflightResolved(Path.Combine(root,"legacy.glb"),new[]{"root"},binds);
 foreach(var path in Directory.GetFiles(root,"bad-static-*.glb"))Reject(()=>RuntimeGltfMeshLoader.PreflightResolved(path,null,null),"resolved worker decoder rejects identical malformed static fixture");
 foreach(var path in Directory.GetFiles(root,"bad-*.glb"))if(!Path.GetFileName(path).StartsWith("bad-static-"))Reject(()=>RuntimeGltfMeshLoader.PreflightResolved(path,new[]{"root"},binds),"resolved worker decoder rejects identical malformed skin fixture");
 if(args.Length>1)ValidatePackageAssets(args[1]);
 Console.WriteLine("PASS "+n+" actual loader checks with Unity boundary stand-ins; no live proof.");}
}