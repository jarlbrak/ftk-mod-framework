using System;
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string method){}} }
public class CharacterDummy {
 public int Health, HealthReads; public bool Destroyed, ThrowEquality, ThrowHealth; public string Response, Attacker;
 public virtual int GetCurrentHealth(){HealthReads++;if(ThrowHealth)throw new InvalidOperationException("health");return Health;}
 public static bool operator ==(CharacterDummy a,CharacterDummy b){if(!object.ReferenceEquals(a,null)&&a.ThrowEquality)throw new InvalidOperationException("Unity equality");return object.ReferenceEquals(a,b)||(object.ReferenceEquals(b,null)&&a.Destroyed);}
 public static bool operator !=(CharacterDummy a,CharacterDummy b){return !(a==b);}
 public override bool Equals(object x){return object.ReferenceEquals(this,x);} public override int GetHashCode(){return base.GetHashCode();}
}
public class EnemyDummy:CharacterDummy{}
namespace FTKModFramework.Core {
 internal static class ThiefRuntime {internal static int Calls,Previous;internal static CharacterDummy Victim;internal static bool Throw;internal static void OnImpact(CharacterDummy victim,int previous){Calls++;Victim=victim;Previous=previous;if(Throw)throw new InvalidOperationException("impact");}}
 internal static class Plugin {internal static Logger Log=new Logger();} internal class Logger {internal int Errors;internal void LogError(string text){Errors++;}}
}
