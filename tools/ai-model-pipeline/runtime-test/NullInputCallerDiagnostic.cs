using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;

public sealed partial class RuntimeModelTest
{
    const string NullInputCallerFlag = "FTK_MODEL_TEST_NULL_INPUT_CALLER";
    static RuntimeModelTest nullInputCallerObserver;
    Harmony nullInputCallerHarmony;
    MethodInfo nullInputCallerMethod;
    int nullInputCallerCount;

    void InstallNullInputCallerDiagnostic()
    {
        // Awake has already verified the exact disposable root and save namespace.
        if (Environment.GetEnvironmentVariable(NullInputCallerFlag) != "1") return;
        try
        {
            if (!ReferenceEquals(nullInputCallerObserver, null)) throw new InvalidOperationException("Null-input observer already installed.");
            nullInputCallerMethod = typeof(FTKInput).GetMethod("GetButton", Members, null,
                new[] { typeof(Rewired.Player), typeof(string) }, null);
            if (nullInputCallerMethod == null || nullInputCallerMethod.IsStatic ||
                nullInputCallerMethod.DeclaringType != typeof(FTKInput) || nullInputCallerMethod.ReturnType != typeof(bool))
                throw new InvalidOperationException("Exact native GetButton(Player,string) overload unavailable.");
            nullInputCallerHarmony = new Harmony("com.ftkmf.runtime-model-test.null-input-caller");
            nullInputCallerObserver = this;
            var prefix = new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("ObserveNullInputCaller", Statics));
            prefix.priority = Priority.Last;
            nullInputCallerHarmony.Patch(nullInputCallerMethod, prefix, null, null, null, null);
            Logger.LogInfo("NULL INPUT CALLER DIAGNOSTIC ACTIVE: session=" + sessionId + "; first eight null actions only; original execution unchanged.");
        }
        catch (Exception error)
        {
            RemoveNullInputCallerDiagnostic();
            try { Logger.LogWarning("NULL INPUT CALLER DIAGNOSTIC UNAVAILABLE: " + error.Message); } catch (Exception) { }
        }
    }

    static void ObserveNullInputCaller(string __1)
    {
        // Positional argument observation only: no ref/out arguments, result or skip-original return.
        RuntimeModelTest observer = nullInputCallerObserver;
        if (__1 != null || ReferenceEquals(observer, null)) return;
        try
        {
            int count = Interlocked.Increment(ref observer.nullInputCallerCount);
            if (count > 8) return;
            string message = "NULL INPUT CALLER: session=" + observer.sessionId + "; occurrence=" + count +
                "; managedThread=" + Thread.CurrentThread.ManagedThreadId + "; utc=" + DateTime.UtcNow.ToString("o") +
                "; FTKInput.GetButton(Rewired.Player,string) action=null; original execution unchanged\n" + new StackTrace(1, true);
            // Avoid Unity object reads: the native caller's thread is not assumed to be the main thread.
            try { observer.Logger.LogWarning(message); } catch (Exception) { }
            try { File.AppendAllText(Path.Combine(observer.output, observer.sessionId + ".null-input-caller.log"), message + "\n"); }
            catch (Exception) { }
        }
        catch (Exception) { } // Diagnostic failures must never replace the original input exception.
    }

    void RemoveNullInputCallerDiagnostic()
    {
        if (ReferenceEquals(nullInputCallerObserver, this)) nullInputCallerObserver = null;
        try
        {
            if (nullInputCallerHarmony != null && nullInputCallerMethod != null)
                nullInputCallerHarmony.Unpatch(nullInputCallerMethod, typeof(RuntimeModelTest).GetMethod("ObserveNullInputCaller", Statics));
        }
        catch (Exception) { }
        finally { nullInputCallerHarmony = null; nullInputCallerMethod = null; }
    }
}
