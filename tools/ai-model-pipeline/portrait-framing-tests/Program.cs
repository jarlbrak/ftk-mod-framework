using System;
using System.IO;
using FTKModFramework.Core;
static class Program
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Main()
    {
        double distance;
        // Actual custom GLB bounds reconstructed from captured five-bone matrices.
        double x=3.6373,y=(3.7726+5.3727)/2,z=(1.0977+5.2634)/2;
        Check(PortraitFraming.Distance(x,y,z,30,204.0/172,.3,100,out distance),"Large posed head fits perspective");
        double tan=Math.Tan(30*Math.PI/360);
        foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {double depth=distance+sz*z;Check(depth>.3&&depth<100&&Math.Abs(sx*x)/(depth*tan*204/172)<1&&Math.Abs(sy*y)/(depth*tan)<1,"Every corner within frustum");}
        Check(PortraitFraming.Distance(x,y,z,30,.5,.3,100,out double narrow)&&narrow>distance,"Narrow portrait aspect increases distance");
        Check(!PortraitFraming.Distance(x,y,z,20,1,.3,10,out distance),"Insufficient far plane fails without modifying camera");
        foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1,0})
            Check(!PortraitFraming.Distance(invalid,1,1,30,1,.3,100,out distance),"Invalid extent rejected");
        Check(!PortraitFraming.Distance(1,1,1,0,1,.3,100,out distance),"Invalid projection rejected");
        Check(!PortraitFraming.Distance(1,1,1,30,0,.3,100,out distance),"Invalid aspect rejected");
        Check(!PortraitFraming.Distance(1,1,1,30,1,100,100,out distance),"Invalid clip range rejected");
        Check(PortraitFraming.Fit(x,y,z,20,204,172,.3,30,out distance,out double scale)&&scale<1&&scale>=.1,"Actual native lens fits only bounded clone scaling");
        tan=Math.Tan(20*Math.PI/360);
        foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {double depth=distance+sz*z*scale;Check(depth>.3&&depth<30&&Math.Abs(sx*x*scale)/(depth*tan*204/172)<1&&Math.Abs(sy*y*scale)/(depth*tan)<1,"Scaled corners fit unchanged native frustum");}
        Check(PortraitFraming.Fit(x*1.7,y*1.7,z*1.7,20,204,172,.3,30,out distance,out double larger)&&larger<scale,"World extents account for larger original root without changing bone locals");
        Check(!PortraitFraming.Fit(x*100,y*100,z*100,20,204,172,.3,30,out distance,out scale),"Extreme geometry exceeds bounded shrink");
        Check(!PortraitFraming.Fit(x,y,z,20,0,172,.3,30,out distance,out scale),"Missing target dimensions fail closed");
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../FTKModFramework/Core"));
        string source=File.ReadAllText(Path.Combine(root,"LegacyKrakenPortrait.cs"));
        Check(source.Contains("target.width,target.height")&&!source.Contains("lens.aspect,"),"First capture uses RT aspect with no preassigned camera target");
        Check(source.Contains("resources.OwnsExplicitMesh(renderer)"),"Only owned explicit mesh eligible");
        Check(source.Contains("if(scaled&&!committed)clone.transform.localScale=originalScale"),"Failure rolls back clone scale");
        Check(source.Contains("Object.Destroy(baked)")&&source.Contains("Object.Destroy(created)"),"Temporary objects released on failure");
        Check(!source.Contains("source.transform.localScale=")&&!source.Contains("lens.fieldOfView=")&&!source.Contains("lens.farClipPlane="),"No live source or projection mutations");
        string lease=File.ReadAllText(Path.Combine(root,"ExplicitEnemyMeshSwap.cs"));
        int a=lease.IndexOf("internal bool OwnsExplicitMesh"),b=lease.IndexOf("internal bool Owns(Renderer",a);string gate=lease.Substring(a,b-a);
        Check(gate.Contains("VisualResourcesOnly")&&gate.Contains("resource==renderer.sharedMesh"),"Renderer membership alone cannot authorize native geometry");
        Console.WriteLine("PASS "+checks+" linked portrait framing checks.");
    }
}
