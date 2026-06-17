using System;
using System.Collections.Generic;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// The SINGLE place that turns a JSON <c>fields</c> dictionary into member writes on a cloned game
    /// row (spec #6). It sets members EXCLUSIVELY through <c>Core/Reflect</c> — there is no second
    /// reflection layer. P1a implements ONLY the scalar path: set a named member to a JSON scalar,
    /// coercing numbers to the member's primitive type via <c>Convert.ChangeType</c> before assignment.
    ///
    /// Fault isolation (R2): a field whose value will not coerce logs a WARNING and is skipped; the
    /// entry still loads with its other fields applied. An unknown member name logs a WARNING and is
    /// skipped likewise.
    ///
    /// P1b/P1c (#8/#9) extend THIS type — enum-by-name, content-id resolution, arrays, nested objects —
    /// by branching inside <see cref="ApplyField"/> on the resolved member type. The
    /// <see cref="Apply"/> loop and the report contract stay as-is, so those phases are additive.
    /// </summary>
    internal static class OverrideEngine
    {
        /// <summary>
        /// Apply every entry of <paramref name="fields"/> to <paramref name="row"/>. Returns the count
        /// of fields successfully written (for self-test assertions). Null/empty <paramref name="fields"/>
        /// is a no-op returning 0.
        /// </summary>
        public static int Apply(object row, Dictionary<string, object> fields, string context, ValidationReport report)
        {
            if (row == null || fields == null) return 0;

            int applied = 0;
            foreach (KeyValuePair<string, object> kv in fields)
            {
                if (ApplyField(row, kv.Key, kv.Value, context, report)) applied++;
            }
            return applied;
        }

        /// <summary>
        /// Set one member. Returns true if the write succeeded. P1a handles the scalar case only.
        /// </summary>
        private static bool ApplyField(object row, string name, object value, string context, ValidationReport report)
        {
            System.Reflection.FieldInfo field = Reflect.Field(row.GetType(), name);
            if (field == null)
            {
                report.Warning(context + ": unknown field '" + name + "' on " + row.GetType().Name + " (skipped).");
                return false;
            }

            object coerced;
            if (!TryCoerce(value, field.FieldType, out coerced))
            {
                report.Warning(context + ": field '" + name + "' value '" + Describe(value) +
                    "' could not be coerced to " + field.FieldType.Name + " (skipped).");
                return false;
            }

            Reflect.SetField(row, name, coerced);
            return true;
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

        private static string Describe(object value)
        {
            if (value == null) return "null";
            return value.ToString();
        }
    }
}
