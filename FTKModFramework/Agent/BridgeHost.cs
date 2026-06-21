using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace FTKModFramework.Agent
{
    /// <summary>
    /// The Unity-side pump for the agent bridge. The HTTP server runs on a background thread, but every
    /// game read/action must execute on Unity's main thread. This MonoBehaviour lives on a hidden
    /// DontDestroyOnLoad GameObject; <see cref="Update"/> drains one queued action per frame (dequeue under
    /// the lock, invoke OUTSIDE the lock) and the screenshot path runs as a coroutine that signals back to
    /// the blocked HTTP thread. net35: no Task/ConcurrentQueue, only lock + Queue + ManualResetEvent.
    /// </summary>
    internal sealed class BridgeHost : MonoBehaviour
    {
        public static BridgeHost Instance;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Drain everything queued this frame, but invoke each action OUTSIDE the lock so a long game
            // call never blocks the HTTP thread from enqueuing the next request.
            while (true)
            {
                Action work = null;
                lock (AgentBridge.Gate)
                {
                    if (AgentBridge.Queue.Count > 0)
                        work = AgentBridge.Queue.Dequeue();
                }
                if (work == null) break;

                try { work(); }
                catch (Exception e)
                {
                    // RunOnMainThread wraps the real call in a try/catch and stores the exception in its
                    // holder, so we should never get here; log defensively if we do.
                    try { Plugin.Log.LogError("[agent] main-thread work threw: " + e); } catch { }
                }
            }
        }

        /// <summary>
        /// Capture the current frame as PNG on the main thread. Started via StartCoroutine from the HTTP
        /// thread; waits for end-of-frame, reads the backbuffer, encodes, stores the bytes (or null) into
        /// the slot, then signals the waiting HTTP thread.
        /// </summary>
        public IEnumerator CapturePng(byte[][] slot, ManualResetEvent done)
        {
            byte[] bytes = null;
            // The yield must run regardless of failure, so do the risky work inside try blocks that do
            // not wrap the yield itself (C# forbids yield inside try/catch).
            yield return new WaitForEndOfFrame();
            try
            {
                int w = Screen.width;
                int h = Screen.height;
                if (w > 0 && h > 0)
                {
                    Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                    tex.Apply();
                    bytes = tex.EncodeToPNG();
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception e)
            {
                try { Plugin.Log.LogError("[agent] screenshot capture failed: " + e); } catch { }
                bytes = null;
            }

            slot[0] = bytes;
            done.Set();
        }
    }
}
