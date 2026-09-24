using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Cache only the finite set of declared names. Numeric, case-varied, whitespace and
    // composite inputs retain the native parser's behavior, including its diagnostics.
    internal static class CanonicalEnumLookup<T> where T : struct
    {
        private static readonly Dictionary<string, T> Names = Build();

        internal static bool Initialize() { return Names != null; }

        private static Dictionary<string, T> Build()
        {
            try
            {
                string[] names = Enum.GetNames(typeof(T));
                Dictionary<string, T> values = new Dictionary<string, T>(names.Length, StringComparer.Ordinal);
                foreach (string name in names)
                    // Match the original ignore-case parser even for aliases or names differing
                    // only by case. Never assume GetNames and GetValues order aliases identically.
                    values.Add(name, (T)Enum.Parse(typeof(T), name, true));
                return values;
            }
            catch (Exception)
            {
                // A changed runtime or enum must disable this optional fast path, not its caller.
                return null;
            }
        }

        internal static bool TryGetValue(string name, out T value)
        {
            value = default(T);
            return name != null && Names != null && Names.TryGetValue(name, out value);
        }
    }
}
