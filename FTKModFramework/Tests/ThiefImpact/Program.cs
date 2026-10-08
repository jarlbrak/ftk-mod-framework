using System;
using System.Reflection;
using FTKModFramework.Core;
class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static MethodInfo Prefix=typeof(ThiefImpactPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static),Postfix=typeof(ThiefImpactPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static);
 static ThiefImpactState Capture(CharacterDummy victim){object[] args={victim,null};Prefix.Invoke(null,args);return (ThiefImpactState)args[1];}
 static void Complete(CharacterDummy victim,ThiefImpactState state){Postfix.Invoke(null,new object[]{victim,state});}
 static void Main(){
  var hero=new CharacterDummy{Health=87,ThrowHealth=true,ThrowEquality=true,Response="HarmlessAttack",Attacker="self"};
  var skipped=Capture(hero);Check(!skipped.Captured&&hero.HealthReads==0,"self hero skips Unity equality and health");Complete(hero,skipped);Check(ThiefRuntime.Calls==0,"self hero skips impact");
  hero.Response="Damaged";hero.Attacker="enemy";Complete(hero,Capture(hero));Check(hero.HealthReads==0&&ThiefRuntime.Calls==0,"incoming hero skips impact");
  var friendly=new CharacterDummy{ThrowHealth=true,ThrowEquality=true};Complete(friendly,Capture(friendly));Check(ThiefRuntime.Calls==0,"friendly skips impact");
  Complete(null,Capture(null));Check(ThiefRuntime.Calls==0,"CLR null skips impact");
  var enemy=new EnemyDummy{Health=22,Attacker="Hunter",Response="Damaged"};var captured=Capture(enemy);Check(captured.Captured&&captured.PreviousHealth==22&&enemy.HealthReads==1,"any-player enemy captures exact prehealth");enemy.Health=0;Complete(enemy,captured);Check(ThiefRuntime.Calls==1&&ThiefRuntime.Previous==22&&object.ReferenceEquals(ThiefRuntime.Victim,enemy),"fatal forwards exact native victim and prehealth");
  enemy.Response="Dodge";Complete(enemy,Capture(enemy));Check(ThiefRuntime.Calls==2,"enemy miss cleanup route remains");
  enemy.Response="HarmlessAttack";Complete(enemy,Capture(enemy));Check(ThiefRuntime.Calls==3,"enemy harmless cleanup route remains");
  var dead=new EnemyDummy{Destroyed=true,ThrowHealth=true};captured=Capture(dead);Check(captured.Captured&&captured.PreviousHealth==0&&dead.HealthReads==0,"destroyed enemy keeps Unity null semantics");
  Complete(enemy,new ThiefImpactState());Check(ThiefRuntime.Calls==3,"uncaptured state cannot invoke impact");
  enemy.ThrowHealth=true;bool threw=false;try{Capture(enemy);}catch(TargetInvocationException e){threw=e.InnerException is InvalidOperationException;}Check(threw,"enemy getter exception is not swallowed");
  ThiefRuntime.Throw=true;Complete(enemy,new ThiefImpactState{Captured=true,PreviousHealth=9});Check(Plugin.Log.Errors==1,"existing impact exception logging retained");
  Console.WriteLine("PASS: "+checks+" actual Thief impact routing checks");
 }
}
