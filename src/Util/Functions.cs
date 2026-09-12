using DadsStorage.APIs.MUC;
using DadsStorage.APIs.MUC.MUCSrc.Helper;
using DadsStorage.Patches;
using Object = UnityEngine.Object;

namespace DadsStorage.Util;

public class Functions
{
    public static void LogContainerStatus(Container container)
    {
        try
        {
            LogIfBuildDebug($"Container {container.name} at {container.transform.position} is {(container.m_nview.IsOwner() ? "owned" : "not owned")} and has {(container.GetInventory()?.NrOfItems() ?? 0)} items.");
        }
        catch
        {
            // Literally don't give a fuck if this fails. But try anyways.
        }
    }

    private static readonly Dictionary<string, float> RangeByPrefab = new();

    internal static void ClearRangeCache() => RangeByPrefab.Clear();

    internal static float GetContainerRange(Container container)
    {
        if (yamlData == null)
        {
            DadsStorageLogger.LogError("yamlData is null when trying to get the container range for a container. Make sure that your YAML file is not empty or to call DeserializeYamlFile() before using GetContainerRange.");
            return -1f;
        }

        if (container.GetInventory() == null)
            return -1f;

        // Try to get container settings from YAML configuration
        string containerName = MiscFunctions.GetPrefabName(container.transform.root.name);

        if (RangeByPrefab.TryGetValue(containerName, out float cached))
            return float.IsNaN(cached) ? FallbackRange.Value : cached;

        if (yamlData.TryGetValue(containerName, out object containerData))
        {
            if (containerData is Dictionary<object, object> containerInfo)
            {
                object? rangeObj = null;
                foreach (KeyValuePair<object, object> kv in containerInfo)
                {
                    if (string.Equals(kv.Key?.ToString(), "range", StringComparison.OrdinalIgnoreCase))
                    {
                        rangeObj = kv.Value;
                        break;
                    }
                }

                if (rangeObj != null && float.TryParse(rangeObj.ToString(), out float range))
                {
                    RangeByPrefab[containerName] = range;
                    return range;
                }
            }
            else
            {
                DadsStorageLogger.LogError($"Unable to cast containerData for container '{containerName}' to Dictionary<object, object>.");
                return -1f;
            }
        }

        RangeByPrefab[containerName] = float.NaN;
        return FallbackRange.Value;
    }

    private readonly struct NearbyContainer
    {
        internal readonly Container Container;
        internal readonly Vector3 Position;
        internal readonly float RangeSq;

        internal NearbyContainer(Container container, Vector3 position, float rangeSq)
        {
            Container = container;
            Position = position;
            RangeSq = rangeSq;
        }
    }

    private static readonly List<NearbyContainer> Nearby = new();

    internal static void BuildNearbyContainers()
    {
        Nearby.Clear();
        Boxes.PruneDestroyed();

        List<Container> containers = Boxes.Containers;

        for (int i = 0; i < containers.Count; ++i)
        {
            Container container = containers[i];
            if (container.m_nview == null || !container.m_nview.IsValid()) continue;

            float range = GetContainerRange(container);
            if (range < 0f) continue;

            Nearby.Add(new NearbyContainer(container, container.transform.position, range * range));
        }
    }

    internal static void ClearNearbyContainers() => Nearby.Clear();

    private static bool SatLongEnoughToAskFor(ItemDrop itemDrop)
    {
		// Literally here just so I don't spam their logs trying to request to pickup something.
        if (ZNet.instance == null) return false;

        long ticks = itemDrop.m_nview.GetZDO().GetLong(ZDOVars.s_spawnTime);
        if (ticks <= 0L || ticks > DateTime.MaxValue.Ticks) return false;

        double age = (ZNet.instance.GetTime() - new DateTime(ticks)).TotalSeconds;
        return age >= Math.Max(15f, IntervalSeconds.Value * 2f);
    }

