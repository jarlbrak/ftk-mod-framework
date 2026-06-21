using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// Parses method+path and dispatches to StateReader / ActionExecutor / the screenshot coroutine.
    /// Runs on the background listener thread; all game access is marshalled onto the Unity main thread via
    /// AgentBridge.RunOnMainThread (or, for /screenshot, BridgeHost.StartCoroutine). Writes the HTTP
    /// response. Every handler is wrapped so a failure becomes an HTTP error body, never a thrown request.
    /// </summary>
    internal static class HttpRouter
    {
        private const int StateTimeoutMs = 4000;
        private const int ActionTimeoutMs = 8000;
        private const int ScreenshotTimeoutMs = 8000;

        public static void Dispatch(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            string method = req.HttpMethod;
            string path = req.Url.AbsolutePath;

            if (method == "GET" && path == "/health") { HandleHealth(ctx); return; }
            if (method == "GET" && path == "/state") { HandleState(ctx); return; }
            if (method == "GET" && path == "/screenshot") { HandleScreenshot(ctx); return; }
            if (method == "POST" && path == "/action") { HandleAction(ctx); return; }

            WriteJson(ctx, 404, Json.Write(Err("not found: " + method + " " + path)));
        }

        private static void HandleHealth(HttpListenerContext ctx)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["ok"] = true;
            body["port"] = AgentBridge.Port;
            // singlePlayer is best-effort and may be null at the menu; do not marshal heavily for liveness.
            body["singlePlayer"] = TrySinglePlayer();
            WriteJson(ctx, 200, Json.Write(body));
        }

        private static object TrySinglePlayer()
        {
            try
            {
                object result = AgentBridge.RunOnMainThread(delegate
                {
                    Type t = HarmonyLib.AccessTools.TypeByName("GameLogic");
                    if (t == null) return null;
                    System.Reflection.PropertyInfo pi = t.GetProperty("Instance", Core.Reflect.All);
                    object gl = pi != null ? pi.GetValue(null, null) : null;
                    if (gl == null) return null;
                    object sp = Core.Reflect.Invoke(gl, "IsSinglePlayer");
                    return sp is bool ? (object)(bool)sp : null;
                }, 2000);
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static void HandleState(HttpListenerContext ctx)
        {
            try
            {
                object snapshot = AgentBridge.RunOnMainThread(StateReader.ReadState, StateTimeoutMs);
                WriteJson(ctx, 200, Json.Write(snapshot));
            }
            catch (TimeoutException)
            {
                WriteJson(ctx, 500, Json.Write(Err("state read timed out (main thread busy)")));
            }
            catch (Exception e)
            {
                WriteJson(ctx, 500, Json.Write(Err("state read failed: " + e.Message)));
            }
        }

        private static void HandleAction(HttpListenerContext ctx)
        {
            string action = null;
            IDictionary<string, object> argsMap = null;
            try
            {
                string bodyText;
                using (StreamReader sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    bodyText = sr.ReadToEnd();

                object parsed = Json.Parse(bodyText);
                IDictionary<string, object> map = parsed as IDictionary<string, object>;
                if (map != null)
                {
                    object a;
                    if (map.TryGetValue("action", out a)) action = a as string;
                    object args;
                    if (map.TryGetValue("args", out args)) argsMap = args as IDictionary<string, object>;
                }
            }
            catch (Exception e)
            {
                WriteJson(ctx, 200, Json.Write(ActionFail("bad request body: " + e.Message)));
                return;
            }

            // Marshal the action onto the main thread. A precondition miss returns {ok:false}; a timeout or
            // unexpected throw is also surfaced as {ok:false} (HTTP 200) so the agent loop never sees a 500.
            string actCopy = action;
            IDictionary<string, object> argCopy = argsMap;
            try
            {
                object result = AgentBridge.RunOnMainThread(
                    delegate { return ActionExecutor.Execute(actCopy, argCopy); }, ActionTimeoutMs);
                WriteJson(ctx, 200, Json.Write(result));
            }
            catch (TimeoutException)
            {
                WriteJson(ctx, 200, Json.Write(ActionFail("action timed out (main thread busy)")));
            }
            catch (Exception e)
            {
                WriteJson(ctx, 200, Json.Write(ActionFail("action failed: " + e.Message)));
            }
        }

        private static void HandleScreenshot(HttpListenerContext ctx)
        {
            BridgeHost host = BridgeHost.Instance;
            if (host == null)
            {
                WriteJson(ctx, 503, Json.Write(Err("no bridge host (no session)")));
                return;
            }

            ManualResetEvent done = new ManualResetEvent(false);
            byte[][] slot = new byte[1][];

            // The coroutine must be started ON the main thread. Marshal a tiny starter through the queue.
            try
            {
                AgentBridge.RunOnMainThread(delegate
                {
                    host.StartCoroutine(host.CapturePng(slot, done));
                    return null;
                }, 2000);
            }
            catch (Exception e)
            {
                WriteJson(ctx, 503, Json.Write(Err("could not start capture: " + e.Message)));
                return;
            }

            if (!done.WaitOne(ScreenshotTimeoutMs))
            {
                WriteJson(ctx, 503, Json.Write(Err("screenshot timed out")));
                return;
            }

            byte[] png = slot[0];
            if (png == null || png.Length == 0)
            {
                WriteJson(ctx, 503, Json.Write(Err("no frame captured (no session?)")));
                return;
            }

            try
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "image/png";
                ctx.Response.ContentLength64 = png.Length;
                ctx.Response.OutputStream.Write(png, 0, png.Length);
                ctx.Response.OutputStream.Close();
                ctx.Response.Close();
            }
            catch (Exception e)
            {
                try { Plugin.Log.LogError("[agent] screenshot write failed: " + e); } catch { }
                try { ctx.Response.Abort(); } catch { }
            }
        }

        // ----------------------------------------------------------------- io ---------------------------

        private static void WriteJson(HttpListenerContext ctx, int status, string json)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.OutputStream.Close();
                ctx.Response.Close();
            }
            catch (Exception e)
            {
                try { Plugin.Log.LogError("[agent] response write failed: " + e); } catch { }
                try { ctx.Response.Abort(); } catch { }
            }
        }

        private static Dictionary<string, object> Err(string message)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["error"] = message;
            return d;
        }

        private static Dictionary<string, object> ActionFail(string message)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ok"] = false;
            d["error"] = message;
            d["result"] = null;
            return d;
        }
    }
}
