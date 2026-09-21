using System;
using FTKModFramework.Core;

class Row {public int Value;}
class Program
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
    static void Reject(Action action,string name){try{action();}catch(InvalidOperationException){checks++;return;}throw new Exception(name);}
    static Row[] Vanilla(int count){Row[] rows=new Row[count];for(int i=0;i<count;i++)rows[i]=new Row{Value=i};return rows;}
    static void Main()
    {
        var ledger=new RegisteredRowRestoration();
        var paladin=new Row{Value=50};var second=new Row{Value=75};
        ledger.Record(14,paladin);ledger.Record(15,second);
        Row[] original=Vanilla(14);
        Row[] initial=(Row[])ledger.Restore(original);
        Check(initial.Length==16 && object.ReferenceEquals(initial[14],paladin),"class index preserved");
        Check(object.ReferenceEquals(initial[15],second),"multiple class order preserved");
        Check(original.Length==14,"input array not mutated");
        Check(object.ReferenceEquals(initial[1],original[1]),"vanilla row reference unchanged");
        Check(object.ReferenceEquals(initial,ledger.Restore(initial)),"same table repeated restore is idempotent");
        paladin.Value=80;
        Row[] recreated=Vanilla(14);
        Row[] restored=(Row[])ledger.Restore(recreated);
        Check(object.ReferenceEquals(restored[14],paladin) && restored[14].Value==80,"post-registration authoring and capability identity survive recreation");
        Check(object.ReferenceEquals(restored[0],recreated[0]) && !object.ReferenceEquals(restored[0],original[0]),"new native rows retained instead of stale table rows");
        Row[] third=(Row[])ledger.Restore(Vanilla(14));
        Check(third.Length==16 && object.ReferenceEquals(third[15],second),"second scene recreation retains exact positions");
        Row[] partial=new Row[15];Array.Copy(restored,partial,15);
        Check(((Row[])ledger.Restore(partial)).Length==16,"existing custom prefix restored without duplicates");
        Row[] conflict=Vanilla(16);Row collision=conflict[14];
        Reject(()=>ledger.Restore(conflict),"foreign position must fail");
        Check(object.ReferenceEquals(conflict[14],collision),"conflict leaves original untouched");
        Reject(()=>ledger.Restore(Vanilla(13)),"missing native row is not padded");
        Reject(()=>ledger.Record(14,new Row()),"registration cannot replace retained identity");
        var items=new RegisteredRowRestoration();var item=new Row();items.Record(120,item);
        Check(object.ReferenceEquals(((Row[])items.Restore(Vanilla(120)))[120],item),"non-class table restoration uses same suffix rule");
        var empty=new RegisteredRowRestoration();Check(object.ReferenceEquals(original,empty.Restore(original)),"no content leaves vanilla untouched");
        Reject(()=>ledger.Restore(new string[14]),"wrong row type rejected before allocation");
        Console.WriteLine(checks+" registered-row lifecycle checks passed");
    }
}
