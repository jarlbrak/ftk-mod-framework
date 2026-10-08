using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        /// <summary>Proficiency ids for a weapon, enemy, custom class, or equipped custom item.</summary>
        [JsonProperty("proficiencies")] public string[] Proficiencies;
        [JsonProperty("replaceProficiencies")] public bool ReplaceProficiencies;
        private ClassWeaponProficiencyEntry[] weaponProficiencies;
        [JsonIgnore] public bool WeaponProficienciesDeclared { get; private set; }
        [JsonProperty("weaponProficiencies")]
        public ClassWeaponProficiencyEntry[] WeaponProficiencies
        {
            get { return weaponProficiencies; }
            set { WeaponProficienciesDeclared = true; weaponProficiencies = value; }
        }
        [JsonProperty("randomDebuffOutcomes")] public string[] RandomDebuffOutcomes;
        [JsonProperty("resistanceDamageBonus")] public ResistanceDamageBonusEntry ResistanceDamageBonus;

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
        [JsonProperty("thiefArmor")] public ThiefArmorEntry ThiefArmor;
        [JsonProperty("thiefArmament")] public string ThiefArmament;
        [JsonProperty("enemyDropRule")] public EnemyDropRuleEntry EnemyDropRule;
        [JsonProperty("townExchange")] public TownExchangeEntry TownExchange;
        [JsonProperty("overworldAilmentImmunity")] public OverworldAilmentImmunityEntry OverworldAilmentImmunity;
        [JsonProperty("guardianBonuses")] public GuardianBonusEntry GuardianBonuses;
        [JsonProperty("guardianProfile")] public GuardianProfileEntry GuardianProfile;
        [JsonProperty("guardianSmiteAction")] public string GuardianSmiteAction;
        [JsonProperty("guardianEquipmentSets")] public GuardianSetEntry[] GuardianEquipmentSets;
        [JsonProperty("icon")] public string Icon;
        /// <summary>Exact vanilla proficiency row whose native combat-button sprite is reused.</summary>
        [JsonProperty("nativeBattleButton")] public string NativeBattleButton;
        [JsonProperty("apparelModels")] public ApparelModelEntry ApparelModels;
        [JsonProperty("modifiers")] public ItemModifierEntry Modifiers;
        [JsonProperty("helmetHairVisibility")] public HelmetHairVisibilityEntry HelmetHairVisibility;
        [JsonProperty("itemModels")] public ModelRendererEntry[] ItemModels;
        [JsonProperty("headProfiles")] public HeadProfileEntry[] HeadProfiles;
        [JsonProperty("offHandModels")] public ModelRendererEntry[] OffHandModels;
        [JsonProperty("displayModels")] public ModelRendererEntry[] DisplayModels;
        [JsonProperty("playerModels")] public PlayerModelEntry[] PlayerModels;
        [JsonProperty("raceBindings")] public RaceBindingEntry[] RaceBindings;
#pragma warning restore CS0649
    }
    // Populated by JSON reflection.
#pragma warning disable CS0649
    internal sealed class ClassWeaponProficiencyEntry
    {
        [JsonProperty("weapons")] public string[] Weapons;
        [JsonProperty("proficiencies")] public string[] Proficiencies;
    }
#pragma warning restore CS0649
    // Populated by JSON reflection.
