using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    internal static class BuiltinExchangeBaseline
    {
        internal static bool IsExact(bool presentationReady, int allocatedCount,
            Dictionary<Type, Dictionary<string, int>> customIds, Type itemTable,
            string tokenName, int tokenId, object tokenRow, object indexedRow)
        {
            Dictionary<string, int> ids;
            int registeredId;
            return presentationReady && tokenRow != null && allocatedCount == 1 &&
                customIds != null && customIds.Count == 1 &&
                customIds.TryGetValue(itemTable, out ids) && ids != null && ids.Count == 1 &&
                ids.TryGetValue(tokenName, out registeredId) && registeredId == tokenId &&
                object.ReferenceEquals(tokenRow, indexedRow);
        }
    }
}
