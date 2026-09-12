namespace DadsStorage.Interfaces;

public class VanillaContainers(Container _container) : IContainer
{
	public GameObject gameObject => _container.gameObject;
	public ZNetView m_nview => _container.m_nview;

	public static bool CantStoreFavorite(ItemDrop.ItemData item, UserConfig playerConfig)
	{
		return playerConfig.IsItemNameOrSlotFavorited(item);
	}

	internal static void LogDebug(string data)
	{
		DadsStorageLogger.LogDebug(data);
	}

	private static List<ItemDrop.ItemData>? QuickSlots()
	{
		return DadsEPICompat.IsLoaded() ? DadsEPICompat.GetQuickSlotsItems() : null;
	}

	private static bool ShouldSkip(ItemDrop.ItemData item, List<ItemDrop.ItemData>? quickSlots)
	{
		if (item.m_equipped)
		{
			LogDebug($"Skipping equipped item {item.m_dropPrefab.name}");
			return true;
		}

		if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 && PlayerIgnoreHotbar.Value.IsOn())
		{
			LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in the hotbar");
			return true;
		}

		if (quickSlots != null)
		{
			for (int i = 0; i < quickSlots.Count; ++i)
			{
				if (quickSlots[i].m_gridPos != item.m_gridPos) continue;

				LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in your quick slots");
				return true;
			}
		}

		if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
		{
			LogDebug($"Skipping favorited item/slot {item.m_dropPrefab.name}");
			return true;
		}

		return false;
	}

	private int Commit(ItemDrop.ItemData item, Inventory inv, int originalAmount)
	{
		int moved = originalAmount - item.m_stack;
		if (moved <= 0) return 0;

		if (item.m_stack > 0)
			inv.Changed();
		else if (inv.ContainsItem(item))
			inv.RemoveItem(item);

		LogDebug($"Stored {moved} {item.m_dropPrefab.name} into {_container.name}");

		if (!Boxes.ContainersToPing.Contains(this))
			Boxes.ContainersToPing.Add(this);

		return moved;
	}

	public int TryStore()
	{
		if (!Player.m_localPlayer) return 0;

		int total = 0;
		Inventory? inv = Player.m_localPlayer.GetInventory();
		List<ItemDrop.ItemData>? items = inv.GetAllItems();
		List<ItemDrop.ItemData>? quickSlots = QuickSlots();

		for (int j = items.Count - 1; j >= 0; --j)
		{
			ItemDrop.ItemData? item = items[j];
			if (ShouldSkip(item, quickSlots)) continue;

			LogDebug($"Checking item {item.m_dropPrefab.name}");
			int originalAmount = item.m_stack;

			if (!Functions.TryStore(_container, ref item, fromPlayer: true))
				continue;

			total += Commit(item, inv, originalAmount);
		}

		return total;
	}

	public int TryStoreThisItem(ItemDrop.ItemData? singleItemData = null, Inventory? playerInventory = null)
	{
		if (!Player.m_localPlayer) return 0;
		if (singleItemData == null) return 0;

		Inventory inv = playerInventory ?? Player.m_localPlayer.GetInventory();
		ItemDrop.ItemData item = singleItemData;

		if (ShouldSkip(item, QuickSlots())) return 0;

		LogDebug($"Checking item {item.m_dropPrefab.name}");
		int originalAmount = item.m_stack;

		if (!Functions.TryStore(_container, ref item, fromPlayer: true, singleItemData: true))
			return 0;

		return Commit(item, inv, originalAmount);
	}

	public bool IsOwner() => _container.m_nview.IsOwner();

	public Inventory GetInventory()
	{
		return _container.GetInventory();
	}

	public static VanillaContainers Create(Container container) => new(container);
}
