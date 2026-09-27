// Developer-only macOS x86_64 kernel. Build without floating-point reassociation or contraction.
#if !defined(FTK_WATER_KERNEL_TEST) && (!defined(__APPLE__) || !defined(__x86_64__))
#error "This experiment supports only the macOS x86_64 Unity player."
#endif
#include <cmath>
#include <cstdint>
struct V {float x,y,z;};
static_assert(sizeof(V)==12,"float3 ABI");
static V sub(V a,V b){return {a.x-b.x,a.y-b.y,a.z-b.z};}
static V normal(V a,V b,int mode){
 V c={a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x};
 if(mode&1)c={float(double(a.y)*b.z-double(a.z)*b.y),float(double(a.z)*b.x-double(a.x)*b.z),float(double(a.x)*b.y-double(a.y)*b.x)};
 float xx=c.x*c.x,yy=c.y*c.y,zz=c.z*c.z;
 float sum=xx+yy;sum=sum+zz;
 if(mode&2)sum=float((double(c.x)*c.x+double(c.y)*c.y)+double(c.z)*c.z);
 float length=static_cast<float>(std::sqrt(static_cast<double>(sum)));
 if(length>1.0e-5f){c.x=c.x/length;c.y=c.y/length;c.z=c.z/length;}
 else c={0.0f,0.0f,0.0f};
 return {c.x*-1.0f,c.y*-1.0f,c.z*-1.0f};
}
extern "C" __attribute__((visibility("default"))) int ftk_normals(const V* vertices,int vertex_count,V* normals,int normal_count,const int32_t* triangles,int index_count,int mode){
 if(!vertices || !normals || !triangles || vertices==normals || vertex_count<1 || normal_count!=vertex_count || index_count<0 || index_count%3) return 0;
 for(int i=0;i<index_count;i++) if(triangles[i]<0 || triangles[i]>=vertex_count) return 0;
 for(int i=0;i<index_count;i+=3){
  int ai=triangles[i],bi=triangles[i+1],ci=triangles[i+2];
  V a=vertices[ai],b=vertices[bi],c=vertices[ci];
  V e0=sub(b,a),e1=sub(c,b),e2=sub(a,c);
  V na=normal(e1,e0,mode),nb=normal(e2,e1,mode),nc=normal(e0,e2,mode);
  normals[ai]=na;normals[bi]=nb;normals[ci]=nc;
 }
 return 1;
}
#include <dlfcn.h>
using Perlin = float (*)(float,float);
static Perlin native_perlin=nullptr;
extern "C" __attribute__((visibility("default"))) int ftk_bind_perlin(void* method){
 using Lookup=void* (*)(void*);
 auto lookup=reinterpret_cast<Lookup>(dlsym(RTLD_DEFAULT,"mono_lookup_internal_call"));
 if(!lookup||!method)return 0;
 native_perlin=reinterpret_cast<Perlin>(lookup(method));
 return native_perlin?1:0;
}
extern "C" __attribute__((visibility("default"))) float ftk_perlin(float x,float y){return native_perlin?native_perlin(x,y):NAN;}
extern "C" __attribute__((visibility("default"))) int ftk_water(V* vertices,int vertex_count,V* normals,int normal_count,const int32_t* triangles,int index_count,float cursor,float height,int mode){
 if(!native_perlin || !vertices || !normals || !triangles || vertices==normals || vertex_count<1 || normal_count!=vertex_count || index_count<0 || index_count%3 || !std::isfinite(cursor) || !std::isfinite(height)) return 0;
 for(int i=0;i<index_count;i++)if(triangles[i]<0||triangles[i]>=vertex_count)return 0;
 for(int i=0;i<vertex_count;i++)if(!std::isfinite(vertices[i].x)||!std::isfinite(vertices[i].z)||!std::isfinite(vertices[i].x*cursor)||!std::isfinite(vertices[i].z*cursor))return 0;
 for(int i=0;i<vertex_count;i++){
  float x=vertices[i].x*cursor,z=vertices[i].z*cursor;
  vertices[i].y=native_perlin(x,z)*height;
 }
 return ftk_normals(vertices,vertex_count,normals,normal_count,triangles,index_count,mode);
}
