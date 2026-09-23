using System;
using FTKModFramework.Core.Data;
using Newtonsoft.Json;

namespace GridEditor
{
    public sealed class FTK_characterModifier
    {
        public int m_ModDefensePhysical, m_ModDefenseMagic, m_ExtraFocus, m_ReflectDamage;
        public float m_ModVitality, m_ModQuickness, m_ModAwareness, m_ModTalent;
    }
}

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Reject(string json)
    {
        try
        {
            JsonConvert.DeserializeObject<ItemModifierEntry>(json).Validate();
        }
        catch (ArgumentException)
        {
            _checks++;
            return;
        }
        throw new Exception("Invalid modifier accepted: " + json);
    }

    private static void Main()
    {
        ItemModifierEntry values = JsonConvert.DeserializeObject<ItemModifierEntry>(
            "{\"armor\":2,\"resistance\":3,\"vitality\":0.1,\"speed\":0.2,\"awareness\":0.15,\"talent\":0.25,\"focusCapacity\":2,\"reflect\":4}");
        values.Validate();
        GridEditor.FTK_characterModifier row = new GridEditor.FTK_characterModifier();
        values.Apply(row);
        Check(row.m_ModDefensePhysical == 2 && row.m_ModDefenseMagic == 3 && row.m_ReflectDamage == 4, "defense and reflection mapping");
        Check(Math.Abs(row.m_ModVitality - 0.1f) < 0.0001f && Math.Abs(row.m_ModQuickness - 0.2f) < 0.0001f, "existing stat mapping");
        Check(Math.Abs(row.m_ModAwareness - 0.15f) < 0.0001f && Math.Abs(row.m_ModTalent - 0.25f) < 0.0001f, "new stat mapping");
        Check(row.m_ExtraFocus == 2, "Focus capacity mapping");

        ItemModifierEntry empty = JsonConvert.DeserializeObject<ItemModifierEntry>("{}");
        empty.Validate();
        empty.Apply(row);
        Check(row.m_ModAwareness == 0 && row.m_ModTalent == 0 && row.m_ExtraFocus == 0 && row.m_ModQuickness == 0, "omitted values clear previously populated fields");

        Reject("{\"focusCapacity\":-1}");
        Reject("{\"focusCapacity\":11}");
        Reject("{\"awareness\":1.1}");
        Reject("{\"talent\":-1.1}");
        Console.WriteLine("PASS: " + _checks + " item modifier mapping and range checks.");
    }
}
