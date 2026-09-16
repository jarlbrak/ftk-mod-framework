using System;

internal static class NativePartyClassPolicy
{
    internal static bool ExplicitDirectionalControl(string name, string direction)
    {
        if (direction != "left" && direction != "right")
            throw new InvalidOperationException("Unknown native class direction.");
        return string.Equals(name, direction == "right" ? "classArrowNext" : "classArrowPrevious", StringComparison.Ordinal);
    }
    internal static bool EligibleArrow(bool explicitDirectional, bool exactCallback, int state,
        bool ownerChild, bool active, bool interactable, bool visible)
    {
        return explicitDirectional && exactCallback && (state == 1 || state == 2)
            && ownerChild && active && interactable && visible;
    }
    internal static bool GraphicVisible(bool graphicActive, bool canvasActive, bool culled, float alpha)
    {
        return graphicActive && canvasActive && !culled && !float.IsNaN(alpha) && !float.IsInfinity(alpha) && alpha > 0f;
    }
    // Repeated serialized references to one button are one authority. Distinct
    // eligible buttons are ambiguous even when their callbacks are identical.
    internal static int UniqueEligibleArrow(int[] identities, bool[] eligible)
    {
        if (identities == null || eligible == null || identities.Length != eligible.Length || identities.Length > 8)
            throw new InvalidOperationException("Invalid native arrow inventory.");
        int selected = -1;
        for (int i = 0; i < identities.Length; i++)
        {
            if (!eligible[i]) continue;
            if (identities[i] == 0) throw new InvalidOperationException("Eligible arrow has no identity.");
            if (selected < 0) selected = i;
            else if (identities[selected] != identities[i]) return -2;
        }
        return selected;
    }
    internal static void RequireSelectionOpen(bool startConsumed, bool uncertain, int frame, int lastFrame)
    {
        if (startConsumed || uncertain || frame <= lastFrame)
            throw new InvalidOperationException("Party selection is closed after Start, an uncertain click, or within the same frame.");
    }
    internal static int Next(bool[] visible, int current, string direction)
    {
        if (visible == null || visible.Length < 1 || visible.Length > 512 || current < 0
            || current >= visible.Length || (direction != "left" && direction != "right"))
            throw new InvalidOperationException("Invalid bounded native class route.");
        int step = direction == "right" ? 1 : -1;
        for (int i = 1; i <= visible.Length; i++)
        {
            int index = (current + step * i + visible.Length) % visible.Length;
            if (visible[index]) return index;
        }
        throw new InvalidOperationException("Native class route has no visible class.");
    }
}
