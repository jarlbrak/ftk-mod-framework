using System;
using System.Collections;
using FTKModFramework.Agent;
class Program
{
 class Enemy { internal int Health; }
 static int count;
 static void Check(bool expected, bool actual, string name) { count++; if(expected != actual) throw new Exception(name); }
 static void Main() {
  var enemy=new Enemy { Health=10 }; object hero=new object(); IDictionary enemies=new Hashtable{{"enemy",enemy}}, dummies=new Hashtable{{"enemy",enemy},{"hero",hero}};
  Check(true,CombatObservation.IsActive(true,false,null,null,null,null),"normal client flag preserved");
  Check(true,CombatObservation.IsActive(false,true,"Enemy",enemies,dummies,"hero"),"revealed hero turn");
  Check(true,CombatObservation.IsActive(false,true,"Enemy",enemies,dummies,"enemy"),"revealed enemy turn");
  Check(false,CombatObservation.IsActive(false,false,"Enemy",enemies,dummies,"hero"),"ended master");
  foreach(string type in new[]{"Ready","UnlockedChestMimic",null}) Check(false,CombatObservation.IsActive(false,true,type,enemies,dummies,"hero"),"unrevealed type");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",new Hashtable(),dummies,"hero"),"empty enemies");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",enemies,null,"hero"),"absent dummies");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",enemies,dummies,null),"empty timeline");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",enemies,dummies,"missing"),"unresolved head");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",enemies,new Hashtable{{"hero",hero}},"hero"),"missing enemy entry");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",enemies,new Hashtable{{"hero",hero},{"enemy",new object()}},"hero"),"nonreciprocal enemy");
  enemy.Health=0;
  Check(true,CombatObservation.IsActive(false,true,"Enemy",enemies,dummies,"hero"),"dead-enemy reward population retained");
  Check(false,CombatObservation.IsActive(false,true,"Enemy",enemies,new Hashtable{{"hero",null},{"enemy",enemy}},"hero"),"null timeline dummy");
  Console.WriteLine(count+" combat observation checks PASS");
 }
}
