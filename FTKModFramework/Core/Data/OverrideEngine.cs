using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// The SINGLE place that turns a JSON <c>fields</c> dictionary into member writes on a cloned game
    /// row (spec #6). It sets members EXCLUSIVELY through <c>Core/Reflect</c> — there is no second
    /// reflection layer, and it writes FIELDS only (FR-4 R1: every high-value aliased member on the P1
    /// types is a plain serialized field; the only properties are assets — P4 — or read-only computed
    /// getters with no setter, so <c>Reflect.SetField</c> is sufficient and correct).
    ///
    /// Resolution is driven by the resolved member's <see cref="System.Reflection.FieldInfo"/> type:
    ///  - SCALAR (primitive / string): coerce the JSON box via <c>Convert.ChangeType</c> (P1a).
    ///  - CONTENT-ID enum (the five id enums): <c>Enum.Parse</c> a vanilla name FIRST, then a custom id
    ///    via <see cref="ContentRegistry.TryGetSyntheticId"/> on miss (R3 order).
    ///  - ANY OTHER enum (vanilla enums): <c>Enum.Parse</c> ONLY; never the synthetic fallback.
    ///  - ARRAY: build a typed array, resolving each element by the element type via the same rules.
    ///  - NESTED OBJECT: a JSON object populates the field via Newtonsoft <c>JToken.ToObject</c>.
    ///
    /// Fault isolation (R2): a member that will not resolve logs a WARNING and is skipped; the entry
    /// still loads with its other members applied. An unknown member name logs a WARNING and is skipped.
    /// Nothing here throws out of the loader.
    ///
    /// An ALIAS (<see cref="AliasTable"/>) is rewritten to its real field BEFORE the lookup, so a
    /// friendly alias and a raw field name take the identical resolution path.
    /// </summary>
    internal static class OverrideEngine
    {
        // The five content-id enums (R3): a vanilla NAME resolves via Enum.Parse, else a custom string id
        // resolves to a synthetic int via ContentRegistry. Each maps to the *DB type(s) ContentRegistry
        // keyed the custom id under (FTK_itembase.ID spans BOTH item and weapon DBs). Any enum NOT in this
        // set is a plain vanilla enum and must NEVER touch the synthetic fallback.
        private static readonly Dictionary<Type, Type[]> ContentIdDbsByEnum = BuildContentIdMap();

        /// <summary>
        /// Apply every entry of <paramref name="fields"/> to <paramref name="row"/>. <paramref name="kind"/>
        /// selects the alias table; member names are alias-resolved before lookup. Returns the count of
        /// members successfully written. Null/empty <paramref name="fields"/> is a no-op returning 0.
        /// </summary>
        public static int Apply(object row, string kind, Dictionary<string, object> fields, string context, ValidationReport report)
        {
            if (row == null || fields == null) return 0;

            int applied = 0;
            foreach (KeyValuePair<string, object> kv in fields)
            {
                string realName = AliasTable.Resolve(kind, kv.Key);
                if (ApplyField(row, realName, kv.Value, context, report)) applied++;
            }
            return applied;
        }

        /// <summary>
        /// Set one member, branching on the resolved field type. Returns true if the write succeeded.
        /// </summary>
        private static bool ApplyField(object row, string name, object value, string context, ValidationReport report)
        {
            System.Reflection.FieldInfo field = Reflect.Field(row.GetType(), name);
            if (field == null)
            {
                report.Warning(context + ": unknown field '" + name + "' on " + row.GetType().Name + " (skipped).");
                return false;
            }

            object resolved;
            if (!TryResolve(value, field.FieldType, context, name, report, out resolved))
                return false; // TryResolve already recorded a warning describing why.

            Reflect.SetField(row, name, resolved);
            return true;
        }

        /// <summary>
        /// Resolve a JSON value to <paramref name="target"/> by category: array, then enum (content-id or
        /// vanilla), then nested object, then scalar. Returns false (no throw) on any miss, after recording
        /// a warning, so the caller warns-and-skips.
        /// </summary>
        private static bool TryResolve(object value, Type target, string context, string name, ValidationReport report, out object result)
        {
            result = null;

            if (target.IsArray)
                return TryResolveArray(value, target, context, name, report, out result);

            if (target.IsEnum)
                return TryResolveEnum(value, target, context, name, report, out result);

            if (IsNestedObject(value))
                return TryResolveNested(value, target, context, name, report, out result);

            if (!TryCoerce(value, target, out result))
            {
                report.Warning(context + ": field '" + name + "' value '" + Describe(value) +
                    "' could not be coerced to " + target.Name + " (skipped).");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Resolve an enum field from a string name. For the five content-id enums (R3): try a vanilla
        /// name via <c>Enum.Parse</c> FIRST, then a custom string id via <see cref="ContentRegistry"/> on
        /// miss. For every other (vanilla) enum: <c>Enum.Parse</c> ONLY. A non-string value (e.g. a JSON
        /// number) coerces to the underlying integer type. Returns false (warn + skip) on any miss.
        /// </summary>
        private static bool TryResolveEnum(object value, Type enumType, string context, string name, ValidationReport report, out object result)
        {
            result = null;

            // Allow a raw integer to set an enum (e.g. ordinals), matching the game's serialized form.
            if (!(value is string))
            {
                if (TryCoerce(value, Enum.GetUnderlyingType(enumType), out result))
                {
                    result = Enum.ToObject(enumType, result);
                    return true;
                }
                report.Warning(context + ": field '" + name + "' value '" + Describe(value) +
                    "' is not a valid name or ordinal for enum " + enumType.Name + " (skipped).");
                return false;
            }

            string token = (string)value;

            // 1) Vanilla NAME (applies to ALL enums, content-id and plain).
            object parsed;
            if (TryParseEnumName(enumType, token, out parsed))
            {
                result = parsed;
                return true;
            }

            // 2) Custom string id -> synthetic int -> cast to the enum. ONLY for the five content-id enums;
            //    a vanilla enum (e.g. ObjectSlot) must NEVER reach the synthetic fallback.
            Type[] dbTypes;
            if (ContentIdDbsByEnum.TryGetValue(enumType, out dbTypes))
            {
                int synthetic;
                if (ContentRegistry.TryGetSyntheticId(token, out synthetic, dbTypes))
                {
                    result = Enum.ToObject(enumType, synthetic);
                    return true;
                }
                report.Warning(context + ": field '" + name + "' value '" + token +
                    "' is neither a vanilla " + enumType.Name + " name nor a known custom id (skipped).");
                return false;
            }

            report.Warning(context + ": field '" + name + "' value '" + token +
                "' is not a valid " + enumType.Name + " name (skipped).");
            return false;
        }

        /// <summary>
        /// Build a typed array from a JSON string/scalar array, resolving each element by the element type
        /// via the enum / scalar rules. A non-array value is a warning + skip. An element that fails to
        /// resolve is a warning + skip for that ELEMENT (the array still builds from the rest).
        /// </summary>
        private static bool TryResolveArray(object value, Type arrayType, string context, string name, ValidationReport report, out object result)
        {
            result = null;
            Type elementType = arrayType.GetElementType();

            IList<object> items;
            if (!TryAsList(value, out items))
            {
                report.Warning(context + ": field '" + name + "' expects a JSON array for " +
                    arrayType.Name + " but got '" + Describe(value) + "' (skipped).");
                return false;
            }

            List<object> resolved = new List<object>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                object element;
                string elementCtx = context + " (" + name + "[" + i + "])";
                if (TryResolveElement(items[i], elementType, elementCtx, name, report, out element))
                    resolved.Add(element);
                // else: warning already recorded; drop just this element.
            }

            Array arr = Array.CreateInstance(elementType, resolved.Count);
            for (int i = 0; i < resolved.Count; i++) arr.SetValue(resolved[i], i);
            result = arr;
            return true;
        }

        /// <summary>Resolve one array element: enum element by name/id, otherwise scalar coercion.</summary>
        private static bool TryResolveElement(object value, Type elementType, string context, string name, ValidationReport report, out object result)
        {
            if (elementType.IsEnum)
                return TryResolveEnum(value, elementType, context, name, report, out result);

            if (!TryCoerce(value, elementType, out result))
            {
                report.Warning(context + ": element value '" + Describe(value) +
                    "' could not be coerced to " + elementType.Name + " (skipped).");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Populate a nested-object field from a JSON object (e.g. <c>CharacterSkills</c>, a [Serializable]
        /// class of public bool fields). The game's bundled Newtonsoft has NO non-generic
        /// <c>JToken.ToObject(Type)</c> overload, so we deserialize the token through a <c>JsonReader</c>
        /// against the runtime field type — the exact path Newtonsoft's own generic <c>ToObject</c> uses
        /// internally. Returns false (warn + skip) if deserialization fails.
        /// </summary>
        private static bool TryResolveNested(object value, Type target, string context, string name, ValidationReport report, out object result)
        {
            result = null;
            try
            {
                using (Newtonsoft.Json.JsonReader reader = ((JToken)value).CreateReader())
                {
                    result = new Newtonsoft.Json.JsonSerializer().Deserialize(reader, target);
                }
                return true;
            }
            catch (Exception e)
            {
                report.Warning(context + ": field '" + name + "' nested object could not be read as " +
                    target.Name + " (" + e.Message + ") (skipped).");
                return false;
            }
        }

        /// <summary>
        /// Coerce a JSON scalar box to a target primitive/string. Numbers (boxed as long/double by
        /// Newtonsoft) go through <c>Convert.ChangeType</c> to the field's primitive; a null target-typed
        /// null is allowed; assignable values pass through unchanged. Returns false (no throw) on any
        /// failure so the caller can warn-and-skip.
        /// </summary>
        private static bool TryCoerce(object value, Type target, out object result)
        {
            result = null;

            if (value == null)
            {
                // Only reference / Nullable targets may take null; primitive value-types may not.
                if (!target.IsValueType) return true;
                return false;
            }

            // Already the right type (e.g. string -> string, bool -> bool): no conversion needed.
            if (target.IsInstanceOfType(value))
            {
                result = value;
                return true;
            }

            // Scalar numeric / convertible coercion (e.g. JSON long/double -> float/int/byte).
            try
            {
                result = Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (InvalidCastException) { return false; }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
            catch (ArgumentException) { return false; }
        }

        /// <summary>Case-insensitive <c>Enum.Parse</c> that returns false (no throw) on an unknown name.</summary>
        private static bool TryParseEnumName(Type enumType, string token, out object result)
        {
            try
            {
                object parsed = Enum.Parse(enumType, token, true); // ignoreCase: true
                if (Enum.IsDefined(enumType, parsed))
                {
                    result = parsed;
                    return true;
                }
            }
            catch (ArgumentException) { }
            catch (OverflowException) { }
            result = null;
            return false;
        }

        /// <summary>True if the JSON value is a Newtonsoft object/array (i.e. NOT a scalar to coerce).</summary>
        private static bool IsNestedObject(object value)
        {
            JObject jobject = value as JObject;
            return jobject != null;
        }

        /// <summary>
        /// View a JSON array value as a list of element boxes. Accepts a Newtonsoft <c>JArray</c> (how a
        /// nested array deserializes into an <c>object</c> field) or an already-materialized list/array.
        /// </summary>
        private static bool TryAsList(object value, out IList<object> items)
        {
            items = null;

            JArray jarray = value as JArray;
            if (jarray != null)
            {
                List<object> boxed = new List<object>(jarray.Count);
                for (int i = 0; i < jarray.Count; i++)
                {
                    JToken token = jarray[i];
                    // Scalars unwrap to their CLR box; objects/arrays stay as JToken for nested resolution.
                    boxed.Add(token is JValue ? ((JValue)token).Value : (object)token);
                }
                items = boxed;
                return true;
            }

            IList<object> list = value as IList<object>;
            if (list != null) { items = list; return true; }

            return false;
        }

        private static Dictionary<Type, Type[]> BuildContentIdMap()
        {
            // enum type -> the *DB type(s) ContentRegistry keyed the custom string id under. FTK_itembase.ID
            // spans both the weapon and item DBs (a start item can be either), so check both.
            Dictionary<Type, Type[]> map = new Dictionary<Type, Type[]>();
            map[typeof(GridEditor.FTK_itembase.ID)] = new Type[]
            {
                typeof(GridEditor.FTK_weaponStats2DB),
                typeof(GridEditor.FTK_itemsDB),
            };
            map[typeof(GridEditor.FTK_proficiencyTable.ID)] = new Type[] { typeof(GridEditor.FTK_proficiencyTableDB) };
            map[typeof(GridEditor.FTK_playerGameStart.ID)] = new Type[] { typeof(GridEditor.FTK_playerGameStartDB) };
            map[typeof(GridEditor.FTK_miniEncounter.ID)] = new Type[] { typeof(GridEditor.FTK_miniEncounterDB) };
            map[typeof(GridEditor.FTK_enemyCombat.ID)] = new Type[] { typeof(GridEditor.FTK_enemyCombatDB) };
            return map;
        }

        private static string Describe(object value)
        {
            if (value == null) return "null";
            return value.ToString();
        }
    }
}
