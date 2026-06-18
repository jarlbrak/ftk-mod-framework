using System;

namespace FTKModFramework.Behaviors
{
    /// <summary>
    /// Marks a class as a custom content behaviour the framework can discover and host. A modder applies
    /// this to a <c>ProficiencyBase</c> subclass and gives it a stable <see cref="Name"/>; the framework
    /// keys the type by (modGuid + ":" + Name) so it can be referenced from content data and instantiated
    /// at runtime.
    ///
    /// This is the ONLY framework type a modder's behaviour DLL needs to reference. It deliberately depends
    /// on nothing else in the framework (just <c>System</c>), so a content DLL can carry it without pulling
    /// in Core internals or the game assemblies at reference time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ContentBehaviorAttribute : Attribute
    {
        /// <summary>
        /// The behaviour's stable local name (the part after the mod guid in its registry key). Required.
        /// Pair it with the owning mod's guid to form the lookup key, e.g. "com.you.mymod" + ":" + "steal".
        /// </summary>
        public string Name { get; private set; }

        /// <summary>Declare a hostable behaviour under the given stable local <paramref name="name"/>.</summary>
        public ContentBehaviorAttribute(string name)
        {
            Name = name;
        }
    }
}
