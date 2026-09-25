using System.Collections.Generic;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// The single flat content DTO (spec #6): one shape for every kind. There is deliberately NO
    /// per-kind subclass hierarchy. The <see cref="Kind"/> string selects which public
    /// <c>Content.Add*</c> helper the loader drives; <see cref="Template"/> names the vanilla row to
    /// clone (resolved to the kind's <c>.ID</c> enum); <see cref="Fields"/> holds raw member overrides.
    ///
    /// P1a only consumes the SCALAR path of <see cref="Fields"/>. Enum-by-name, content-id resolution,
    /// arrays/nested objects, the alias table, and <see cref="Proficiencies"/> attach are P1b/P1c (#8/#9);
    /// the DTO already carries them so those phases extend the loader without a DTO rewrite.
    /// </summary>
    internal sealed class ContentEntry
    {
        // Fields are populated by Newtonsoft via reflection, not by C# code; silence "never assigned".
#pragma warning disable CS0649
        [JsonProperty("kind")] public string Kind;
        [JsonProperty("id")] public string Id;
        [JsonProperty("template")] public string Template;
        [JsonProperty("displayName")] public string DisplayName;

        /// <summary>Raw member overrides: member name -&gt; JSON value (scalar in P1a).</summary>
        [JsonProperty("fields")] public Dictionary<string, object> Fields;

        /// <summary>Proficiency ids to attach in Phase 2 (weapon -&gt; AttachProficiencies, enemy -&gt; AttachEnemyProficiencies).</summary>
        [JsonProperty("proficiencies")] public string[] Proficiencies;
        [JsonProperty("replaceProficiencies")] public bool ReplaceProficiencies;

        /// <summary>Class flavor text -&gt; <c>Localization.SetClassFlavor</c> in Phase 2 (class kind).</summary>
        [JsonProperty("flavor")] public string Flavor;

        /// <summary>Tooltip/bestiary description -&gt; Localization in Phase 2 (proficiency / enemy kind).</summary>
        [JsonProperty("description")] public string Description;

        /// <summary>
        /// Bare behaviour name (the part after the owning mod guid in a <c>BehaviorRegistry</c> key); the
        /// loader prefixes the owning mod guid to form (modGuid + ":" + behavior) before resolving it.
        /// Meaningful ONLY on <c>kind:"proficiency"</c>: it wires the resolved <c>ProficiencyBase</c>
        /// subclass into the row's <c>m_ProficiencyPrefab</c> in Phase 2. Ignored (with a warning) on any
        /// other kind.
        /// </summary>
        [JsonProperty("behavior")] public string Behavior;

        /// <summary>
        /// Optional <c>ProficiencyBase.Category</c> enum NAME (case-insensitive, e.g. "StealGold") set on the
        /// hosted behaviour instance after it is created. Meaningful only alongside <see cref="Behavior"/> on
        /// a proficiency. An unknown name leaves the instance's default category untouched and warns.
        /// </summary>
        [JsonProperty("behaviorCategory")] public string BehaviorCategory;
        [JsonProperty("guardian")] public bool Guardian;
        [JsonProperty("opportunist")] public bool Opportunist;
        [JsonProperty("precisionWeapon")] public string PrecisionWeapon;
        [JsonProperty("precisionAction")] public string PrecisionAction;
        [JsonProperty("thiefArtifact")] public string ThiefArtifact;
        [JsonProperty("overworldAilmentImmunity")] public OverworldAilmentImmunityEntry OverworldAilmentImmunity;
        [JsonProperty("guardianBonuses")] public GuardianBonusEntry GuardianBonuses;
        [JsonProperty("icon")] public string Icon;
        [JsonProperty("apparelModels")] public ApparelModelEntry ApparelModels;
        [JsonProperty("modifiers")] public ItemModifierEntry Modifiers;
        [JsonProperty("itemModels")] public ModelRendererEntry[] ItemModels;
        [JsonProperty("offHandModels")] public ModelRendererEntry[] OffHandModels;
        [JsonProperty("displayModels")] public ModelRendererEntry[] DisplayModels;
        [JsonProperty("playerModels")] public PlayerModelEntry[] PlayerModels;
        [JsonProperty("raceBindings")] public RaceBindingEntry[] RaceBindings;
#pragma warning restore CS0649
    }
    // Populated by JSON reflection.
#pragma warning disable CS0649
    internal sealed class GuardianBonusEntry
    {
        [JsonProperty("guardHealPercent")] public int GuardHealPercent;
        [JsonProperty("focusHealBonusPercent")] public int FocusHealBonusPercent;
        [JsonProperty("retaliationDamage")] public int RetaliationDamage;
        [JsonProperty("wardDebuffs")] public bool WardDebuffs;
        [JsonProperty("guardFocusRestore")] public int GuardFocusRestore;
        [JsonProperty("guardReckoning")] public bool GuardReckoning;
        [JsonProperty("guardCleanse")] public bool GuardCleanse;
    }
    internal sealed class OverworldAilmentImmunityEntry
    {
        [JsonProperty("displayName")] public string DisplayName;
    }
    internal sealed class ApparelModelEntry
    {
        [JsonProperty("femaleBinding")] public string FemaleBinding;
        [JsonProperty("maleBinding")] public string MaleBinding;
        [JsonProperty("renderers")] public ModelRendererEntry[] Renderers;
    }
    internal sealed class ModelRendererEntry
    {
        [JsonProperty("path")] public string Path;
        [JsonProperty("model")] public string Model;
        [JsonProperty("texture")] public string Texture;
        [JsonProperty("nativeMesh")] public string NativeMesh;
    }
    internal sealed class PlayerModelEntry
    {
        [JsonProperty("skinset")] public string Skinset;
        [JsonProperty("body")] public ModelRendererEntry[] Body;
        [JsonProperty("apparel")] public ModelRendererEntry[] Apparel;
        [JsonProperty("backpack")] public ModelRendererEntry[] Backpack;
    }
    internal sealed class RaceBindingEntry
    {
        [JsonProperty("class")] public string Class;
        [JsonProperty("skinset")] public string Skinset;
        [JsonProperty("body")] public ModelRendererEntry[] Body;
        [JsonProperty("apparel")] public ModelRendererEntry[] Apparel;
    }
}
