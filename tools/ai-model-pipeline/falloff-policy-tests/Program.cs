using System;
using Newtonsoft.Json.Linq;

static class Program
{
    static int checks;

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    static void Reject(JToken value)
    {
        bool rejected = false;
        try { FallOffPolicyFixture.Read(value); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid fallOffPolicy accepted: " + (value == null ? "missing" : value.ToString()));
    }

    static void Main()
    {
        Check(!FallOffPolicyFixture.Read(null), "Missing fallOffPolicy must preserve native behavior.");
        Check(FallOffPolicyFixture.Read(new JValue(FallOffPolicyFixture.PreserveCustomBody)), "Exact policy rejected.");
        Reject(new JValue((object)null));
        Reject(new JValue("preserve-native"));
        Reject(new JValue("PreserveCustomBody"));
        Reject(new JValue(true));
        Reject(new JValue(1));

        JObject explicitProfile = new JObject { { "fallOffPolicy", FallOffPolicyFixture.PreserveCustomBody } };
        Check(FallOffPolicyFixture.Validate(explicitProfile, false), "Explicit plural profile rejected.");
        Check(!FallOffPolicyFixture.Validate(new JObject(), false), "Omitted policy must remain native.");
        bool legacyRejected = false;
        try { FallOffPolicyFixture.Validate(explicitProfile, true); }
        catch (InvalidOperationException) { legacyRejected = true; }
        Check(legacyRejected, "Legacy singular profile accepted preserve-custom-body.");

        Console.WriteLine("PASS " + checks + " fall-off policy parser assertions");
    }
}
