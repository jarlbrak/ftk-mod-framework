using System;
using Newtonsoft.Json.Linq;

internal sealed class LeaseObservationPin
{
    public readonly string root,session,commandId;
    readonly JObject core;
    public LeaseObservationPin(string root,string session,string commandId,JObject core)
    {if(string.IsNullOrEmpty(root)||string.IsNullOrEmpty(session)||string.IsNullOrEmpty(commandId)||core==null)throw new ArgumentException("Complete observation identity required.");this.root=root;this.session=session;this.commandId=commandId;this.core=(JObject)core.DeepClone();}
    public void Check(string currentRoot,string currentSession,JObject currentCore)
    {if(currentRoot!=root || currentSession!=session || !JToken.DeepEquals(core,currentCore))throw new InvalidOperationException("Watched lease observation identity changed; disposal cannot be inferred.");}
    public JObject View(){return new JObject{{"root",root},{"session",session},{"armCommandId",commandId},{"coreIdentity",core.DeepClone()}};}
    public static int ExactId(JObject command,string key,bool required)
    {
        JToken value=command[key];if(value==null && !required)return 0;
        if(value==null || value.Type!=JTokenType.Integer)throw new ArgumentException("Exact integer ID required: "+key);
        int id=(int)value;if(id==0)throw new ArgumentException("Nonzero ID required: "+key);return id;
    }
    public static void ValidateArm(int expectedOwner,int actualOwner,int expectedCel,int actualCel,int expectedLease,int actualLease,
        bool active,bool acquired,bool applied,bool present,bool alreadyWatched,int watchedCount,int existingResources,int resourceCount)
    {
        if(expectedOwner==0 || expectedCel==0 || expectedLease==0 || expectedOwner!=actualOwner || expectedCel!=actualCel || expectedLease!=actualLease
            || !active || !acquired || !applied || !present || alreadyWatched)
            throw new InvalidOperationException("Exact active acquired/applied native avatar and unwatched lease required.");
        if(watchedCount<0 || watchedCount>=8 || existingResources<0 || resourceCount<=0 || resourceCount>256 || existingResources>256-resourceCount)
            throw new InvalidOperationException("Lease observation limits8/256 exceeded.");
    }
}
