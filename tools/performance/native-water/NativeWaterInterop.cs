using System;
using System.Runtime.InteropServices;
using UnityEngine;
internal static class NativeWaterInterop
{
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] unsafe delegate int NormalsFunction(Vector3* v,int nv,Vector3* n,int nn,int* tri,int nt,int mode);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int BindPerlinFunction(IntPtr method);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate float PerlinFunction(float x,float y);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] unsafe delegate int WaterFunction(Vector3* v,int nv,Vector3* n,int nn,int* tri,int nt,float cursor,float height,int mode);
 [DllImport("/usr/lib/libSystem.B.dylib", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr dlopen(string path,int flags);
 [DllImport("/usr/lib/libSystem.B.dylib", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr dlsym(IntPtr library,string symbol);
 [DllImport("/usr/lib/libSystem.B.dylib", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr dlerror();
 static IntPtr library;
 static NormalsFunction ftk_normals;
 static BindPerlinFunction ftk_bind_perlin;
 static PerlinFunction ftk_perlin;
 static WaterFunction ftk_water;
 static bool normalReady, waterReady;
 internal static bool Ready { get { return normalReady && perlinReady && waterReady && Error == null; } }
 internal static string LibraryPath { get; private set; }
 internal static void Load(string path)
 {
  if(library != IntPtr.Zero) return;
  // Only load the developer-built companion beside this managed plugin, after its isolation gate.
  if(!System.IO.File.Exists(path) || (System.IO.File.GetAttributes(path)&System.IO.FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("Native companion is missing or is a symlink.");
  library=dlopen(path,2); // RTLD_NOW; exact path avoids process-global search for a similarly named library.
  if(library==IntPtr.Zero) throw new InvalidOperationException("Native x86_64 library load failed: "+Marshal.PtrToStringAnsi(dlerror()));
  ftk_normals=(NormalsFunction)Bind("ftk_normals",typeof(NormalsFunction));
  ftk_bind_perlin=(BindPerlinFunction)Bind("ftk_bind_perlin",typeof(BindPerlinFunction));
  ftk_perlin=(PerlinFunction)Bind("ftk_perlin",typeof(PerlinFunction));
  ftk_water=(WaterFunction)Bind("ftk_water",typeof(WaterFunction));
  LibraryPath=path;
 }
 static Delegate Bind(string symbol,Type signature)
 {
  IntPtr function=dlsym(library,symbol);
  if(function==IntPtr.Zero) throw new MissingMethodException("Missing native export: "+symbol);
  return Marshal.GetDelegateForFunctionPointer(function,signature);
 }
 // Keep the handle loaded for the process lifetime: Unity's internal-call pointer must not outlive its delegate target.
 static readonly bool AbiValid=Marshal.SizeOf(typeof(Vector3))==12 && Marshal.OffsetOf(typeof(Vector3),"x").ToInt32()==0 && Marshal.OffsetOf(typeof(Vector3),"y").ToInt32()==4 && Marshal.OffsetOf(typeof(Vector3),"z").ToInt32()==8;
 internal static int Mode=3;
 internal static long Calls, Rejected;
 internal static string Error;
 internal static unsafe bool Compute(Vector3[] v,Vector3[] n,int[] tri)
 {
  if(Error!=null || v==null || n==null || tri==null || v.Length==0 || n.Length!=v.Length || tri.Length==0 || ReferenceEquals(v,n)) {Rejected++;return false;}
  try {
   if(!AbiValid) throw new InvalidOperationException("Unsupported Vector3 ABI");
   fixed(Vector3* pv=&v[0]) fixed(Vector3* pn=&n[0]) fixed(int* pt=&tri[0]) {
    if(ftk_normals(pv,v.Length,pn,n.Length,pt,tri.Length,Mode)!=1){Rejected++;return false;}
   }
   Calls++;return true;
  } catch(Exception e){Error=e.GetType().Name+": "+e.Message;Rejected++;return false;}
 }

 [StructLayout(LayoutKind.Explicit)] struct FloatBits { [FieldOffset(0)] public float Value; [FieldOffset(0)] public int Bits; }
 static int Bits(float f){FloatBits b=new FloatBits();b.Value=f;return b.Bits;}
 static void Managed(Vector3[] v,Vector3[] n,int[] t){
  for(int i=0;i<t.Length;i+=3){
   Vector3 a=v[t[i]],b=v[t[i+1]],c=v[t[i+2]];
   Vector3 e0=b-a,e1=c-b,e2=a-c;
   Vector3 na=Vector3.Cross(e1,e0).normalized*-1f,nb=Vector3.Cross(e2,e1).normalized*-1f,nc=Vector3.Cross(e0,e2).normalized*-1f;
   n[t[i]]=na;n[t[i+1]]=nb;n[t[i+2]]=nc;
  }
 }
 internal static System.Collections.Generic.Dictionary<string,object> SelfTestOne(){
  var random=new System.Random(91731);long differences=0,finiteDifferences=0,nanDifferences=0,checks=0;int rejected=0;double max=0;
  float[] scales={1f,0.00001f,1e-15f,1e10f,1e20f,0f};
  for(int test=0;test<2400;test++){
   var v=new Vector3[5];var actual=new Vector3[5];var expected=new Vector3[5];
   for(int j=0;j<v.Length;j++) {float k=scales[test%scales.Length];v[j]=new Vector3((float)(random.NextDouble()*2-1)*k,(float)(random.NextDouble()*2-1)*k,(float)(random.NextDouble()*2-1)*k);actual[j]=expected[j]=new Vector3(123f,456f,789f);}
   if(test%31==0)v[1]=v[0];
   if(test%47==0)v[0].x=float.NaN;
   if(test%53==0)v[1].y=float.PositiveInfinity;
   int[] t=test%2==0?new[]{0,1,2,2,1,3}:new[]{0,0,2,2,1,3};
   Managed(v,expected,t);if(!Compute(v,actual,t)){rejected++;continue;}
   for(int j=0;j<v.Length;j++)foreach(int c in new[]{0,1,2}){
    float a=c==0?actual[j].x:c==1?actual[j].y:actual[j].z;
    float e=c==0?expected[j].x:c==1?expected[j].y:expected[j].z;
    checks++;if(Bits(a)!=Bits(e)) {differences++;if(float.IsNaN(a)&&float.IsNaN(e))nanDifferences++;else finiteDifferences++;}
    if(!float.IsNaN(a)&&!float.IsInfinity(a)&&!float.IsNaN(e)&&!float.IsInfinity(e)) max=Math.Max(max,Math.Abs((double)a-e));
   }
  }
  var vertices=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,0,1)};
  var normals=new[]{new Vector3(99,88,77),new Vector3(99,88,77),new Vector3(99,88,77)};
  bool invalidRejected=!Compute(vertices,normals,new[]{0,1,4})&&normals[0].x==99;
  return new System.Collections.Generic.Dictionary<string,object>{{"componentChecks",checks},{"bitMismatches",differences},{"finiteOrClassMismatches",finiteDifferences},{"nanPayloadMismatches",nanDifferences},{"mode",Mode},{"maxFiniteError",max},{"rejectedValidCases",rejected},{"invalidTopologyRejectedWithoutWrites",invalidRejected},{"error",Error}};
 }
 internal static System.Collections.Generic.Dictionary<string,object> SelfTest(){
 normalReady=false; perlinReady=false; waterReady=false;
 if(library==IntPtr.Zero || ftk_normals==null) throw new InvalidOperationException("Native companion not loaded.");
 var modes=new System.Collections.Generic.List<object>();int selected=-1;
 for(int m=0;m<4;m++){Mode=m;var result=SelfTestOne();modes.Add(result);if((long)result["bitMismatches"]==0 && (int)result["rejectedValidCases"]==0 && (bool)result["invalidTopologyRejectedWithoutWrites"] && result["error"]==null)selected=m;}
 Mode=selected<0?3:selected;
 normalReady=selected>=0 && Error==null;
 return new System.Collections.Generic.Dictionary<string,object>{{"variants",modes},{"selectedExactMode",selected},{"error",Error}};
 }

 static bool perlinReady;
 internal static long WaterCalls;
 internal static System.Collections.Generic.Dictionary<string,object> PerlinSelfTest(){
  var m=typeof(Mathf).GetMethod("PerlinNoise",new[]{typeof(float),typeof(float)});
  if(m==null || !m.IsStatic || m.ReturnType!=typeof(float) || (m.GetMethodImplementationFlags()&System.Reflection.MethodImplAttributes.InternalCall)==0)throw new InvalidOperationException("Unsupported Perlin signature");
  int bound=ftk_bind_perlin(m.MethodHandle.Value);long checks=0,differences=0;
  if(bound==1){
   var r=new System.Random(711);
   float[] values={0f,-0f,1f,-1f,0.00001f,10f,-10f,100000f,-100000f};
   foreach(float x in values)foreach(float y in values){checks++;if(Bits(Mathf.PerlinNoise(x,y))!=Bits(ftk_perlin(x,y)))differences++;}
   for(int i=0;i<10000;i++){float x=(float)(r.NextDouble()*2000-1000),y=(float)(r.NextDouble()*2000-1000);checks++;if(Bits(Mathf.PerlinNoise(x,y))!=Bits(ftk_perlin(x,y)))differences++;}
  }
  perlinReady=bound==1&&differences==0;
  return new System.Collections.Generic.Dictionary<string,object>{{"bound",bound},{"checks",checks},{"bitMismatches",differences},{"ready",perlinReady}};
 }
 internal static System.Collections.Generic.Dictionary<string,object> WaterSelfTest(){
  waterReady=false; long checks=0,differences=0; int rejected=0; bool noWrite=true;
  if(!normalReady || !perlinReady) return new System.Collections.Generic.Dictionary<string,object>{{"ready",false},{"reason","Normal and Perlin gates must pass first."}};
  var random=new System.Random(31579);
  for(int test=0;test<256;test++){
   var v=new Vector3[5];var n=new Vector3[5];
   float scale=test%3==0?0.00001f:test%3==1?1f:1000f;
   for(int i=0;i<5;i++){v[i]=new Vector3((float)(random.NextDouble()*2-1)*scale,123f,(float)(random.NextDouble()*2-1)*scale);n[i]=new Vector3(17f,29f,37f);}
   if(test%7==0)v[1]=v[0];
   int[] t=test%2==0?new[]{0,1,2,2,1,3}:new[]{0,0,2,2,1,3};
   float cursor=test%5==0?0f:(float)(random.NextDouble()*20-10),height=test%11==0?0f:(float)(random.NextDouble()*8-4);
   var ev=(Vector3[])v.Clone();var en=(Vector3[])n.Clone();
   for(int i=0;i<ev.Length;i++){Vector3 x=ev[i];x.y=Mathf.PerlinNoise(x.x*cursor,x.z*cursor)*height;ev[i]=x;}
   Managed(ev,en,t);
   if(!ComputeWaterUnchecked(v,n,t,cursor,height)){rejected++;continue;}
   Compare(v,ev,ref checks,ref differences);Compare(n,en,ref checks,ref differences);
  }
  for(int test=0;test<6;test++){
   var v=new[]{new Vector3(0,9,0),new Vector3(1,9,0),new Vector3(0,9,1)};
   var n=new[]{new Vector3(11,12,13),new Vector3(11,12,13),new Vector3(11,12,13)};
   int[] t=test==0?new[]{0,1,4}:test==1?new[]{0,1}:new[]{0,1,2};
   float cursor=test==2?float.NaN:test==3?float.PositiveInfinity:1f,height=test==4?float.NaN:1f;
   if(test==5)v[0].x=float.NaN;
   var beforeV=(Vector3[])v.Clone();var beforeN=(Vector3[])n.Clone();
   long localChecks=0,localDifferences=0;
   if(ComputeWaterUnchecked(v,n,t,cursor,height))noWrite=false;
   Compare(v,beforeV,ref localChecks,ref localDifferences);Compare(n,beforeN,ref localChecks,ref localDifferences);
   if(localDifferences!=0)noWrite=false;
  }
  waterReady=checks==7680&&differences==0&&rejected==0&&noWrite&&Error==null;
  return new System.Collections.Generic.Dictionary<string,object>{{"componentChecks",checks},{"bitMismatches",differences},{"rejectedValidCases",rejected},{"invalidInputsRejectedWithoutWrites",noWrite},{"ready",waterReady}};
 }
 static void Compare(Vector3[] actual,Vector3[] expected,ref long checks,ref long differences){
  for(int i=0;i<actual.Length;i++){checks+=3;if(Bits(actual[i].x)!=Bits(expected[i].x))differences++;if(Bits(actual[i].y)!=Bits(expected[i].y))differences++;if(Bits(actual[i].z)!=Bits(expected[i].z))differences++;}
 }
 internal static bool ComputeWater(Vector3[] v,Vector3[] n,int[] t,float cursor,float height){
  return Ready && ComputeWaterUnchecked(v,n,t,cursor,height);
 }
 static unsafe bool ComputeWaterUnchecked(Vector3[] v,Vector3[] n,int[] t,float cursor,float height){
  if(!normalReady || !perlinReady || !AbiValid || Error!=null || v==null || n==null || t==null || v.Length==0 || n.Length!=v.Length || t.Length==0 || ReferenceEquals(v,n)) return false;
  try{
   fixed(Vector3* pv=&v[0])fixed(Vector3* pn=&n[0])fixed(int* pt=&t[0]){
    if(ftk_water(pv,v.Length,pn,n.Length,pt,t.Length,cursor,height,Mode)!=1)return false;
   }
   WaterCalls++;return true;
  }catch(Exception e){Error=e.GetType().Name+": "+e.Message;return false;}
 }
}
