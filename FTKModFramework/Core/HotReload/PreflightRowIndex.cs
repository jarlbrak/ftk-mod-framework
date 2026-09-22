using System;
using System.Collections.Generic;

namespace FTKModFramework.Core.HotReload
{
    internal static class PreflightRowIndex
    {
        internal static Dictionary<int, T> Build<T>(IEnumerable<T> rows, HashSet<int> required, Func<T, int> identity)
        {
            Dictionary<int, T> result = new Dictionary<int, T>();
            if (required.Count == 0) return result;
            foreach (T row in rows)
            {
                int id = identity(row);
                if (!required.Contains(id)) continue;
                if (result.ContainsKey(id)) throw new InvalidOperationException("Ambiguous registered model item: " + id);
                result.Add(id, row);
            }
            if (result.Count != required.Count) throw new InvalidOperationException("Model plan has no registered item.");
            return result;
        }
    }
}
