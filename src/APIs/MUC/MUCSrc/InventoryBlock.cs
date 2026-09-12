using System.Runtime.CompilerServices;

namespace DadsStorage.APIs.MUC.MUCSrc;

public class InventoryBlock {
    private static readonly ConditionalWeakTable<Inventory, InventoryBlock> Inventories = new ConditionalWeakTable<Inventory, InventoryBlock>();

    private const float BlockTimeout = 30f;

    private bool blockConsume;
    private float blockConsumeAt;

    public bool BlockConsume {
        get => blockConsume && Time.time - blockConsumeAt < BlockTimeout;
        set {
            blockConsume = value;
            blockConsumeAt = Time.time;
        }
    }

    public bool BlockAllSlots { get; set; }

    public Dictionary<Vector2i, int> BlockedSlots { get; } = new Dictionary<Vector2i, int>();

    private readonly Dictionary<Vector2i, float> blockedAt = new Dictionary<Vector2i, float>();
    private readonly List<Vector2i> expired = new List<Vector2i>();

    private static readonly ConditionalWeakTable<Inventory, InventoryBlock>.CreateValueCallback CreateBlock = _ => new InventoryBlock();

    public static InventoryBlock Get(Inventory inventory) {
        return Inventories.GetValue(inventory, CreateBlock);
    }

    public void BlockSlot(Vector2i slot) {
        if (!CanBlockSlot(slot)) {
            return;
        }

        if (BlockedSlots.ContainsKey(slot)) {
            BlockedSlots[slot]++;
        } else {
            BlockedSlots[slot] = 1;
        }

        blockedAt[slot] = Time.time;
    }

    public void ReleaseSlot(Vector2i slot) {
        if (!CanBlockSlot(slot)) {
            return;
        }

        if (!BlockedSlots.ContainsKey(slot)) {
            return;
        }

        BlockedSlots[slot]--;

        if (BlockedSlots[slot] <= 0) {
            BlockedSlots.Remove(slot);
            blockedAt.Remove(slot);
        }
    }

    public void ReleaseBlockedSlots() {
        BlockedSlots.Clear();
        blockedAt.Clear();
        expired.Clear();
        BlockAllSlots = false;
        BlockConsume = false;
    }

    public bool IsSlotBlocked(Vector2i slot) {
        if (BlockConsume || BlockAllSlots) {
            return true;
        }

        if (!BlockedSlots.ContainsKey(slot)) {
            return false;
        }

        if (HasExpired(slot)) {
            BlockedSlots.Remove(slot);
            blockedAt.Remove(slot);
            return false;
        }

        return true;
    }

    public bool IsAnySlotBlocked() {
        if (BlockConsume || BlockAllSlots) {
            return true;
        }

        DropExpired();
        return BlockedSlots.Count > 0;
    }

    private bool HasExpired(Vector2i slot) {
        return !blockedAt.TryGetValue(slot, out float at) || Time.time - at >= BlockTimeout;
    }

    private void DropExpired() {
        if (BlockedSlots.Count == 0) {
            return;
        }

        expired.Clear();

        foreach (KeyValuePair<Vector2i, int> pair in BlockedSlots) {
            if (HasExpired(pair.Key)) {
                expired.Add(pair.Key);
            }
        }

        for (int i = 0; i < expired.Count; ++i) {
            BlockedSlots.Remove(expired[i]);
            blockedAt.Remove(expired[i]);
        }

        expired.Clear();
    }

    private static bool CanBlockSlot(Vector2i slot) {
        return slot.x >= 0 && slot.y >= 0;
    }
}
