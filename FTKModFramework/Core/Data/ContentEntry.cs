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

        /// <summary>Proficiency ids to attach (P1c; carried but not consumed in P1a).</summary>
        [JsonProperty("proficiencies")] public string[] Proficiencies;
#pragma warning restore CS0649
    }
}
