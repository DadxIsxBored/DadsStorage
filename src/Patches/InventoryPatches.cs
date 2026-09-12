namespace DadsStorage.Patches;

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
static class InventoryGridGetHoveredElementPatch
{
    static void Prefix(InventoryGrid __instance)
    {
        bool flag = __instance.m_uiGroup.IsActive && ZInput.IsGamepadActive();
        InventoryElement element = flag
            ? __instance.GetElement(__instance.m_selected.x, __instance.m_selected.y, __instance.m_inventory.GetWidth())
            : __instance.GetHoveredElement();
        if (element == null) return;


        if (SingleItemShortcut.Value.IsKeyDown())
        {
            ItemDrop.ItemData? item = __instance.m_inventory.GetItemAt(element.Position.x, element.Position.y);
            if (item == null) return;
            if (__instance.m_inventory == null) return;
            Functions.TryStoreThisItem(item, __instance.m_inventory);
        }
    }
}
