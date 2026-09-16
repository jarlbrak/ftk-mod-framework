using System;
using System.Collections.Generic;
static class Program
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Main()
    {
        var shared=new HashSet<int>{12,-99,1000};
        foreach(int id in shared)Check(!RowPortraitOwnership.Fresh(id,204,172,shared),"Shared source/HUD/unknown/other native textures rejected");
        Check(RowPortraitOwnership.Fresh(-101,204,172,shared),"New native negative Unity ID accepted");
        Check(RowPortraitOwnership.Fresh(101,204,172,shared),"New native positive Unity ID accepted");
        Check(!RowPortraitOwnership.Fresh(0,204,172,shared),"Zero rejected");
        Check(!RowPortraitOwnership.Fresh(101,204,172,null),"Missing pre-call inventory rejected");
        Check(!RowPortraitOwnership.Fresh(101,204,172,new HashSet<int>()),"Empty/uninitialized pre-call inventory rejected");
        Check(!RowPortraitOwnership.Fresh(101,205,172,shared),"Unexpected width rejected");
        Check(!RowPortraitOwnership.Fresh(101,204,171,shared),"Unexpected height rejected");
        // Simulate validation failure before cleanup ownership assignment: a shared candidate is never destroyed.
        int? cleanupOwned=null;int candidate=12;
        if(RowPortraitOwnership.Fresh(candidate,204,172,shared))cleanupOwned=candidate;
        Check(cleanupOwned==null,"Rejected shared candidate remains outside cleanup ownership");
        Check(shared.SetEquals(new[]{12,-99,1000}),"Ownership checks do not mutate pre-call identity set");
        Check(RowPortraitOwnership.NativeDimension(82,4)==328 && RowPortraitOwnership.NativeDimension(70,4)==280,"Native row size from exact cast then AA");
        Check(RowPortraitOwnership.NativeDimension(82.9f,4)==328,"Native truncation precedes multiplication");
        Check(RowPortraitOwnership.Fresh(101,328,280,shared,328,280),"New exact row texture accepted");
        Check(!RowPortraitOwnership.Fresh(12,328,280,shared,328,280),"Shared row texture rejected");
        Check(!RowPortraitOwnership.Fresh(101,204,172,shared,328,280),"HUD dimensions cannot substitute for row");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,0f,-1f,1025f})
        {bool rejected=false;try{RowPortraitOwnership.NativeDimension(bad,4);}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid or overbound raw dimension rejected");}
        foreach(int bad in new[]{0,-1,9})
        {bool rejected=false;try{RowPortraitOwnership.NativeDimension(82,bad);}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid AA rejected");}
        bool oversized=false;try{RowPortraitOwnership.NativeDimension(1024,4);}catch(ArgumentException){oversized=true;}Check(oversized,"Final texture cap applied");
        Console.WriteLine("PASS "+checks+" linked ownership assertions; Unity lifetime remains a live fixture test.");
    }
}
