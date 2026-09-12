using System.Reflection;

namespace DadsStorage.Interfaces;

internal static class DadsEPICompat
{
    private const string PluginGuid = "com.dadisbored.dadsepi";
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static bool IsLoaded()
    {
        return Chainloader.PluginInfos.ContainsKey(PluginGuid);
    }

    internal static List<ItemDrop.ItemData> GetQuickSlotsItems()
    {
        List<ItemDrop.ItemData> items = new();
        Player player = Player.m_localPlayer;
        Inventory inventory = player?.GetInventory();
        if (inventory == null || !IsLoaded()) return items;

        Type layout = Type.GetType("DadsEPI.InventoryLayout, DadsEPI");
        PropertyInfo equipmentCountProperty = layout?.GetProperty("EquipmentSlotCount", StaticMembers);
        PropertyInfo quickCountProperty = layout?.GetProperty("EnabledQuickSlots", StaticMembers);
        MethodInfo slotPositionMethod = layout?.GetMethod("SlotPosition", StaticMembers, null, new[] { typeof(int), typeof(int) }, null);
        if (equipmentCountProperty == null || quickCountProperty == null || slotPositionMethod == null) return items;

        try
        {
            int equipmentCount = (int)equipmentCountProperty.GetValue(null);
            int quickCount = (int)quickCountProperty.GetValue(null);
            int width = inventory.GetWidth();
            for (int index = 0; index < quickCount; index++)
            {
                Vector2i position = (Vector2i)slotPositionMethod.Invoke(null, new object[] { equipmentCount + index, width });
                ItemDrop.ItemData item = inventory.GetItemAt(position.x, position.y);
                if (item != null) items.Add(item);
            }
        }
        catch (Exception exception)
        {
            DadsStorageLogger.LogWarning($"DadsEPI quick-slot lookup failed: {exception.Message}");
        }

        return items;
    }
}
