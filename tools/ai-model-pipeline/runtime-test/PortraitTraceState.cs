using System;
using System.Collections.Generic;

// Bounded scalar-record storage and nested-scope bookkeeping; no Unity dependency.
internal sealed class PortraitTraceState<TScope,TRecord> where TScope:class
{
    internal sealed class Token { internal int camera,generation;internal TScope previous;internal bool hadPrevious; }
    readonly Dictionary<int,TScope> active=new Dictionary<int,TScope>();
    readonly List<TRecord> records=new List<TRecord>();
    int generation;
    public const int Capacity=8;
    public int Count {get{return records.Count;}}
    public Token Begin(int camera,TScope scope)
    {
        TScope previous;bool had=active.TryGetValue(camera,out previous);
        active[camera]=scope;return new Token{camera=camera,generation=generation,previous=previous,hadPrevious=had};
    }
    public TScope Current(int camera){TScope value;return active.TryGetValue(camera,out value)?value:null;}
    public void End(Token token){if(token==null || token.generation!=generation)return;if(token.hadPrevious)active[token.camera]=token.previous;else active.Remove(token.camera);}
    public bool Add(TRecord record){if(records.Count>=Capacity)return false;records.Add(record);return true;}
    public TRecord[] Read(){return records.ToArray();}
    public void Clear(){generation++;active.Clear();records.Clear();}
}
