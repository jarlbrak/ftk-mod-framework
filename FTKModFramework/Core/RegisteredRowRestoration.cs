using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Unity-free row ledger. Native arrays keep their existing entries; only the original
    // contiguous custom suffix can be restored. Positions are save identities for classes.
    internal sealed class RegisteredRowRestoration
    {
        private readonly SortedDictionary<int, object> rows = new SortedDictionary<int, object>();

        internal void Record(int index, object row)
        {
            if (index < 0 || row == null) throw new ArgumentException("Invalid registered row.");
            object existing;
            if (rows.TryGetValue(index, out existing) && !object.ReferenceEquals(existing, row))
                throw new InvalidOperationException("Registered row position already owned: " + index);
            rows[index] = row;
        }

        internal Array Restore(Array current)
        {
            if (current == null || current.Rank != 1 || current.GetLowerBound(0) != 0)
                throw new ArgumentException("Expected a native one-dimensional row array.");
            int length = current.Length;
            Type element = current.GetType().GetElementType();
            foreach (KeyValuePair<int, object> row in rows)
            {
                if (!element.IsInstanceOfType(row.Value))
                    throw new InvalidOperationException("Registered row type changed.");
                if (row.Key < current.Length)
                {
                    if (!object.ReferenceEquals(current.GetValue(row.Key), row.Value))
                        throw new InvalidOperationException("Registered row position collides: " + row.Key);
                }
                else if (row.Key != length++)
                    throw new InvalidOperationException("Registered row position has a gap: " + row.Key);
            }
            if (length == current.Length) return current;
            Array result = Array.CreateInstance(element, length);
            Array.Copy(current, result, current.Length);
            foreach (KeyValuePair<int, object> row in rows) result.SetValue(row.Value, row.Key);
            return result;
        }
    }
}
