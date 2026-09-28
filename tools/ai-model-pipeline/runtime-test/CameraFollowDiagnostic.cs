using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using GridEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    const string CameraTraceFlag = "FTK_MODEL_TEST_CAMERA_TRACE";
    const int CameraTraceSamplesPerMethod = 16;
    static readonly string[] CameraTraceNames = { "uiPlayerMainHud.OnPortraiteClick", "FTKUI.FocusOverworldCamera", "FollowHelper.SetCameraTarget", "RtsCamera.Follow(Transform,bool,bool)", "RtsCamera.EndFollow", "Movement.TrackCheckClickPath", "Movement.TrackCheckClickPath.return", "Movement.TrackingPathFinished" };
    static RuntimeModelTest cameraTraceObserver;
    readonly object cameraTraceLock = new object();
    readonly long[] cameraTraceCounts = new long[8];
    readonly JObject[] cameraTraceLatest = new JObject[8];
    readonly MethodInfo[] cameraTraceMethods = new MethodInfo[8];
    readonly MethodInfo[] cameraTracePrefixes = new MethodInfo[8];
    Harmony cameraTraceHarmony;
    int cameraTraceThread, cameraTraceReadFailures, cameraTraceWriteFailures;
    string cameraTraceError;
    bool cameraTraceEnabled;

    void InstallCameraFollowDiagnostic()
    {
        // The isolated root and save namespace have already been checked by Awake.
        if (Environment.GetEnvironmentVariable(CameraTraceFlag) != "1") return;
        try
        {
            if (!ReferenceEquals(cameraTraceObserver, null)) throw new InvalidOperationException("Camera trace already installed.");
            Type[] types = { typeof(uiPlayerMainHud), typeof(FTKUI), typeof(FollowHelper), typeof(RtsCamera), typeof(RtsCamera), typeof(Movement), typeof(Movement), typeof(Movement) };
            string[] names = { "OnPortraiteClick", "FocusOverworldCamera", "SetCameraTarget", "Follow", "EndFollow", "TrackCheckClickPath", "TrackCheckClickPath", "TrackingPathFinished" };
            Type[][] parameters = { Type.EmptyTypes, new[] { typeof(FTKPlayerID) }, new[] { typeof(bool) }, new[] { typeof(Transform), typeof(bool), typeof(bool) }, Type.EmptyTypes, new[] { typeof(HexLand), typeof(bool), typeof(bool), typeof(bool) }, new[] { typeof(HexLand), typeof(bool), typeof(bool), typeof(bool) }, new[] { typeof(bool) } };
            // Resolve the entire exact signature set before installing any hook.
            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo method = types[i].GetMethod(names[i], Members, null, parameters[i], null);
                if (method == null || method.DeclaringType != types[i] || method.IsStatic || method.ReturnType != typeof(void))
                    throw new InvalidOperationException("Exact camera callback unavailable: " + CameraTraceNames[i]);
                cameraTraceMethods[i] = method;
                cameraTracePrefixes[i] = typeof(RuntimeModelTest).GetMethod("ObserveCameraCallback" + i, Statics);
            }
            cameraTraceThread = Thread.CurrentThread.ManagedThreadId;
            cameraTraceHarmony = new Harmony("com.ftkmf.runtime-model-test.camera-follow-trace");
            cameraTraceObserver = this;
            for (int i = 0; i < cameraTraceMethods.Length; i++)
            {
                var prefix = new HarmonyMethod(cameraTracePrefixes[i]);
                prefix.priority = Priority.Last;
                cameraTraceHarmony.Patch(cameraTraceMethods[i], i == 6 ? null : prefix, i == 6 ? prefix : null, null, null, null);
            }
            cameraTraceEnabled = true;
        }
        catch (Exception error)
        {
            cameraTraceError = error.GetType().Name + ": " + error.Message;
            RemoveCameraFollowDiagnostic();
            try { Logger.LogWarning("CAMERA FOLLOW TRACE UNAVAILABLE: " + cameraTraceError); } catch (Exception) { }
        }
    }

    static void ObserveCameraCallback0(uiPlayerMainHud __instance) { ObserveCameraCallback(0, __instance, null, false, false); }
    static void ObserveCameraCallback1(FTKUI __instance, FTKPlayerID __0) { ObserveCameraCallback(1, __instance, __0, false, false); }
    static void ObserveCameraCallback2(FollowHelper __instance, bool __0) { ObserveCameraCallback(2, __instance, null, __0, false); }
    static void ObserveCameraCallback3(RtsCamera __instance, Transform __0, bool __1, bool __2) { ObserveCameraCallback(3, __instance, __0, __1, __2); }
    static void ObserveCameraCallback4(RtsCamera __instance) { ObserveCameraCallback(4, __instance, null, false, false); }

    static void ObserveCameraCallback5(Movement __instance, HexLand __0, bool __1, bool __2, bool __3) { ObserveCameraCallback(5, __instance, __0, __1, __2, __3); }
    static void ObserveCameraCallback6(Movement __instance, HexLand __0, bool __1, bool __2, bool __3) { ObserveCameraCallback(6, __instance, __0, __1, __2, __3); }
    static void ObserveCameraCallback7(Movement __instance, bool __0) { ObserveCameraCallback(7, __instance, null, __0, false); }

    static JObject MovementTraceHex(HexLand hex)
    {
        if (hex == null) return null;
        return new JObject {{"instanceId",hex.GetInstanceID()},{"parentIndex",hex.m_ParentIndex},
            {"index",hex.m_Index},{"type",hex.m_Type.ToString()}};
    }

    static JObject MovementTracePath(System.Collections.Generic.List<HexLand> path)
    {
        if (path == null) return null;
        JArray hexes = new JArray();
        for (int i = 0; i < Math.Min(path.Count,64); i++) hexes.Add(MovementTraceHex(path[i]));
        return new JObject {{"count",path.Count},{"truncated",path.Count>64},{"hexes",hexes}};
    }

    static JObject MovementTraceSnapshot(Movement movement)
    {
        if (movement == null) return null;
        CharacterOverworld cow = movement.m_CharacterOverworld;
        return new JObject {{"instanceId",movement.GetInstanceID()},{"mode",movement.m_Mode.ToString()},
            {"pickingMode",movement.m_PickingMode.ToString()},{"lockedInput",movement.m_LockedInput},
            {"usingThumbStick",movement.m_UsingThumbStick},{"actionPoints",movement.m_ActionPoints},
            {"actionPointsCurrent",movement.m_ActionPointsCurrent},
            {"ownerInstanceId",cow==null?new JValue((object)null):new JValue(cow.GetInstanceID())},
            {"ownerUsesMouse",cow==null?new JValue((object)null):new JValue(cow.m_IsUseMouse)},
            {"pathStart",MovementTraceHex(movement.m_PathStart)},{"lastAdded",MovementTraceHex(movement.m_LastAdded)},
            {"cursorHex",MovementTraceHex(movement.m_CursorHex)},
            {"committedPath",MovementTracePath(movement.m_HexList)},{"partialPath",MovementTracePath(movement.m_HexListPartial)}};
    }

    static void ObserveCameraCallback(int index, UnityEngine.Object instance, object argument, bool first, bool second, bool third = false)
    {
        RuntimeModelTest observer = cameraTraceObserver;
        if (ReferenceEquals(observer, null)) return;
        try
        {
            lock (observer.cameraTraceLock)
            {
                long count = ++observer.cameraTraceCounts[index];
                bool mainThread = Thread.CurrentThread.ManagedThreadId == observer.cameraTraceThread;
                JObject row = new JObject {{"method",CameraTraceNames[index]},{"occurrence",count},
                    {"session",observer.sessionId},{"phase",index==6?"postfix":"prefix"},{"managedThread",Thread.CurrentThread.ManagedThreadId},
                    {"mainThread",mainThread}};
                // A surprising off-thread caller remains observable without dispatching Unity APIs.
                if (mainThread)
                {
                    row["frame"] = Time.frameCount;
                    row["instanceId"] = instance == null ? new JValue((object)null) : new JValue(instance.GetInstanceID());
                    if (index == 0)
                    {
                        CharacterOverworld cow = ((uiPlayerMainHud)instance).m_Cow;
                        row["heroInstanceId"] = cow == null ? new JValue((object)null) : new JValue(cow.GetInstanceID());
                        if (cow != null) row["heroIdentity"] = cow.m_FTKPlayerID.m_TurnIndex + ":" + cow.m_FTKPlayerID.m_PhotonID;
                    }
                    if (index == 1)
                    {
                        FTKPlayerID id = (FTKPlayerID)argument;
                        row["heroIdentity"] = id.m_TurnIndex + ":" + id.m_PhotonID;
                    }
                    if (index == 2)
                    {
                        FollowHelper helper = (FollowHelper)instance;
                        row["offTurnOptional"] = first;
                        row["snap"] = helper.Snap;
                        row["targetInstanceId"] = helper.FollowTarget == null ? new JValue((object)null) : new JValue(helper.FollowTarget.transform.GetInstanceID());
                    }
                    if (index == 3)
                    {
                        Transform target = (Transform)argument;
                        row["targetInstanceId"] = target == null ? new JValue((object)null) : new JValue(target.GetInstanceID());
                        row["snap"] = first; row["offTurnOptional"] = second;
                    }
                    if (index >= 5)
                    {
                        row["movement"] = MovementTraceSnapshot((Movement)instance);
                        if (index == 7) row["walk"] = first;
                        else
                        {
                            row["targetHex"] = MovementTraceHex((HexLand)argument);
                            row["forceMove"] = first; row["rightClick"] = second; row["isController"] = third;
                        }
                    }
                }
                observer.cameraTraceLatest[index] = row;
                if (count > CameraTraceSamplesPerMethod) return;
                if (index == 4) row["callerStack"] = new StackTrace(2, true).ToString();
                try
                {
                    using (var file = new FileStream(Path.Combine(observer.output, observer.sessionId + ".camera-follow.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                    using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                    { writer.WriteLine(row.ToString(Formatting.None)); writer.Flush(); }
                }
                catch (Exception) { observer.cameraTraceWriteFailures++; }
            }
        }
        catch (Exception) { Interlocked.Increment(ref observer.cameraTraceReadFailures); }
    }

    static JObject CameraFollowDiagnosticState()
    {
        RuntimeModelTest observer = cameraTraceObserver;
        if (ReferenceEquals(observer, null)) return new JObject {{"enabled",false}};
        lock (observer.cameraTraceLock)
        {
            JArray methods = new JArray();
            for (int i = 0; i < CameraTraceNames.Length; i++)
                methods.Add(new JObject {{"method",CameraTraceNames[i]},{"total",observer.cameraTraceCounts[i]},
                    {"latest",observer.cameraTraceLatest[i] == null ? null : observer.cameraTraceLatest[i].DeepClone()}});
            return new JObject {{"enabled",observer.cameraTraceEnabled},{"session",observer.sessionId},
                {"samplesPerMethod",CameraTraceSamplesPerMethod},{"readFailures",observer.cameraTraceReadFailures},
                {"writeFailures",observer.cameraTraceWriteFailures},{"error",observer.cameraTraceError},{"methods",methods}};
        }
    }

    void RemoveCameraFollowDiagnostic()
    {
        if (ReferenceEquals(cameraTraceObserver, this)) cameraTraceObserver = null;
        for (int i = 0; i < cameraTraceMethods.Length; i++)
            try
            {
                if (cameraTraceHarmony != null && cameraTraceMethods[i] != null && cameraTracePrefixes[i] != null)
                    cameraTraceHarmony.Unpatch(cameraTraceMethods[i], cameraTracePrefixes[i]);
            }
            catch (Exception) { }
        cameraTraceHarmony = null;
        cameraTraceEnabled = false;
    }
}