    private static bool AnyContainerWants(ItemDrop itemDrop)
    {
        ItemDrop.ItemData item = itemDrop.m_itemData;
        long playerId = Game.instance.GetPlayerProfile().GetPlayerID();

        Vector3 dropPos = itemDrop.transform.position;

        for (int i = 0; i < Nearby.Count; ++i)
        {
            NearbyContainer near = Nearby[i];
            Container container = near.Container;
            if (!container || !container.transform) continue;
            if (container.m_nview == null || !container.m_nview.IsValid()) continue;
            if ((near.Position - dropPos).sqrMagnitude > near.RangeSq) continue;

            Inventory? inv = container.GetInventory();
            if (inv == null) continue;

            if (container.m_nview.GetZDO().GetBool(ContainerAwakePatch.storingPausedHash, false)) continue;
            if (MustHaveExistingItemToPull.Value.IsOn() && !inv.HaveItem(item.m_shared.m_name)) continue;
            if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(container.transform.root.name), item.m_dropPrefab.name)) continue;
            if (!container.CheckAccess(playerId)) continue;
            if (container.IsInUse() || container.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) != 0) continue;
            if (InventoryMove.HowMuchFits(inv, item) <= 0) continue;

            return true;
        }

        return false;
    }

    internal static void CheckItemDropInstanceAndStore(ItemDrop itemDrop)
    {
        if (Boxes.Containers == null || itemDrop == null || itemDrop.transform == null)
            return;


        if (ChestsPickupFromGround.Value.IsOff()) return;
        ZNetView? nview = itemDrop.m_nview;
        if (!nview || !nview.IsValid()) return;
        // Check if the itemdrop is a Fish and if it's not out of water before trying to store it.
        if (!itemDrop.m_itemData.m_dropPrefab) return;
        if (itemDrop.m_itemData.m_dropPrefab.TryGetComponent<Fish>(out Fish? fish))
        {
            if (!fish.IsOutOfWater() && !FishSuction.Value.IsOn()) return;
        }

        ZDO? zdo = nview.GetZDO();
        if (zdo == null) return;

        if (!nview.IsOwner())
        {
            if (nview.HasOwner() && !SatLongEnoughToAskFor(itemDrop)) return;

            if (!AnyContainerWants(itemDrop)) return;

            if (nview.HasOwner())
            {
                try
                {
                    itemDrop.RequestOwn();
                }
                catch
                {
                    /* ignore */
                }

                return;
            }

            try
            {
                nview.ClaimOwnership();
            }
            catch
            {
                /* ignore */
            }

            if (!nview.IsOwner()) return;
        }

        bool anyChanged = false;
        Vector3 dropPosition = itemDrop.transform.position;

        for (int i = 0; i < Nearby.Count; ++i)
        {
            NearbyContainer near = Nearby[i];
            Container container = near.Container;
            if (!container || !container.transform || container.GetInventory() == null) continue;
            if (container.m_nview == null || !container.m_nview.IsValid()) continue;

            if ((near.Position - dropPosition).sqrMagnitude > near.RangeSq) continue;

            // Pause flag
            bool isPaused = container.m_nview.GetZDO().GetBool(ContainerAwakePatch.storingPausedHash, false);
            if (isPaused) continue;

            if (!container.m_nview.IsOwner())
            {
                if (container.m_nview.HasOwner() || IsContainerBeingUsed(container) || SharesRootZDO(container))
                    continue;

                container.m_nview.ClaimOwnership();
                if (!container.m_nview.IsOwner())
                    continue;
            }

            if (container.IsInUse() || container.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) != 0)
                continue;

            LogDebug($"Nearby item name: {itemDrop.m_itemData.m_dropPrefab.name}");

            if (!TryStore(container, ref itemDrop.m_itemData))
                continue;

            anyChanged = true;

            if (itemDrop.m_itemData.m_stack <= 0)
                break;
        }

        if (!anyChanged) return;
        itemDrop.Save();

        if (itemDrop.m_itemData.m_stack > 0) return;
        if (!itemDrop.m_nview)
            Object.DestroyImmediate(itemDrop.gameObject);
        else
            ZNetScene.instance.Destroy(itemDrop.gameObject);
    }

    internal static bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false, bool singleItemData = false)
    {
        bool changed = false;

        LogIfBuildDebug($"Checking container {nearbyContainer?.name}");
        if (!nearbyContainer || !MiscFunctions.CheckItemSharedIntegrity(item))
            return false;

        Inventory? target = nearbyContainer.GetInventory();
        if (target == null) return false;

        if (MustHaveExistingItemToPull.Value.IsOn() && !target.HaveItem(item.m_shared.m_name))
        {
            if (singleItemData)
            {
                LogDebug($"Skipping {item.m_dropPrefab?.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{item.m_dropPrefab?.name}] is not in nearby containers</color>");
            }

            return false;
        }

        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(nearbyContainer.transform.root.name), item.m_dropPrefab?.name ?? ""))
        {
            if (singleItemData)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{item.m_dropPrefab?.name}] cannot be stored based on configuration settings</color>");
            }

            LogDebug($"{item.m_shared.m_name} cannot be stored based on configuration setting");
            return false;
        }

        if (!nearbyContainer.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID()))
        {
            LogDebug($"Cannot store items in {nearbyContainer.name} because the player does not have access.");
            return false;
        }

		int moved;

		if (nearbyContainer.m_nview.IsOwner())
		{
			moved = InventoryMove.MoveStack(target, item);
		}
		else
		{
			moved = TryStoreToOtherOwner(nearbyContainer, item);
			if (moved <= 0)
			{
				LogDebug($"Cannot store items in {nearbyContainer.name}: not the owner and nothing to route the write through.");
				return false;
			}
		}

		changed = moved > 0;

        if (!changed) return changed;

        if (!fromPlayer)
            PingContainer(nearbyContainer.gameObject);

        // Vanilla's OnContainerChanged does the Save, and only when we actually own the thing.
        return changed;
    }


    private static readonly Dictionary<Inventory, Inventory> PendingSends = new();

    private static Inventory PendingView(Inventory target)
    {
        if (PendingSends.TryGetValue(target, out Inventory view))
            return view;

        view = InventoryHelper.CopyInventory(target);
        PendingSends[target] = view;
        return view;
    }

    internal static void ClearPendingSends() => PendingSends.Clear();

    internal static int TryStoreToOtherOwner(Container container, ItemDrop.ItemData item)
    {
        if (!MUCCompat.CanRouteToOtherOwner) return 0;
        if (!container.m_nview.HasOwner()) return 0;
        if (!Player.m_localPlayer) return 0;

        Inventory source = Player.m_localPlayer.GetInventory();
        if (source == null || !source.ContainsItem(item)) return 0;

        Inventory? target = container.GetInventory();
        if (target == null) return 0;

        Inventory view = PendingView(target);
        int amount = InventoryMove.HowMuchFits(view, item);
        if (amount <= 0) return 0;

        int before = item.m_stack;

        target.AddItem(item, amount, -1, -1);

        int committed;

        if (!source.ContainsItem(item))
        {
            item.m_stack = 0;
            committed = before;
        }
        else
        {
            committed = before - item.m_stack;
        }

        if (committed > 0)
        {
            ItemDrop.ItemData reserved = item.Clone();
            reserved.m_stack = committed;
            InventoryMove.MoveStack(view, reserved);
        }

        return committed;
    }

    internal static bool SharesRootZDO(Container container) => container.m_rootObjectOverride;

    internal static bool IsContainerBeingUsed(Container container)
    {
        if (!container.m_nview.IsValid()) return false;
        if (container.m_nview.IsOwner())
            return container.IsInUse();
        return container.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) != 0;
    }

    internal static void TryStore()
    {
        if (!Player.m_localPlayer) return;
        LogDebug("Trying to store items from player inventory");

        Boxes.ContainersToPing.Clear();
        ClearPendingSends();

        int total = 0;
        foreach (IContainer container in Boxes.GetNearbyContainers(Player.m_localPlayer, PlayerRange.Value))
            total += StoreToContainer(container, null, null);

        StoreSuccess(total);
    }

    internal static void TryStoreThisItem(ItemDrop.ItemData itemData, Inventory m_inventory)
    {
        if (!Player.m_localPlayer) return;
        if (m_inventory != Player.m_localPlayer.GetInventory()) return;

        LogDebug($"Trying to store {itemData.m_shared.m_name}");

        Boxes.ContainersToPing.Clear();
        ClearPendingSends();

        int total = 0;
        foreach (IContainer container in Boxes.GetNearbyContainers(Player.m_localPlayer, PlayerRange.Value))
            total += StoreToContainer(container, itemData, m_inventory);

        StoreSuccess(total);
    }

    private static int StoreToContainer(IContainer container, ItemDrop.ItemData? singleItem, Inventory? inv)
    {
        if (container is not VanillaContainers vc)
            return singleItem == null ? container.TryStore() : container.TryStoreThisItem(singleItem, inv);

        if (!vc.m_nview.IsValid()) return 0;

        ZDO? zdo = null;

        if (!MUCCompat.CanRouteToOtherOwner)
        {
            Container? c = vc.gameObject.GetComponent<Container>();
            if (c != null && IsContainerBeingUsed(c))
            {
                LogDebug($"Skipping {vc.gameObject.name}: container is in use.");
                return 0;
            }

            Container? rawForClaim = vc.gameObject.GetComponent<Container>();
            if (!vc.IsOwner())
            {
                if (rawForClaim != null && SharesRootZDO(rawForClaim)) return 0;
                vc.m_nview.ClaimOwnership();
            }

            // Mark in-use so concurrent quickstacks from other players skip this container
            zdo = vc.m_nview.GetZDO();
            zdo?.Set(ZDOVars.s_inUse, 1);
            if (zdo != null) ZDOMan.instance.ForceSendZDO(ZNet.GetUID(), zdo.m_uid);
        }

        int moved = 0;
        try
        {
            moved = singleItem == null ? vc.TryStore() : vc.TryStoreThisItem(singleItem, inv);
        }
        catch (Exception e)
        {
            LogError($"Error while storing to {vc.gameObject.name}: {e}");
        }
        finally
        {
            if (zdo != null)
            {
                zdo.Set(ZDOVars.s_inUse, 0);
                ZDOMan.instance.ForceSendZDO(ZNet.GetUID(), zdo.m_uid);
            }
        }

        return moved;
    }


    internal static void StoreSuccess(int total)
    {
        try
        {
            if (total > 0)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Stored {total} items from your inventory into nearby containers");

                foreach (IContainer c in Boxes.ContainersToPing)
                {
                    PingContainer(c.gameObject);
                    try
                    {
                        if (InventoryGui.instance)
                        {
                            Container? raw = c.gameObject.GetComponent<Container>();
                            if (raw != InventoryGui.instance.m_currentContainer)
                                InventoryGui.instance.m_moveItemEffects.Create(c.gameObject.transform.position, Quaternion.identity);
                        }
                    }
                    catch (Exception e)
                    {
                        DadsStorageLogger.LogError($"Error while playing move effect for container {c.gameObject.name}: {e}");
                    }
                }
            }
        }
        finally
        {
            Boxes.ContainersToPing.Clear();
            ClearPendingSends();
        }
    }


    internal static void PingContainer(GameObject container)
    {
        if (container == null) return;
        if (PingContainers.Value.IsOn() && container.GetComponent<ChestPingEffect>() == null)
            container.AddComponent<ChestPingEffect>();

        if (HighlightContainers.Value.IsOn() && container.GetComponent<HighLightChest>() == null)
            container.AddComponent<HighLightChest>();
    }


    internal static void LogDebug(string data)
    {
        DadsStorageLogger.LogDebug(data);
    }

    internal static void LogIfBuildDebug(string data)
    {
#if DEBUG
        DadsStoragePlugin.DadsStorageLogger.LogDebug(data);
#endif
    }

    internal static void LogError(string data)
    {
        DadsStorageLogger.LogError(data);
    }

    internal static void LogInfo(string data)
    {
        DadsStorageLogger.LogInfo(data);
    }

    internal static void LogWarning(string data)
    {
        DadsStorageLogger.LogWarning(data);
    }
}