#pragma warning disable CS0649
    internal sealed class ThiefArmorEntry
    {
        [JsonProperty("family", Required = Required.Always)] public string Family;
        [JsonProperty("slot", Required = Required.Always)] public string Slot;
    }
    internal sealed class EnemyDropRuleEntry
    {
        [JsonProperty("minimumDisplayedLevel", Required = Required.Always)] public int MinimumDisplayedLevel;
        [JsonProperty("ordinaryChancePercent", Required = Required.Always)] public int OrdinaryChancePercent;
        [JsonProperty("bossChancePercent", Required = Required.Always)] public int BossChancePercent;
        [JsonProperty("guaranteedByOpportunity", Required = Required.Always)] public int GuaranteedByOpportunity;
        [JsonProperty("namedBossGroups")] public string[][] NamedBossGroups;
    }
    internal sealed class TownExchangeEntry
    {
        [JsonProperty("token")] public string Token;
        [JsonProperty("offers")] public TownExchangeOfferEntry[] Offers;
    }
    internal sealed class TownExchangeCatalogEntry
    {
        [JsonProperty("ownerClass")] public string OwnerClass;
        [JsonProperty("offers")] public TownExchangeOfferEntry[] Offers;
    }
    internal sealed class TownExchangeOfferEntry
    {
        [JsonProperty("item")] public string Item;
        [JsonProperty("name")] public string Name;
        [JsonProperty("family")] public string Family;
        [JsonProperty("slot")] public string Slot;
    }
    internal sealed class HelmetHairVisibilityEntry
    {
        [JsonProperty("top", Required = Required.Always), JsonConverter(typeof(HelmetHairBooleanConverter))] public bool Top;
        [JsonProperty("bottom", Required = Required.Always), JsonConverter(typeof(HelmetHairBooleanConverter))] public bool Bottom;
    }

    // Newtonsoft otherwise coerces strings and numbers to booleans, unlike package admission.
    internal sealed class HelmetHairBooleanConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) { return objectType == typeof(bool); }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Boolean)
                throw new JsonSerializationException("helmetHairVisibility requires JSON boolean tokens");
            return (bool)reader.Value;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { writer.WriteValue((bool)value); }
    }

    internal sealed class ResistanceDamageBonusEntry
    {
        [JsonProperty("sources")] public string[] Sources;
        [JsonProperty("multiplier")] public float Multiplier;
    }

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
    internal sealed class GuardianProfileEntry
    {
        [JsonProperty("physicalPercent", Required = Required.Always)] public int PhysicalPercent;
        [JsonProperty("smitePercent", Required = Required.Always)] public int SmitePercent;
        [JsonProperty("healingPercent", Required = Required.Always)] public int HealingPercent;
        [JsonProperty("guardReductionPercent", Required = Required.Always)] public int GuardReductionPercent;
        [JsonProperty("guardSmiteHealing"), JsonConverter(typeof(GuardianProfileBooleanConverter))] public bool GuardSmiteHealing;
        [JsonProperty("guardPhysicalBonusPercent"), JsonConverter(typeof(GuardianProfilePercentConverter))] public int GuardPhysicalBonusPercent;
        [JsonProperty("bonuses")] public GuardianBonusEntry Bonuses;
    }
    internal sealed class GuardianProfileBooleanConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) { return objectType == typeof(bool); }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Boolean)
                throw new JsonSerializationException("guardSmiteHealing requires a JSON boolean");
            return (bool)reader.Value;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { writer.WriteValue((bool)value); }
    }
    internal sealed class GuardianProfilePercentConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) { return objectType == typeof(int); }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Integer)
                throw new JsonSerializationException("guardPhysicalBonusPercent requires a JSON integer");
            long percent = System.Convert.ToInt64(reader.Value);
            if (percent < 0 || percent > 50)
                throw new JsonSerializationException("guardPhysicalBonusPercent must be between 0 and 50");
            return (int)percent;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { writer.WriteValue((int)value); }
    }
    internal sealed class GuardianSetEntry
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("head")] public string Head;
        [JsonProperty("body")] public string Body;
        [JsonProperty("feet")] public string Feet;
        [JsonProperty("oneHand")] public string OneHand;
        [JsonProperty("shield")] public string Shield;
        [JsonProperty("twoHand")] public string TwoHand;
        [JsonProperty("minor")] public GuardianProfileEntry Minor;
        [JsonProperty("core")] public GuardianProfileEntry Core;
        [JsonProperty("completion")] public GuardianBonusEntry Completion;
        [JsonProperty("coreProficiencies")] public string[] CoreProficiencies;
        [JsonProperty("armorDamageBonus")] public ResistanceDamageBonusEntry ArmorDamageBonus;
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
    internal sealed class MetallicGlossPathConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) { return objectType == typeof(string); }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.String || string.IsNullOrEmpty((string)reader.Value))
                throw new JsonSerializationException("metallicGlossTexture requires a nonempty PNG path or null");
            return (string)reader.Value;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { writer.WriteValue((string)value); }
    }

    internal sealed class ModelRendererEntry
    {
        [JsonProperty("path")] public string Path;
        [JsonProperty("model")] public string Model;
        [JsonProperty("texture")] public string Texture;
        [JsonProperty("metallicGlossTexture"), JsonConverter(typeof(MetallicGlossPathConverter))] public string MetallicGlossTexture;
        [JsonProperty("nativeMesh")] public string NativeMesh;
        [JsonProperty("matte"), JsonConverter(typeof(MatteBooleanConverter))] public bool Matte;
    }
    [JsonConverter(typeof(HeadProfileEntryConverter))]
    internal sealed class HeadProfileEntry
    {
        [JsonProperty("nativeSkinset")] public string NativeSkinset;
        [JsonProperty("customRace")] public HeadCustomRaceEntry CustomRace;
        [JsonProperty("model", Required = Required.Always)] public HeadProfileModelEntry Model;
        [JsonProperty("faceOcclusion")] public HeadFaceOcclusionEntry FaceOcclusion;
    }
    internal sealed class HeadCustomRaceEntry
    {
        [JsonProperty("modGuid", Required = Required.Always)] public string ModGuid;
        [JsonProperty("key", Required = Required.Always)] public string Key;
    }
    internal sealed class HeadProfileModelEntry
    {
        [JsonProperty("path", Required = Required.Always)] public string Path;
        [JsonProperty("model", Required = Required.Always)] public string Model;
        [JsonProperty("texture", Required = Required.Always)] public string Texture;
        [JsonProperty("matte"), JsonConverter(typeof(MatteBooleanConverter))] public bool Matte;
    }
    internal sealed class HeadFaceOcclusionEntry
    {
        public string BodyPath;
        public HeadPlaneEntry[] Planes;
        public string UpperHair, LowerHair;
    }
    internal sealed class HeadPlaneEntry
    {
        public float X, Y, Z, Distance;
    }
    internal sealed class HeadProfileEntryConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) { return objectType == typeof(HeadProfileEntry); }
        private static void Only(JObject value, params string[] names)
        {
            foreach (JProperty property in value.Properties())
            {
                bool known = false;
                foreach (string name in names) if (property.Name == name) known = true;
                if (!known) throw new JsonSerializationException("Unknown headProfiles field: " + property.Name);
            }
        }
        private static string RequiredString(JObject value, string name)
        {
            JToken token = value[name];
            if (token == null || token.Type != JTokenType.String || string.IsNullOrEmpty((string)token))
                throw new JsonSerializationException("headProfiles requires string " + name);
            return (string)token;
        }
        private static float FiniteFloat(JToken token, string name)
        {
            if (token == null || token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                throw new JsonSerializationException("headProfiles requires numeric " + name);
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1000d || value > 1000d)
                throw new JsonSerializationException("headProfiles plane value out of bounds: " + name);
            return (float)value;
        }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject value = JObject.Load(reader);
            Only(value, "nativeSkinset", "customRace", "model", "faceOcclusion");
            HeadProfileEntry result = new HeadProfileEntry();
            if (value["nativeSkinset"] != null) result.NativeSkinset = RequiredString(value, "nativeSkinset");
            if (value["customRace"] != null)
            {
                JObject race = value["customRace"] as JObject;
                if (race == null) throw new JsonSerializationException("headProfiles customRace must be an object");
                Only(race, "modGuid", "key");
                result.CustomRace = new HeadCustomRaceEntry { ModGuid = RequiredString(race, "modGuid"), Key = RequiredString(race, "key") };
            }
            JObject model = value["model"] as JObject;
            if (model == null) throw new JsonSerializationException("headProfiles model must be an object");
            Only(model, "path", "model", "texture", "matte");
            JToken matte = model["matte"];
            if (matte != null && matte.Type != JTokenType.Boolean)
                throw new JsonSerializationException("headProfiles matte must be a boolean");
            result.Model = new HeadProfileModelEntry { Path = RequiredString(model, "path"),
                Model = RequiredString(model, "model"), Texture = RequiredString(model, "texture"),
                Matte = matte != null && (bool)matte };
            if (value["faceOcclusion"] != null)
            {
                JObject face = value["faceOcclusion"] as JObject;
                if (face == null) throw new JsonSerializationException("faceOcclusion must be an object");
                Only(face, "bodyPath", "planes", "upperHair", "lowerHair");
                JArray planes = face["planes"] as JArray;
                if (planes == null || planes.Count < 4 || planes.Count > 16)
                    throw new JsonSerializationException("faceOcclusion requires 4-16 planes");
                var occlusion = new HeadFaceOcclusionEntry { BodyPath = RequiredString(face, "bodyPath"),
                    UpperHair = RequiredString(face, "upperHair"), LowerHair = RequiredString(face, "lowerHair"),
                    Planes = new HeadPlaneEntry[planes.Count] };
                for (int i = 0; i < planes.Count; i++)
                {
                    JObject plane = planes[i] as JObject;
                    if (plane == null) throw new JsonSerializationException("faceOcclusion plane must be an object");
                    Only(plane, "normal", "distance");
                    JArray normal = plane["normal"] as JArray;
                    if (normal == null || normal.Count != 3)
                        throw new JsonSerializationException("faceOcclusion normal requires three numbers");
                    occlusion.Planes[i] = new HeadPlaneEntry { X = FiniteFloat(normal[0], "normal.x"),
                        Y = FiniteFloat(normal[1], "normal.y"), Z = FiniteFloat(normal[2], "normal.z"),
                        Distance = FiniteFloat(plane["distance"], "distance") };
                    float length2 = occlusion.Planes[i].X * occlusion.Planes[i].X +
                        occlusion.Planes[i].Y * occlusion.Planes[i].Y + occlusion.Planes[i].Z * occlusion.Planes[i].Z;
                    if (length2 < 0.998001f || length2 > 1.002001f)
                        throw new JsonSerializationException("faceOcclusion plane normal must be normalized");
                }
                result.FaceOcclusion = occlusion;
            }
            return result;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { throw new System.NotSupportedException("headProfiles are read-only declarations"); }
    }
    internal sealed class MatteBooleanConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) { return objectType == typeof(bool); }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Boolean)
                throw new JsonSerializationException("matte requires a JSON boolean token");
            return (bool)reader.Value;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        { writer.WriteValue((bool)value); }
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
