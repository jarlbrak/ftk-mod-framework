using System;
using System.Collections.Generic;
using FTKModFramework.Core.HotReload;
class Row {internal int Id;}
class Program {
 static void Check(bool b){if(!b)throw new Exception("assertion");}
 static void Reject(Action a){try{a();}catch(InvalidOperationException){return;}throw new Exception("expected rejection");}
 static void Main(){var rows=new[]{new Row{Id=1},new Row{Id=2},new Row{Id=3}};int calls=0;
 var required=new HashSet<int>{2,3};var index=PreflightRowIndex.Build(rows,required,r=>{calls++;return r.Id;});
 Check(calls==3 && object.ReferenceEquals(index[2],rows[1]) && object.ReferenceEquals(index[3],rows[2]));
 for(int i=0;i<100;i++)Check(object.ReferenceEquals(index[2],rows[1]));Check(calls==3);
 Reject(()=>PreflightRowIndex.Build(new[]{rows[1],rows[1]},new HashSet<int>{2},r=>r.Id));
 Reject(()=>PreflightRowIndex.Build(rows,new HashSet<int>{9},r=>r.Id));
 Check(PreflightRowIndex.Build(new[]{rows[0],rows[0],rows[1]},new HashSet<int>{2},r=>r.Id).Count==1);
 Check(PreflightRowIndex.Build(rows,new HashSet<int>(),r=>{throw new Exception("empty selection inspected rows");}).Count==0);
 Console.WriteLine("Preflight row index: one identity lookup per row, exact references, requested duplicate/missing rejection, unrelated aliases and empty selection passed.");}
}
