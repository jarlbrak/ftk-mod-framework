using System;
class Program
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Main()
    {
        var state=new PortraitTraceState<string,string>();
        var outer=state.Begin(1,"watched-row");
        Check(state.Current(1)=="watched-row","outer identity");
        var unresolved=state.Begin(1,null);
        Check(state.Current(1)==null,"unresolved nested scope clears outer identity");
        state.End(unresolved);Check(state.Current(1)=="watched-row","nested restore");
        var other=state.Begin(2,"other-row");Check(state.Current(1)=="watched-row"&&state.Current(2)=="other-row","camera scopes isolated");
        state.End(other);Check(state.Current(2)==null,"other camera released");
        var inner=state.Begin(1,"verified-live-dummy");
        try{throw new InvalidOperationException("telemetry failure");}catch(InvalidOperationException){}finally{state.End(inner);}
        Check(state.Current(1)=="watched-row","exception cleanup restores outer");
        state.End(outer);Check(state.Current(1)==null,"outer released");
        state.End(null);Check(state.Current(1)==null,"absent token harmless");
        for(int i=0;i<8;i++)Check(state.Add(i.ToString()),"record admitted");
        Check(!state.Add("overflow")&&state.Count==8,"hard eight record bound");
        string[] copy=state.Read();copy[0]="changed";Check(state.Read()[0]=="0","read array cannot replace stored record");
        Check(string.Join(",",state.Read())=="0,1,2,3,4,5,6,7","oldest evidence retained at saturation");
        state.Begin(1,"stale");state.Clear();Check(state.Count==0&&state.Current(1)==null,"clear removes scopes and records");
        state.Begin(1,"previous");var interrupted=state.Begin(1,"interrupted");state.Clear();state.End(interrupted);
        Check(state.Current(1)==null,"stale finalizer cannot restore cleared Unity scope references");
        Console.WriteLine(checks+" linked portrait scope/buffer checks passed");
    }
}
