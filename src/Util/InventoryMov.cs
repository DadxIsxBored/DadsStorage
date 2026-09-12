namespace DadsStorage.Util;

internal static class InventoryMove
{
	private static int FreeStackSpace(Inventory target, ItemDrop.ItemData item, int max)
	{
		int space = 0;

		foreach (ItemDrop.ItemData slot in target.GetAllItems())
		{
			if (slot.m_shared.m_name != item.m_shared.m_name) continue;
			if (slot.m_quality != item.m_quality) continue;
			if (slot.m_worldLevel != item.m_worldLevel) continue;
			if (slot.m_stack < max)
				space += max - slot.m_stack;
		}

		return space;
	}

	internal static int HowMuchFits(Inventory target, ItemDrop.ItemData item)
	{
		if (target == null || item is not { m_stack: > 0 })
			return 0;

		int max = Mathf.Max(1, item.m_shared.m_maxStackSize);
		int space = FreeStackSpace(target, item, max);

		int emptySlots = target.m_width * target.m_height - target.m_inventory.Count;
		if (emptySlots > 0)
			space += emptySlots * max;

		return Math.Min(item.m_stack, space);
	}

	internal static int MoveStack(Inventory target, ItemDrop.ItemData sourceItem)
	{
		if (target == null || sourceItem is not { m_stack: > 0 })
			return 0;

		int before = sourceItem.m_stack;

		ItemDrop.ItemData copy = sourceItem.Clone();

		int moved = target.AddItem(copy) ? before : before - copy.m_stack;
		sourceItem.m_stack -= moved;
		return moved;
	}
}
