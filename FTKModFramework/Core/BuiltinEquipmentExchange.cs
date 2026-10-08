using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core
{
    internal static class BuiltinEquipmentExchange
    {
        internal const string ModGuid = "com.ftkmf.equipment-exchange";
        internal const string ItemId = "equipment_token";
        private static FTK_items row;
        private static bool dropsEnabled;
        private static string resourceRoot;
        private static bool presentationReady;

        internal static bool IsExactBaseline(TableManager manager)
        {
            if (manager == null || row == null) return false;
            int id = (int)FTK_itembase.GetEnum(row.m_ID);
            return BuiltinExchangeBaseline.IsExact(presentationReady, IdAllocator.CustomIdCount,
                ContentRegistry.CustomIds, typeof(FTK_itemsDB), ItemId, id, row,
                manager.Get<FTK_itemsDB>().GetEntryByInt(id));
        }

        internal static FTK_items Token
        {
            get
            {
                if (row == null) throw new InvalidOperationException("Framework Guild Token is unavailable.");
                return row;
            }
        }

        internal static void RequirePresentation()
        {
            if (!presentationReady) throw new InvalidOperationException("Guild Token presentation is unavailable.");
        }

        internal static void Register()
        {
            if (row != null) return;
            FTK_itembase.ID template = FTK_itembase.GetEnum("royalJewel");
            row = Content.AddItem(ModGuid, ItemId, template, "Guild Token", delegate(FTK_items item)
            {
                item._goldValue = 0;
                item.m_MinLevel = 0;
                item.m_MaxLevel = 10;
                item.m_ItemRarity = FTK_itemRarityLevel.ID.rare;
                item.m_Dropable = false;
                item.m_TownMarket = false;
                item.m_NightMarket = false;
                item.m_DungeonMerchant = false;
                item._shopStock = 0;
                item.m_CollectLoreItemUnlock = String.Empty;
                item.m_DLC = FTK_dlc.ID.None;
                item._useable = false;
                item.m_ObjectType = FTK_itembase.ObjectType.resource;
                item.m_ObjectSlot = FTK_itembase.ObjectSlot.pack;
                item.m_BackpackEquip = false;
            });
            Localization.SetItemDescription(ItemId, "Trade one Guild Token for class equipment at a town Back Alley.");
            try
            {
                byte[] bytes = Resource("guild-token-icon.png");
                int width, height;
                PngStructure.Validate(bytes, 4 * 1024 * 1024, 1024, out width, out height);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes) || texture.width != width || texture.height != height)
                    throw new InvalidOperationException("Guild Token icon failed to decode.");
                texture.name = "Guild Token";
                texture.wrapMode = TextureWrapMode.Clamp;
                Sprite icon = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
                icon.name = "Guild Token";
                UnityEngine.Object.DontDestroyOnLoad(texture);
                UnityEngine.Object.DontDestroyOnLoad(icon);
                row.m_Icon = icon;
                row.m_IconNonClickable = icon;
            }
            catch (Exception error) { throw new InvalidOperationException("Guild Token icon unavailable.", error); }
            RegisterPresentation();
        }

        internal static void RegisterPresentation()
        {
            if (row == null) throw new InvalidOperationException("Guild Token row is unavailable.");
            try
            {
                if (resourceRoot == null)
                {
                    resourceRoot = Path.Combine(Path.GetTempPath(), "ftkmf-exchange-" + Guid.NewGuid().ToString("N"));
                    string assets = Path.Combine(resourceRoot, "assets");
                    Directory.CreateDirectory(assets);
                    WriteResource(assets, "guild-token-icon.png");
                    WriteResource(assets, "guild-token-display.glb");
                    WriteResource(assets, "guild-token-metallic-gloss.png");
                }
                string model = PackageModelPaths.Register(ModGuid, resourceRoot, "assets/guild-token-display.glb");
                string texture = PackageModelPaths.Register(ModGuid, resourceRoot, "assets/guild-token-icon.png");
                string mask = PackageModelPaths.Register(ModGuid, resourceRoot, "assets/guild-token-metallic-gloss.png");
                if (!Content.SetItemDisplayMeshesFromGlb(row, new ItemRendererMesh(".", model, texture, mask)))
                    throw new InvalidOperationException("Guild Token display model registration was rejected.");
                presentationReady = true;
            }
            catch (Exception error) { throw new InvalidOperationException("Guild Token display unavailable.", error); }
        }

        private static void WriteResource(string directory, string name)
        {
            File.WriteAllBytes(Path.Combine(directory, name), Resource(name));
        }

        private static byte[] Resource(string name)
        {
            Assembly assembly = typeof(BuiltinEquipmentExchange).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream("FTKModFramework.assets.equipment-exchange." + name))
            {
                if (stream == null) throw new InvalidOperationException("Guild Token resource is missing: " + name);
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) throw new EndOfStreamException();
                    read += count;
                }
                return bytes;
            }
        }

        internal static void EnableDrops()
        {
            if (dropsEnabled) return;
            Content.SetEnemyDropRule(Token, new EnemyDropRule {
                MinimumDisplayedLevel = 8, OrdinaryChancePercent = 10, BossChancePercent = 50,
                GuaranteedByOpportunity = 6,
                NamedBossGroups = new[] {
                    new[] { "krakenHead", "krakenTentacle", "krakenTentacleMirror" },
                    new[] { "seaKing", "seaKingTentacleA", "seaKingTentacleB" },
                    new[] { "vexor", "vexorEasy" },
                    new[] { "harazuelBoss1" }, new[] { "harazuelBoss2" },
                    new[] { "harazuelBoss3" }, new[] { "harazuelBoss4" }
                }
            });
            dropsEnabled = true;
        }
    }
}
