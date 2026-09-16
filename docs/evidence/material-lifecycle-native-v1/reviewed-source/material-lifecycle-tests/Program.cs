using System;
class Program
{
    static int assertions;
    static void Check(bool pass){assertions++;if(!pass)throw new Exception("Material observation timing assertion failed.");}
    static void Reject(Action call){try{call();}catch(ArgumentException){assertions++;return;}throw new Exception("Invalid native timing accepted.");}
    static void Main()
    {
        // Different native frame durations and positive/negative rates; no fixed-framerate assumption.
        Check(MaterialLifecycleTiming.Error(10,11,.25f,1,2,-2,4,.5f,3)==0);
        Check(MaterialLifecycleTiming.Error(11,12,.125f,.5f,3,-2,4,.25f,3.5f)==0);
        // One missing callback and two callbacks both fail the unchanged 1e-5 acceptance boundary.
        Check(MaterialLifecycleTiming.Error(1,2,.02f,0,0,0,.2f,0,0)>MaterialLifecycleTiming.Tolerance);
        Check(MaterialLifecycleTiming.Error(1,2,.02f,0,0,0,.2f,0,.008f)>MaterialLifecycleTiming.Tolerance);
        // Disabled renderer still requires native phase progression; material offset is deliberately not this predicate's input.
        Check(MaterialLifecycleTiming.Error(1,2,.02f,0,0,0,.2f,0,.004f)<=MaterialLifecycleTiming.Tolerance);
        Reject(()=>MaterialLifecycleTiming.Error(1,3,.02f,0,0,0,.2f,0,.008f));
        Reject(()=>MaterialLifecycleTiming.Error(1,1,.02f,0,0,0,.2f,0,.004f));
        Reject(()=>MaterialLifecycleTiming.Error(int.MaxValue,int.MinValue,.02f,0,0,0,.2f,0,.004f));
        foreach(float bad in new[]{0f,-.01f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})Reject(()=>MaterialLifecycleTiming.Error(1,2,bad,0,0,0,.2f,0,.004f));
        Reject(()=>MaterialLifecycleTiming.Error(1,2,.02f,float.NaN,0,0,.2f,0,.004f));
        Reject(()=>MaterialLifecycleTiming.Error(1,2,.02f,0,0,0,float.PositiveInfinity,0,.004f));
        Reject(()=>MaterialLifecycleTiming.Error(1,2,.02f,0,0,0,.2f,0,float.NaN));
        Reject(()=>MaterialLifecycleTiming.Error(1,2,2,0,0,0,float.MaxValue,0,0));
        Console.WriteLine(assertions+" linked timing assertions PASS; no Unity scheduling or resource lifetime claim.");
    }
}
