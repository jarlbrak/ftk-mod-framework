using System;
using System.Collections.Generic;
using FTKModFramework.Core.Diagnostics;

// Spec #242 FR-2: which text-table misses the diagnostic logs, how often, and when it stops.
internal static class Program
{
    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }

    private static readonly HashSet<string> ContentIds = new HashSet<string> { "frostbrandBlade", "mudGolem" };

    private static bool IsContentId(string id)
    {
        return ContentIds.Contains(id);
    }

    private static void Main()
    {
        Filter();
        Dedupe();
        Cap();
        Caller();
        Console.WriteLine("PASS: " + _checks + " localization-miss checks.");
    }

    private static void Filter()
    {
        Func<string, bool> ids = IsContentId;

        // Real misses: STR_ keys the framework does not pass through on purpose.
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_DungeonNoneDisplay", ids), "a vanilla missing key is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_rewardAlignment", ids), "a vanilla missing key is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_", ids), "a bare prefix is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_12Popup", ids), "a synthetic id with another suffix is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_frostbrandBladeDisplay", ids), "a content id with a suffix is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_mudGolem", null), "no content lookup means no content filter");
        Check(!LocalizationMissLog.IsSyntheticIdKey("str_1000Display"), "the prefix is case-sensitive, like Enum.Parse");

        // Custom realm names: HexLand.GetRealmDisplayValue builds STR_<int>Display.
        Check(LocalizationMissLog.IsIntentionalPassthrough("STR_1000123Display", ids), "a synthetic realm key is skipped");
        Check(LocalizationMissLog.IsIntentionalPassthrough("STR_-5Display", ids), "a negative synthetic key is skipped");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_-Display", ids), "a sign without digits is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_Display", ids), "no digits is logged");
        Check(!LocalizationMissLog.IsIntentionalPassthrough("STR_12Displays", ids), "only the exact Display suffix is skipped");

        // Custom enemy IDs, as the string ID and as the decimal ID after a Photon round trip.
        Check(LocalizationMissLog.IsIntentionalPassthrough("STR_mudGolem", ids), "a registered enemy id is skipped");
        Check(LocalizationMissLog.IsIntentionalPassthrough("STR_1000456", ids), "a decimal synthetic enemy id is skipped");
        Check(LocalizationMissLog.IsIntentionalPassthrough("STR_frostbrandBlade", ids), "any registered content id is skipped");

        // Literal display names shown verbatim: not STR_ keys.
        Check(LocalizationMissLog.IsIntentionalPassthrough("Goblin Warren", ids), "a literal name is skipped");
        Check(LocalizationMissLog.IsIntentionalPassthrough("Frostbrand", ids), "a one-word literal is skipped");
        Check(LocalizationMissLog.IsIntentionalPassthrough("", ids), "an empty key is skipped");
        Check(LocalizationMissLog.IsIntentionalPassthrough(null, ids), "a null key is skipped");

        LocalizationMissLog log = new LocalizationMissLog(8, ids);
        Check(log.Observe("TextLore", "STR_1000123Display") == LocalizationMissDecision.Ignore, "Observe applies the filter");
        Check(log.Observe("TextMenu", "Goblin Warren") == LocalizationMissDecision.Ignore, "Observe skips literals");
        Check(log.Observe("TextLore", null) == LocalizationMissDecision.Ignore, "Observe ignores a null key");
        Check(log.Observe(null, "STR_x") == LocalizationMissDecision.Ignore, "Observe ignores a null table");
        Check(log.LoggedCount == 0, "filtered keys do not count toward the cap");
    }

    private static void Dedupe()
    {
        int lookups = 0;
        Func<string, bool> ids = id => { lookups++; return IsContentId(id); };
        LocalizationMissLog log = new LocalizationMissLog(8, ids);

        Check(log.Observe("TextLore", "STR_DungeonNoneDisplay") == LocalizationMissDecision.Log, "the first miss logs");
        for (int i = 0; i < 100; i++)
            Check(log.Observe("TextLore", "STR_DungeonNoneDisplay") == LocalizationMissDecision.Ignore, "a repeat miss is silent");
        Check(log.Observe("TextMisc", "STR_DungeonNoneDisplay") == LocalizationMissDecision.Log, "the same key in another table logs once");
        Check(log.Observe("TextMisc", "STR_DungeonNoneDisplay") == LocalizationMissDecision.Ignore, "and then is silent");
        Check(log.LoggedCount == 2, "two distinct (table, key) pairs were logged");

        int before = lookups;
        Check(log.Observe("TextEnemy", "STR_mudGolem") == LocalizationMissDecision.Ignore, "a content id is skipped");
        for (int i = 0; i < 50; i++) log.Observe("TextEnemy", "STR_mudGolem");
        Check(lookups == before + 1, "a skipped key is remembered, so the content lookup runs once");
    }

    private static void Cap()
    {
        Check(LocalizationMissLog.DefaultCap == 256, "the default cap is 256 keys");

        LocalizationMissLog log = new LocalizationMissLog(3, IsContentId);
        Check(log.Observe("TextMisc", "STR_a") == LocalizationMissDecision.Log, "key 1 logs");
        Check(log.Observe("TextMisc", "STR_b") == LocalizationMissDecision.Log, "key 2 logs");
        Check(log.Observe("TextMenu", "STR_c") == LocalizationMissDecision.Log, "key 3 logs");
        Check(!log.CapReached, "the cap is not reached at exactly the cap");
        Check(log.Observe("TextMisc", "STR_a") == LocalizationMissDecision.Ignore, "a seen key at the cap stays silent");
        Check(log.Observe("TextMisc", "Literal text") == LocalizationMissDecision.Ignore, "a literal at the cap stays silent");
        Check(log.Observe("TextMisc", "STR_d") == LocalizationMissDecision.CapReached, "the next new key reports the cap once");
        Check(log.CapReached, "the cap is recorded");
        Check(log.Observe("TextMisc", "STR_e") == LocalizationMissDecision.Ignore, "after the cap line nothing logs");
        Check(log.Observe("TextStory", "STR_f") == LocalizationMissDecision.Ignore, "in any table");
        Check(log.LoggedCount == 3, "exactly the cap was logged");

        LocalizationMissLog none = new LocalizationMissLog(0, IsContentId);
        Check(none.Observe("TextMisc", "STR_a") == LocalizationMissDecision.CapReached, "a zero cap reports once");
        Check(none.Observe("TextMisc", "STR_b") == LocalizationMissDecision.Ignore, "then stays silent");

        // Remembered passthroughs are bounded; past the bound they are re-filtered, never logged.
        LocalizationMissLog many = new LocalizationMissLog(1, IsContentId);
        for (int i = 0; i < LocalizationMissLog.MaxRememberedPassthroughs + 10; i++)
            Check(many.Observe("TextMenu", "Literal " + i) == LocalizationMissDecision.Ignore, "literals never log");
        Check(many.Observe("TextMenu", "Literal 5") == LocalizationMissDecision.Ignore, "a remembered literal stays silent");
        Check(many.Observe("TextMenu", "Literal " + (LocalizationMissLog.MaxRememberedPassthroughs + 5)) == LocalizationMissDecision.Ignore, "an unremembered literal is re-filtered");
        Check(many.LoggedCount == 0 && !many.CapReached, "literals never consume the cap");
    }

    private static void Caller()
    {
        List<string> frames = new List<string>
        {
            "FTKModFramework.Core.LocalizationMissPatch.GetGenRow_Postfix",
            "DMD<Google2u.TextMisc::GetGenRow>",
            "Google2u.TextMisc.GetGenRow",
            "FTKHub.Localized",
            "System.Reflection.MonoMethod.Invoke",
            "MultiChoose.GetDisplay",
            "uiRewardMenu.Show",
            "uiPlayerMainHud.Update",
            "Deeper.Frame",
        };
        Check(LocalizationMissLog.TrimCaller(frames, 3) == "MultiChoose.GetDisplay < uiRewardMenu.Show < uiPlayerMainHud.Update",
            "the caller skips logger, Harmony, lookup and FTKHub frames and keeps three");
        Check(LocalizationMissLog.TrimCaller(frames, 1) == "MultiChoose.GetDisplay", "the frame count is honored");
        Check(LocalizationMissLog.TrimCaller(new List<string> { "FTKHub.LocalizeString", null, "" }, 3) == "unknown", "only plumbing is unknown");
        Check(LocalizationMissLog.TrimCaller(null, 3) == "unknown", "no frames is unknown");
        Check(LocalizationMissLog.TrimCaller(frames, 0) == "unknown", "zero frames is unknown");

        string line = LocalizationMissLog.FormatMiss("TextLore", "STR_DungeonNoneDisplay", "QuestLogicBase.SetMessageParams");
        Check(line == "[loc-miss] TextLore has no row 'STR_DungeonNoneDisplay' (caller: QuestLogicBase.SetMessageParams)", "one line names table, key and caller");
        Check(LocalizationMissLog.FormatCapReached(256).StartsWith("[loc-miss] Logged 256 ", StringComparison.Ordinal), "the cap line names the cap");
    }
}
