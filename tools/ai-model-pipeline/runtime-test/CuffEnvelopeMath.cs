using System;
using System.Collections.Generic;

// In-memory reduction only. Native surface points never become result fields.
internal sealed class CuffEnvelopeMath
{
    internal struct Point
    {
        internal double t, u, v, weight;
        internal Point(double axial, double radialU, double radialV, double armWeight)
        { t=axial; u=radialU; v=radialV; weight=armWeight; }
        internal static Point Lerp(Point a, Point b, double f)
        { return new Point(a.t+(b.t-a.t)*f,a.u+(b.u-a.u)*f,a.v+(b.v-a.v)*f,a.weight+(b.weight-a.weight)*f); }
    }
    internal sealed class Band
    {
        internal int triangles, rejectedRadialTriangles, supportSamples;
        internal readonly double[] min = { double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity };
        internal readonly double[] max = { double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity };
    }
    internal readonly Band[] bands = { new Band(),new Band(),new Band() };
    internal int inputTriangles, rejectedArmTriangles;
    const double Radius = 0.35, ArmWeight = 0.25;
    // Both candidates may pass only when their actual mapped points are equivalent.
    internal static bool UseScaleFreeBake(double fullMax, double rigidMax, double candidateGap)
    {
        Finite(fullMax); Finite(rigidMax); Finite(candidateGap);
        if(fullMax<0 || rigidMax<0 || candidateGap<0)throw new InvalidOperationException("Invalid bake residual.");
        bool full=fullMax<=0.002, rigid=rigidMax<=0.002;
        if(full && (!rigid || candidateGap<=0.001))return false;
        if(rigid && !full)return true;
        throw new InvalidOperationException("Bake mapping unverified or ambiguous.");
    }
    static void Finite(double value)
    { if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidOperationException("Nonfinite cuff input."); }
    static List<Point> Clip(List<Point> polygon, int axis, double edge, bool greater)
    {
        List<Point> result=new List<Point>();
        if(polygon.Count==0)return result;
        Point previous=polygon[polygon.Count-1];
        double prior=(axis==0?previous.t:previous.weight)-edge;
        bool previousInside=greater?prior>=0:prior<=0;
        foreach(Point current in polygon)
        {
            double distance=(axis==0?current.t:current.weight)-edge;
            bool inside=greater?distance>=0:distance<=0;
            if(inside!=previousInside)result.Add(Point.Lerp(previous,current,prior/(prior-distance)));
            if(inside)result.Add(current);
            previous=current;prior=distance;previousInside=inside;
        }
        return result;
    }
    internal void AddTriangle(Point a, Point b, Point c)
    {
        foreach(Point p in new[]{a,b,c}) {Finite(p.t);Finite(p.u);Finite(p.v);Finite(p.weight);}
        inputTriangles++;
        List<Point> arm=Clip(new List<Point>{a,b,c},1,ArmWeight,true);
        if(arm.Count<3){rejectedArmTriangles++;return;}
        for(int band=0;band<3;band++)
        {
            List<Point> clipped=Clip(Clip(arm,0,band/3.0,true),0,(band+1)/3.0,false);
            if(clipped.Count<3)continue;
            bool outside=false;
            foreach(Point p in clipped)if(p.u*p.u+p.v*p.v>Radius*Radius+1e-10)outside=true;
            if(outside){bands[band].rejectedRadialTriangles++;continue;}
            Band output=bands[band];output.triangles++;output.supportSamples+=clipped.Count;
            foreach(Point p in clipped)
            {
                double[] values={p.u,p.v,(p.u+p.v)/Math.Sqrt(2),(p.u-p.v)/Math.Sqrt(2)};
                for(int i=0;i<4;i++){output.min[i]=Math.Min(output.min[i],values[i]);output.max[i]=Math.Max(output.max[i],values[i]);}
            }
        }
    }
}
