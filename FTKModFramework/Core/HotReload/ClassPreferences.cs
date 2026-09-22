using System;
using System.IO;
using System.Collections.Generic;
using GridEditor;
using FTKModFramework.Core.Marketplace;
using UnityEngine;

namespace FTKModFramework.Core.HotReload
{
    // An intent is durable before the helper changes current. Startup resolves that decision
    // before marketplace bootstrap can advance current again, including with hot reload off.
    internal sealed class ClassPreferences
    {
        internal static bool RecoveryFaulted { get; private set; }
        internal static string RecoveryNotice { get; private set; }
        private readonly PreferenceTransaction transaction;
        internal ClassPreferences()
        {
            transaction = new PreferenceTransaction(new UnityPreferences(), Keys());
        }
        private static string[] Keys()
        {
            FTK_playerGameStart[] rows = TableManager.Instance.Get<FTK_playerGameStartDB>().m_Array;
            string[] keys = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++) keys[i] = rows[i].m_ID;
            return keys;
        }
        internal void Plan()
        {
            FTK_playerGameStartDB db = TableManager.Instance.Get<FTK_playerGameStartDB>();
            int fallback = -1;
            Dictionary<string, int> custom;
            ContentRegistry.CustomIds.TryGetValue(typeof(FTK_playerGameStartDB), out custom);
            for (int i = 0; i < db.m_Array.Length; i++)
            {
                FTK_playerGameStart row = db.m_Array[i];
                if ((custom == null || !custom.ContainsKey(row.m_ID)) && row.m_Release &&
                    row.m_DLC == FTK_dlc.ID.None && db.IsUnlock((FTK_playerGameStart.ID)i, true))
                { fallback = i; break; }
            }
            transaction.Plan(Keys(), fallback);
        }
        internal void Prepare(string root, string before, string after) { transaction.Prepare(root, before, after); }
        internal void Commit() { transaction.Complete(true); }
        internal void Rollback() { transaction.Complete(false); }
        internal static void RecoverPending(string root)
        {
            try
            {
                if (!File.Exists(PreferenceTransaction.PathFor(root))) return;
                MarketplaceResult state = MarketplaceProtocol.ReadState(root);
                PreferenceTransaction.Recover(root, state.Active == null ? "" : state.Active.GenerationId, new UnityPreferences());
            }
            catch (Exception error)
            {
                RecoveryFaulted = true;
                RecoveryNotice = "Remembered class recovery failed. Quit and repair the marketplace preference journal: " + error.Message;
                Plugin.Log.LogError("[hot-reload] " + RecoveryNotice);
            }
        }
        private sealed class UnityPreferences : IClassPreferenceStore
        {
            public bool Has(string key) { return PlayerPrefs.HasKey(key); }
            public int Get(string key) { return PlayerPrefs.GetInt(key, -1); }
            public void Set(string key, int value) { PlayerPrefs.SetInt(key, value); }
            public void Delete(string key) { PlayerPrefs.DeleteKey(key); }
            public void Save() { PlayerPrefs.Save(); }
        }
    }
}
