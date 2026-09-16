using System;

// Pure scheduled-observation boundary, shared by the actual fixture and linked offline tests.
internal static class MaterialLifecycleTiming
{
    internal const float Tolerance=.00001f;
    static void Finite(float value){if(float.IsNaN(value) || float.IsInfinity(value))throw new ArgumentException("Finite native timing/phase required.");}
    internal static float Error(int priorFrame,int frame,float deltaTime,float priorX,float priorY,float rateX,float rateY,float x,float y)
    {
        if(priorFrame==int.MaxValue || frame!=priorFrame+1)throw new ArgumentException("Consecutive native frames required; unseen deltas cannot be inferred.");
        foreach(float value in new[]{deltaTime,priorX,priorY,rateX,rateY,x,y})Finite(value);
        if(deltaTime<=0)throw new ArgumentException("Native game time must advance.");
        float expectedX=priorX+rateX*deltaTime,expectedY=priorY+rateY*deltaTime;Finite(expectedX);Finite(expectedY);
        return Math.Max(Math.Abs(expectedX-x),Math.Abs(expectedY-y));
    }
}
