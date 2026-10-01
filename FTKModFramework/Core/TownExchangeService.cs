using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    internal static class TownExchangeService
    {
        private static FTK_itembase.ID token = FTK_itembase.ID.None;
        private static TownExchangeOffer[] offers = new TownExchangeOffer[0];
        private sealed class OfferFlags
        {
            internal FTK_itembase Row;
            internal bool Dropable;
            internal bool Town;
            internal bool Night;
            internal bool Dungeon;
        }
        private static readonly List<OfferFlags> previousFlags = new List<OfferFlags>();
        private static readonly TownExchangeTransaction transaction = new TownExchangeTransaction();
        private static Button entry;
        private static readonly ItemContainer displayStock = new ItemContainer();
        private static CharacterOverworld stockBuyer;
        private static FTK_itembase.ID[] stockItems = new FTK_itembase.ID[0];
        private static uiBuyMenuHud exchangeShop;
        private static bool sellTabWasActive;
        private static string goldLabelWas;
        private static Image currencyIcon;
        private static Sprite currencyIconWas;
        private static bool openingShop;
        private static uiTownServiceMenu menu;
        private static string refreshStage;

        internal static bool HasRegistrations { get { return token != FTK_itembase.ID.None && offers.Length != 0; } }

        internal static bool IsExclusiveItem(FTK_itembase.ID item)
        {
            for (int i = 0; i < offers.Length; i++)
                if (offers[i].Item == item) return true;
            return false;
        }

        internal static bool IsCurrencyItem(FTK_itembase.ID item)
        {
            return HasRegistrations && item == token;
        }

        internal static bool IsShopBlockedItem(FTK_itembase.ID item)
        {
            return IsCurrencyItem(item) || IsExclusiveItem(item);
        }

        internal static void Clear()
        {
            if (exchangeShop != null) ShopHidden(exchangeShop);
            openingShop = false;
            if (entry != null) Release(entry.gameObject);
            entry = null;
            menu = null;
            foreach (OfferFlags saved in previousFlags)
            {
                if (saved.Row == null) continue;
                saved.Row.m_Dropable = saved.Dropable;
                saved.Row.m_TownMarket = saved.Town;
                saved.Row.m_NightMarket = saved.Night;
                saved.Row.m_DungeonMerchant = saved.Dungeon;
            }
            previousFlags.Clear();
            token = FTK_itembase.ID.None;
            offers = new TownExchangeOffer[0];
            stockBuyer = null;
            stockItems = new FTK_itembase.ID[0];
            transaction.ClearCompleted();
        }

        private static void Release(GameObject objectToRelease)
        {
            if (objectToRelease == null) return;
            objectToRelease.SetActive(false);
            UnityEngine.Object.Destroy(objectToRelease);
        }

        internal static void Register(FTK_itembase.ID currency, TownExchangeOffer[] catalog)
        {
            if (currency == FTK_itembase.ID.None || catalog == null || catalog.Length == 0)
                throw new ArgumentException("Town exchange requires a token and offers.");
            if (HasRegistrations && token != currency)
                throw new InvalidOperationException("Town exchange registrations must use the same token.");
            var seen = new HashSet<FTK_itembase.ID>();
            for (int i = 0; i < offers.Length; i++) seen.Add(offers[i].Item);
            var copy = new TownExchangeOffer[catalog.Length];
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i] == null || catalog[i].Item == currency || !seen.Add(catalog[i].Item))
                    throw new ArgumentException("Town exchange offers must be distinct physical items.");
                int ownerClass = (int)catalog[i].OwnerClass;
                FTK_playerGameStart owner = ownerClass < 0 ? null : Content.Db<FTK_playerGameStartDB>().GetEntryByInt(ownerClass);
                if (owner == null || (int)FTK_playerGameStart.GetEnum(owner.m_ID) != ownerClass)
                    throw new ArgumentException("Town exchange offers require a registered owner class.");
                FTK_itembase row = FTK_itembase.GetItemBase(catalog[i].Item);
                if (row == null ||
                    !ContentRegistry.IsRegisteredSyntheticId((int)catalog[i].Item, typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB)))
                    throw new ArgumentException("Town exchange offer is not registered.");
                if (row.m_BackpackEquip)
                    throw new ArgumentException("Backpack-active items cannot use the town exchange batch.");
                copy[i] = catalog[i];
            }
            if (FTK_itembase.GetItemBase(currency) == null ||
                !ContentRegistry.IsRegisteredSyntheticId((int)currency, typeof(FTK_itemsDB)))
                throw new ArgumentException("Town exchange token is not registered.");
            if (FTK_itembase.GetItemBase(currency).m_BackpackEquip)
                throw new ArgumentException("Backpack-active tokens cannot use the town exchange batch.");
            Array.Sort(copy, delegate(TownExchangeOffer left, TownExchangeOffer right)
            {
                int order = SlotRank(left.Slot).CompareTo(SlotRank(right.Slot));
                return order != 0 ? order : String.CompareOrdinal(left.Family + left.Name, right.Family + right.Name);
            });
            for (int i = 0; i < copy.Length; i++)
            {
                FTK_itembase row = FTK_itembase.GetItemBase(copy[i].Item);
                previousFlags.Add(new OfferFlags { Row = row, Dropable = row.m_Dropable,
                    Town = row.m_TownMarket, Night = row.m_NightMarket, Dungeon = row.m_DungeonMerchant });
                row.m_Dropable = false;
                row.m_TownMarket = false;
                row.m_NightMarket = false;
                row.m_DungeonMerchant = false;
            }
            FTK_itembase tokenRow = FTK_itembase.GetItemBase(currency);
            if (!HasRegistrations)
            {
                previousFlags.Add(new OfferFlags { Row = tokenRow, Dropable = tokenRow.m_Dropable,
                    Town = tokenRow.m_TownMarket, Night = tokenRow.m_NightMarket, Dungeon = tokenRow.m_DungeonMerchant });
                tokenRow.m_TownMarket = false;
                tokenRow.m_NightMarket = false;
                tokenRow.m_DungeonMerchant = false;
            }
            TownExchangeOffer[] combined = new TownExchangeOffer[offers.Length + copy.Length];
            Array.Copy(offers, combined, offers.Length);
            Array.Copy(copy, 0, combined, offers.Length, copy.Length);
            token = currency;
            offers = combined;
            stockBuyer = null;
            stockItems = new FTK_itembase.ID[0];
            displayStock.m_CountDictionary.Clear();
        }

        private static int SlotRank(string slot)
        {
            if (slot == "Head") return 0;
            if (slot == "Body") return 1;
            if (slot == "Foot") return 2;
            if (slot == "RightHand") return 3;
            if (slot == "LeftHand") return 4;
            return 5;
        }

        private static CharacterOverworld Buyer(uiTownServiceMenu current)
        {
            if (current == null || !current.isActiveAndEnabled || !(current.m_Town is MiniHexTown)) return null;
            if (uiLocationMenuDisplay.Instance == null || uiLocationMenuDisplay.Instance.m_IsPeep) return null;
            CharacterOverworld cow = current.m_CurrentCow;
            if (cow == null || !cow.m_FTKPlayerID.IsLocal() || !cow.IsOwner || cow.m_PlayerInventory == null) return null;
            if (GameLogic.Instance == null || GameLogic.Instance.GetCurrentCOW() != cow) return null;
            if (cow.m_HexLand == null || cow.m_HexLand != current.m_Town.m_HexLand) return null;
            if (!PhotonNetwork.offlineMode) return null;
            return cow;
        }

        private static CharacterOverworld ShopBuyer()
        {
            uiBuyMenuHud shop = exchangeShop;
            if (!IsExchangeShop(shop) || shop.m_ThisHex == null || !(shop.m_ThisHex is MiniHexTown) ||
                uiLocationMenuDisplay.Instance == null || uiLocationMenuDisplay.Instance.m_IsPeep || !PhotonNetwork.offlineMode)
                return null;
            CharacterOverworld cow = shop.m_CurrentCow;
            if (cow == null || !cow.m_FTKPlayerID.IsLocal() || !cow.IsOwner || cow.m_PlayerInventory == null ||
                GameLogic.Instance == null || GameLogic.Instance.GetCurrentCOW() != cow ||
                cow.m_HexLand == null || cow.m_HexLand != shop.m_ThisHex.m_HexLand) return null;
            return cow;
        }

        private static int Count(CharacterOverworld cow, FTK_itembase.ID item)
        {
            return cow.m_PlayerInventory.GetItemCount(PlayerInventory.ContainerID.Backpack, item);
        }

        private static string TokenName()
        {
            return FTKHub.Instance != null ? FTKHub.Instance.GetItemDisplayName(token) : "token";
        }

        private static int OwnedCount(CharacterOverworld cow, TownExchangeOffer offer)
        {
            return TownExchangeStockPolicy.OwnedCount(offer.Slot, Count(cow, offer.Item),
                slot => cow.m_PlayerInventory.GetItemCount(SlotId(slot), offer.Item));
        }

        private static bool Eligible(CharacterOverworld cow, TownExchangeOffer offer)
        {
            return cow != null && cow.m_CharacterStats != null &&
                TownExchangeStockPolicy.Eligible((int)offer.OwnerClass,
                    (int)cow.m_CharacterStats.m_CharacterClass, OwnedCount(cow, offer));
        }

        private static void RefreshDisplayStock(CharacterOverworld cow, uiBuyMenuHud shop)
        {
            var visible = new List<FTK_itembase.ID>();
            for (int i = 0; i < offers.Length; i++)
                if (Eligible(cow, offers[i])) visible.Add(offers[i].Item);
            bool changed = cow != stockBuyer || visible.Count != stockItems.Length;
            if (!changed)
                for (int i = 0; i < visible.Count; i++)
                    if (visible[i] != stockItems[i]) { changed = true; break; }
            if (!changed) return;
            stockBuyer = cow;
            stockItems = visible.ToArray();
            displayStock.m_CountDictionary.Clear();
            for (int i = 0; i < stockItems.Length; i++) displayStock.m_CountDictionary[stockItems[i]] = 1;
            if (shop != null)
            {
                shop.m_IsStockListChanged = true;
                shop.m_IsStockListFullRefresh = true;
            }
        }

        private sealed class NativeInventory : ITownExchangeInventory
        {
            private readonly CharacterOverworld cow;
            private readonly FTK_itembase.ID currency;
            private readonly FTK_itembase.ID item;

            internal NativeInventory(CharacterOverworld owner, FTK_itembase.ID tokenId, FTK_itembase.ID itemId)
            {
                cow = owner;
                currency = tokenId;
                item = itemId;
            }

            public int TokenCount { get { return Count(cow, currency); } }
            public int ItemCount { get { return Count(cow, item); } }
            public int EquippedItemCount
            {
                get
                {
                    TownExchangeOffer offer = FindOffer(item);
                    return offer == null ? 0 : OwnedCount(cow, offer) - ItemCount;
                }
            }
            private Dictionary<FTK_itembase.ID, int> Counts { get { return cow.m_PlayerInventory.m_ContainerBackpack.m_CountDictionary; } }
            public void RemoveToken()
            {
                if (TokenCount < 1) throw new InvalidOperationException("No token in buyer backpack.");
                SetCount(currency, TokenCount - 1);
            }
            public void AddItem()
            {
                SetCount(item, ItemCount + 1);
            }
            public void RestoreCounts(int tokenCount, int itemCount)
            {
                SetCount(currency, tokenCount);
                SetCount(item, itemCount);
            }
            private void SetCount(FTK_itembase.ID id, int count)
            {
                if (count < 0) throw new InvalidOperationException("Negative inventory count.");
                if (count == 0) Counts.Remove(id);
                else Counts[id] = count;
            }
            public void NotifyChanged()
            {
                cow.m_PlayerInventory.m_ContainerBackpack.Changed();
                cow.m_UIPlayMainHud.RepopQuickItems();
                FTKHub.Instance.m_CallEventsByName("oncowinvremove", null);
                FTKHub.Instance.m_CallEventsByName("oncowinvadd", null);
            }
        }

        internal static bool Buy(uiTownServiceMenu current, TownExchangeOffer offer, string nonce)
        {
            if (offer == null || token == FTK_itembase.ID.None || Array.IndexOf(offers, offer) < 0) return false;
            CharacterOverworld cow = ShopBuyer();
            if (cow == null || !Eligible(cow, offer) || Count(cow, token) < 1 || FTK_itembase.GetItemBase(offer.Item) == null) return false;
            bool result = transaction.TryBuy(nonce, new NativeInventory(cow, token, offer.Item));
            if (transaction.RecoveryFailed)
                Debug.LogError("Town exchange stopped: inventory recovery could not restore exact counts. Reload the saved game before trading again.");
            return result;
        }

        private static void ReplaceServiceIcon(GameObject row)
        {
            FTK_itembase currency = FTK_itembase.GetItemBase(token);
            if (currency == null || currency.m_Icon == null) return;
            RectTransform root = row.transform as RectTransform;
            if (root == null) return;
            Image target = null;
            float left = 0f;
            foreach (Image candidate in row.GetComponentsInChildren<Image>(true))
            {
                if (candidate.gameObject == row || candidate.sprite == null ||
                    candidate.GetComponentInParent<Canvas>() == null) continue;
                RectTransform rect = candidate.transform as RectTransform;
                if (rect == null || rect.rect.width < 24f || rect.rect.width > 100f ||
                    rect.rect.height < 24f || rect.rect.height > 100f) continue;
                Vector3 position = root.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                if (position.x < -root.rect.width * .25f && (target == null || position.x < left))
                {
                    target = candidate;
                    left = position.x;
                }
            }
            if (target != null)
            {
                target.sprite = currency.m_Icon;
                target.preserveAspect = true;
            }
        }

        private static string Progress(CharacterOverworld cow, TownExchangeOffer offer)
        {
            if (offer.Slot != "Head" && offer.Slot != "Body" && offer.Slot != "Foot" && offer.Slot != "RightHand" && offer.Slot != "LeftHand")
                return "Accessory: no set progress";
            int owned = 0;
            bool alreadyOwned = false;
            string missing = String.Empty;
            string missingAfter = String.Empty;
            for (int i = 0; i < offers.Length; i++)
            {
                TownExchangeOffer other = offers[i];
                if (other.Family != offer.Family || (other.Slot != "Head" && other.Slot != "Body" && other.Slot != "Foot")) continue;
                bool has = OwnedCount(cow, other) > 0;
                if (has) owned++;
                else
                {
                    missing += (missing.Length == 0 ? "" : ", ") + other.Name;
                    if (other.Item != offer.Item || (offer.Slot != "Head" && offer.Slot != "Body" && offer.Slot != "Foot"))
                        missingAfter += (missingAfter.Length == 0 ? "" : ", ") + other.Name;
                }
                if (other.Item == offer.Item) alreadyOwned = has;
            }
            if (offer.Slot == "Head" || offer.Slot == "Body" || offer.Slot == "Foot")
                return "Owned armor: " + owned + "/3, after purchase " + Math.Min(3, owned + (alreadyOwned ? 0 : 1)) +
                    "/3. " + (missingAfter.Length == 0 ? "All armor pieces owned after purchase." : "Missing after purchase: " + missingAfter + ".") + " Equip for effects.";
            return "Owned armor: " + owned + "/3. " + (missing.Length == 0 ? "All armor pieces owned." : "Missing: " + missing + ".") +
                " Equip matching armament for completion.";
        }

        private static PlayerInventory.ContainerID SlotId(string slot)
        {
            if (slot == "Head") return PlayerInventory.ContainerID.Head;
            if (slot == "Body") return PlayerInventory.ContainerID.Body;
            if (slot == "Foot") return PlayerInventory.ContainerID.Foot;
            if (slot == "RightHand") return PlayerInventory.ContainerID.RightHand;
            if (slot == "Trinket") return PlayerInventory.ContainerID.Trinket;
            if (slot == "Neck") return PlayerInventory.ContainerID.Neck;
            return PlayerInventory.ContainerID.LeftHand;
        }

        private static TownExchangeOffer FindOffer(FTK_itembase.ID item)
        {
            for (int i = 0; i < offers.Length; i++)
                if (offers[i].Item == item) return offers[i];
            return null;
        }

        private static bool IsExchangeShop(uiBuyMenuHud shop)
        {
            return shop != null && exchangeShop == shop && shop.isActiveAndEnabled;
        }

        private static void Show(uiTownServiceMenu current)
        {
            CharacterOverworld cow = Buyer(current);
            uiBuyMenuHud shop = uiBuyMenuHud.Instance;
            uiLocationMenuDisplay location = uiLocationMenuDisplay.Instance;
            if (cow == null || entry == null || shop == null || location == null || openingShop) return;
            openingShop = true;
            location.StartCoroutine(OpenShopAfterServices(current, shop, cow, location));
        }

        private static IEnumerator OpenShopAfterServices(uiTownServiceMenu current, uiBuyMenuHud shop,
            CharacterOverworld cow, uiLocationMenuDisplay location)
        {
            current.DisableMenu();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (location != null && location.IsShowing() &&
                (location.m_SubMenu != null || location.m_FSM.ActiveStateName != "Showing"))
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    openingShop = false;
                    Debug.LogError("Town exchange could not return from Services to the town menu.");
                    yield break;
                }
                yield return null;
            }
            // Let the town menu finish its queued focus callback before the shop takes focus.
            yield return new WaitForEndOfFrame();
            if (location == null || !location.IsShowing() || !PhotonNetwork.offlineMode ||
                location.m_SubMenu != null || location.m_FSM.ActiveStateName != "Showing" ||
                cow == null || GameLogic.Instance == null || GameLogic.Instance.GetCurrentCOW() != cow ||
                cow.m_HexLand == null || current.m_Town == null || cow.m_HexLand != current.m_Town.m_HexLand)
            {
                openingShop = false;
                yield break;
            }
            exchangeShop = shop;
            sellTabWasActive = shop.m_SellTab != null && shop.m_SellTab.gameObject.activeSelf;
            goldLabelWas = shop.m_ShopPlayerGoldValue == null ? null : shop.m_ShopPlayerGoldValue.text;
            try { shop.EnableMenu("Back Alley", null, current.m_Town, cow.m_FTKPlayerID); }
            catch (Exception error)
            {
                ShopHidden(shop);
                openingShop = false;
                Debug.LogError("Town exchange could not open the native shop: " + error);
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 8f;
            while (location != null && location.IsShowing() && !shop.m_DisplayRoot.gameObject.activeInHierarchy)
            {
                if (Time.realtimeSinceStartup > deadline) break;
                yield return null;
            }
            if (shop.m_DisplayRoot == null || !shop.m_DisplayRoot.gameObject.activeInHierarchy)
            {
                ShopHidden(shop);
                Debug.LogError("Town exchange shop did not appear after the native submenu transition.");
            }
            openingShop = false;
        }

        private static void Confirm(uiBuyMenuHud shop, TownExchangeOffer offer)
        {
            if (!IsExchangeShop(shop) || offer == null || ShopBuyer() == null) return;
            CharacterOverworld cow = shop.m_CurrentCow;
            if (Count(cow, token) < 1 || !Eligible(cow, offer)) return;
            string nonce = Guid.NewGuid().ToString("N");
            uiSystemDialog.Instance.Show("Back Alley", "Exchange one " + TokenName() + " for " +
                offer.Name + "?\nOwned: " + OwnedCount(cow, offer) + ". " + Progress(cow, offer),
                "No refunds", "Exchange", "Cancel", new ContinueFSM((Action)delegate
                {
                    if (!IsExchangeShop(shop)) return;
                    if (!Buy(menu, offer, nonce))
                    {
                        uiSystemDialog.Instance.Show("Exchange failed", transaction.RecoveryFailed
                            ? "Inventory recovery failed. Reload the saved game before trading again."
                            : "No purchase was recorded.", "", "OK", null);
                    }
                    shop.m_IsStockListChanged = true;
                    shop.m_IsStockListFullRefresh = true;
                }), null, false);
        }

        internal static ItemContainer DisplayStock(uiBuyMenuHud shop, ItemContainer original)
        {
            if (!IsExchangeShop(shop)) return original;
            RefreshDisplayStock(ShopBuyer(), shop);
            return displayStock;
        }

        internal static bool HandleBuy(uiBuyMenuHud shop)
        {
            if (!IsExchangeShop(shop)) return false;
            if (shop.m_CurrentItem != null) Confirm(shop, FindOffer(shop.m_CurrentItem.m_ItemName));
            return true;
        }

        internal static bool TryGetCost(uiBuyMenuHud shop, FTK_itembase.ID item, out int cost)
        {
            cost = 0;
            return IsExchangeShop(shop) && FindOffer(item) != null;
        }

        internal static bool TryGetStock(uiBuyMenuHud shop, FTK_itembase.ID item, out int count)
        {
            count = 0;
            if (!IsExchangeShop(shop) || FindOffer(item) == null) return false;
            CharacterOverworld cow = ShopBuyer();
            count = cow != null && !transaction.RecoveryFailed && Eligible(cow, FindOffer(item)) && Count(cow, token) > 0 ? 1 : 0;
            return true;
        }

        internal static void SetAppearance(uiBuyMenuHud shop, uiItemIcon icon)
        {
            if (!IsExchangeShop(shop) || icon == null || FindOffer(icon.m_ItemName) == null) return;
            icon.m_CostText.text = "1";
            int available;
            TryGetStock(shop, icon.m_ItemName, out available);
            icon.m_CountText.text = available.ToString();
            icon.m_CostText.color = Color.white;
            icon.m_NameText.color = Color.white;
        }

        internal static void RefreshShop(uiBuyMenuHud shop)
        {
            if (!IsExchangeShop(shop)) return;
            CharacterOverworld cow = ShopBuyer();
            RefreshDisplayStock(cow, shop);
            if (cow == null) return;
            shop.m_HeaderTitle.text = "Back Alley | " + TokenName();
            string balance = Count(cow, token).ToString();
            shop.m_PlayerGoldText.text = balance;
            if (shop.m_ShopPlayerGoldValue != null) shop.m_ShopPlayerGoldValue.text = balance;
            if (currencyIcon == null) ReplaceCurrencyIcon(shop);
            if (shop.m_SellTab != null) shop.m_SellTab.gameObject.SetActive(false);
        }

        private static void ReplaceCurrencyIcon(uiBuyMenuHud shop)
        {
            FTK_itembase row = FTK_itembase.GetItemBase(token);
            if (row == null || row.m_Icon == null || shop.m_ShopPlayerGoldValue == null) return;
            RectTransform value = shop.m_ShopPlayerGoldValue.transform as RectTransform;
            if (value == null || value.parent == null) return;
            Image best = null;
            float distance = float.MaxValue;
            foreach (Image candidate in value.parent.GetComponentsInChildren<Image>(true))
            {
                RectTransform rect = candidate.transform as RectTransform;
                if (rect == null || candidate.sprite == null || rect.rect.width < 14f ||
                    rect.rect.width > 48f || rect.rect.height < 14f || rect.rect.height > 48f) continue;
                Vector3 center = value.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                if (center.x < 0f || Math.Abs(center.y) > 28f || center.x >= distance) continue;
                best = candidate;
                distance = center.x;
            }
            if (best == null) return;
            currencyIcon = best;
            currencyIconWas = best.sprite;
            best.sprite = row.m_Icon;
            best.preserveAspect = true;
        }

        internal static void ShopHidden(uiBuyMenuHud shop)
        {
            if (exchangeShop != shop) return;
            if (shop.m_SellTab != null) shop.m_SellTab.gameObject.SetActive(sellTabWasActive);
            if (shop.m_ShopPlayerGoldValue != null) shop.m_ShopPlayerGoldValue.text = goldLabelWas;
            if (currencyIcon != null) currencyIcon.sprite = currencyIconWas;
            currencyIcon = null;
            currencyIconWas = null;
            exchangeShop = null;
        }

        internal static void LocationShutdown(uiBuyMenuHud shop)
        {
            if (shop == null || exchangeShop != shop) return;
            shop.Hide();
            ShopHidden(shop);
        }

        internal static bool ShowBuyOnly(uiPopupMenu popup)
        {
            if (!IsExchangeShop(uiBuyMenuHud.Instance)) return false;
            MethodInfo showButton = AccessTools.Method(typeof(uiPopupMenu), "ShowButton");
            if (showButton == null) return false;
            CharacterOverworld cow = ShopBuyer();
            showButton.Invoke(popup, new object[] { uiPopupMenu.Action.Buy, cow != null &&
                Count(cow, token) > 0, null, null });
            return true;
        }

        internal static bool CloseOverlay(uiTownServiceMenu current) { return false; }

        internal static void Refresh(uiTownServiceMenu current)
        {
            if (token == FTK_itembase.ID.None || offers.Length == 0 || current == null) return;
            try { refreshStage = "start"; RefreshCore(current); }
            catch (Exception error) { Debug.LogError("Town exchange menu entry failed at " + refreshStage + ": " + error); }
        }

        private static void RefreshCore(uiTownServiceMenu current)
        {
            if (menu != current)
            {
                if (menu != null) Hide(menu);
                entry = null;
                menu = current;
            }
            if (entry == null)
            {
                refreshStage = "clone native service row";
                RectTransform template = null;
                foreach (uiTownServiceMenu.ServiceButton service in current.m_ServiceButtons)
                {
                    if (service == null || service.m_RectTransform == null) continue;
                    if (template == null) template = service.m_RectTransform;
                    Button source = service.m_RectTransform.GetComponent<Button>();
                    if (source != null && source.interactable && service.m_RectTransform.gameObject.activeSelf)
                    {
                        template = service.m_RectTransform;
                        break;
                    }
                }
                if (template == null) return;
                GameObject row = UnityEngine.Object.Instantiate(template.gameObject);
                row.name = "Back Alley";
                row.transform.SetParent(template.parent, false);
                row.transform.SetAsLastSibling();
                entry = row.GetComponent<Button>();
                if (entry == null) { UnityEngine.Object.Destroy(row); return; }
                entry.onClick = new Button.ButtonClickedEvent();
                entry.onClick.AddListener(() => Show(current));
                Transform cost = row.transform.Find("CostIcon");
                if (cost != null) cost.gameObject.SetActive(false);
                ReplaceServiceIcon(row);
                foreach (Text label in row.GetComponentsInChildren<Text>(true))
                {
                    if (label.transform.parent == row.transform) label.text = "Back Alley";
                    else if (label.name == "Text" && label.transform.parent != null &&
                             label.transform.parent.name.StartsWith("Content", StringComparison.Ordinal))
                        label.text = "Exchange one owned " + TokenName() + " for a chosen item.";
                    label.color = Color.white;
                    label.fontStyle = FontStyle.Normal;
                }
            }
            refreshStage = "update native row";
            entry.gameObject.SetActive(current.isActiveAndEnabled && current.m_Town is MiniHexTown);
            Transform title = entry.transform.Find("Text");
            Text entryLabel = title == null ? null : title.GetComponent<Text>();
            if (entryLabel != null)
                entryLabel.text = PhotonNetwork.offlineMode ? "Back Alley" : "Back Alley (solo exchange only)";
            refreshStage = "validate buyer";
            entry.interactable = Buyer(current) != null;
            if (entry.interactable && current.m_ServiceButtons != null)
            {
                refreshStage = "link navigation";
                RectTransform last = null;
                foreach (uiTownServiceMenu.ServiceButton service in current.m_ServiceButtons)
                    if (service != null && service.m_RectTransform != null && service.m_RectTransform.gameObject.activeSelf &&
                        service.m_RectTransform.GetComponent<Button>() != null && service.m_RectTransform.GetComponent<Button>().interactable)
                        last = service.m_RectTransform;
                if (last != null) uiItemIcon.SetNavigatePairUpDown(last, (RectTransform)entry.transform);
            }
        }

        internal static void Hide(uiTownServiceMenu current)
        {
            if (menu != current) return;
            if (entry != null) entry.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(uiTownServiceMenu), "EnableMenu", new Type[] { typeof(string), typeof(ContinueFSM), typeof(MiniHexInfo), typeof(FTKPlayerID) })]
    internal static class TownExchangeEnablePatch
    {
        private static void Postfix(uiTownServiceMenu __instance) { TownExchangeService.Refresh(__instance); }
    }

    [HarmonyPatch(typeof(uiTownServiceMenu), "RefreshServicesButtons")]
    internal static class TownExchangeRefreshPatch
    {
        private static void Postfix(uiTownServiceMenu __instance) { TownExchangeService.Refresh(__instance); }
    }

    [HarmonyPatch(typeof(GeneralMenuBase), "DisableMenu")]
    internal static class TownExchangeDisablePatch
    {
        private static bool Prefix(GeneralMenuBase __instance)
        {
            uiTownServiceMenu town = __instance as uiTownServiceMenu;
            if (town == null) return true;
            if (TownExchangeService.CloseOverlay(town)) return false;
            TownExchangeService.Hide(town);
            return true;
        }
    }

    [HarmonyPatch(typeof(GeneralMenuBase), "Hide")]
    internal static class TownExchangeHidePatch
    {
        private static void Prefix(GeneralMenuBase __instance)
        {
            uiTownServiceMenu town = __instance as uiTownServiceMenu;
            if (town != null) TownExchangeService.Hide(town);
        }
    }

    [HarmonyPatch(typeof(uiItemContainerBase), "GenerateListChild")]
    internal static class TownExchangeNativeListPatch
    {
        private static void Prefix(uiItemContainerBase __instance, ref ItemContainer _itemContainer)
        {
            uiBuyMenuHud shop = __instance as uiBuyMenuHud;
            if (shop != null) _itemContainer = TownExchangeService.DisplayStock(shop, _itemContainer);
        }
    }

    [HarmonyPatch(typeof(uiItemContainerBase), "LiveUpdateList")]
    internal static class TownExchangeNativeLiveListPatch
    {
        private static void Prefix(uiItemContainerBase __instance, ref ItemContainer _itemContainer)
        {
            uiBuyMenuHud shop = __instance as uiBuyMenuHud;
            if (shop != null) _itemContainer = TownExchangeService.DisplayStock(shop, _itemContainer);
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "SetItemAppearance")]
    internal static class TownExchangeNativeAppearancePatch
    {
        private static void Postfix(uiBuyMenuHud __instance, uiItemIcon _itemIcon)
        {
            TownExchangeService.SetAppearance(__instance, _itemIcon);
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "GetShopItemCost")]
    internal static class TownExchangeNativeCostPatch
    {
        private static bool Prefix(uiBuyMenuHud __instance, FTK_itembase.ID _item, ref int __result)
        {
            int cost;
            if (!TownExchangeService.TryGetCost(__instance, _item, out cost)) return true;
            __result = cost;
            return false;
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "GetItemCountInStock")]
    internal static class TownExchangeNativeStockPatch
    {
        private static bool Prefix(uiBuyMenuHud __instance, FTK_itembase.ID _item, ref int __result)
        {
            int count;
            if (!TownExchangeService.TryGetStock(__instance, _item, out count)) return true;
            __result = count;
            return false;
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "BuyCurrentItem")]
    internal static class TownExchangeNativeBuyPatch
    {
        private static bool Prefix(uiBuyMenuHud __instance)
        {
            return !TownExchangeService.HandleBuy(__instance);
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "BuyAndEquipCurrentItem")]
    internal static class TownExchangeNativeBuyEquipPatch
    {
        private static bool Prefix(uiBuyMenuHud __instance)
        {
            return !TownExchangeService.HandleBuy(__instance);
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "Update")]
    internal static class TownExchangeNativeUpdatePatch
    {
        private static void Postfix(uiBuyMenuHud __instance)
        {
            TownExchangeService.RefreshShop(__instance);
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "Hide")]
    internal static class TownExchangeNativeHidePatch
    {
        private static void Postfix(uiBuyMenuHud __instance)
        {
            TownExchangeService.ShopHidden(__instance);
        }
    }

    [HarmonyPatch(typeof(uiPopupMenu), "ShowBuyMenu")]
    internal static class TownExchangeNativePopupPatch
    {
        private static bool Prefix(uiPopupMenu __instance)
        {
            return !TownExchangeService.ShowBuyOnly(__instance);
        }
    }

    [HarmonyPatch(typeof(uiLocationMenuDisplay), "Shutdown2")]
    internal static class TownExchangeLocationShutdownPatch
    {
        private static void Postfix()
        {
            TownExchangeService.LocationShutdown(uiBuyMenuHud.Instance);
        }
    }
}
