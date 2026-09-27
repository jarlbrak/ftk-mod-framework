namespace FTKModFramework.Core
{
    // The Tweaks types live in FTKModFramework.Core rather than a Core.Tweaks namespace so that
    // patches anywhere under Core can call Tweaks.IsOn without the namespace shadowing the class.
    // Keep this folder free of UnityEngine, Plugin and game types so the game-free Tests/Tweaks
    // harness can compile it directly.

    /// <summary>Display grouping and default policy. Fix tweaks default on; the others are opt-in.</summary>
    internal enum TweakCategory
    {
        Fix,
        Information,
        Convenience,
    }

    /// <summary>Local tweaks affect only this player. Session tweaks are shared rules for a run and
    /// are captured at run setup, then locked at run start.</summary>
    internal enum TweakScope
    {
        Local,
        Session,
    }

    /// <summary>The stored player choice. Default follows the descriptor's current DefaultOn, so a
    /// changed default reaches players who never touched the row.</summary>
    internal enum TweakPreference
    {
        Default,
        On,
        Off,
    }

    /// <summary>Immutable description of one framework-owned tweak.</summary>
    internal sealed class TweakDescriptor
    {
        /// <param name="defaultOn">Null takes the category default. A Fix that carries a balance
        /// note must pass a value, because shipping a balance change on by default is a decision
        /// the author has to make visibly rather than inherit.</param>
        internal TweakDescriptor(string id, TweakCategory category, TweakScope scope, string title,
            string summary, string evidence, string balanceNote = null, bool? defaultOn = null)
        {
            Id = id;
            Category = category;
            Scope = scope;
            Title = title;
            Summary = summary;
            Evidence = evidence;
            BalanceNote = balanceNote;
            ExplicitDefault = defaultOn;
        }

        /// <summary>Stable config key. Never changes once shipped, or players lose their choice.</summary>
        internal string Id { get; }
        internal TweakCategory Category { get; }
        internal TweakScope Scope { get; }
        internal string Title { get; }
        internal string Summary { get; }
        /// <summary>Maintainer reference to the assembly method and any public report.</summary>
        internal string Evidence { get; }
        internal string BalanceNote { get; }
        /// <summary>The author's explicit default, or null when the category default applies.</summary>
        internal bool? ExplicitDefault { get; }

        internal bool DefaultOn
        {
            get { return ExplicitDefault ?? Category == TweakCategory.Fix; }
        }
    }
}
