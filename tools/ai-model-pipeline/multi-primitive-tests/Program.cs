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
 static void Main(string[] args){var root=args[0];var bones=new[]{new Transform{name="root"}};var ibm=new Matrix4x4();for(int i=0;i<4;i++)ibm[i,i]=1;var binds=new[]{ibm};
 var mesh=RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"two.glb"),bones,binds,true,new[]{1,0});Check(mesh!=null&&mesh.subMeshCount==2,"actual loader accepts two primitives");Check(mesh.parts[1][0]==0&&mesh.parts[0][0]==3,"actual loader remaps primitive indices to explicit native slots");Check(mesh.uv[0].y==.5f&&mesh.boneWeights.Length==6&&mesh.bindposes.Length==1,"one shared skin/attributes");
 Check(RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"two.glb"),bones,binds,true)==null,"legacy strict mode rejects multi primitive");
 foreach(var map in new[]{new[]{0,0},new[]{0,2},new[]{0,1,2}})Check(RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"two.glb"),bones,binds,true,map)==null,"invalid mapping rejected");
 foreach(var path in Directory.GetFiles(root,"bad-*.glb"))if(!Path.GetFileName(path).StartsWith("bad-static-"))Check(RuntimeGltfMeshLoader.LoadSkinnedGlb(path,bones,binds,true,new[]{0,1})==null,"malformed second primitive rejected: "+path);
 var legacy=RuntimeGltfMeshLoader.LoadSkinnedGlb(Path.Combine(root,"legacy.glb"),bones,binds,true);Check(legacy!=null&&legacy.triangles.Length==6,"single-primitive path preserved");
 var rigid=RuntimeGltfMeshLoader.LoadStaticGlb(Path.Combine(root,"static.glb"),true);Check(rigid!=null&&rigid.vertices.Length==3&&rigid.triangles.Length==3&&rigid.uv[0].y==.8f,"actual loader accepts one unskinned rigid primitive in MeshFilter local space");
 foreach(var path in Directory.GetFiles(root,"bad-static-*.glb"))Check(RuntimeGltfMeshLoader.LoadStaticGlb(path,true)==null,"malformed static primitive rejected: "+path);
 Console.WriteLine("PASS "+n+" actual loader checks with Unity boundary stand-ins; no live proof.");}
}