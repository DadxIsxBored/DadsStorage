using Object = UnityEngine.Object;

namespace DadsStorage.Managers;

[HarmonyPatch(typeof(Game), nameof(Game.Start))]
public static class GameStartPatch
{
    static void Postfix()
    {
        if (DadsStorageItemDropManager.Instance) return;
        DadsStorageItemDropManager? itemDropManager = new GameObject("DadsStorage_ItemDropManager").AddComponent<DadsStorageItemDropManager>();
        Object.DontDestroyOnLoad(itemDropManager);
    }
}

public class DadsStorageItemDropManager : MonoBehaviour
{
    internal static DadsStorageItemDropManager? Instance;

    private void Awake()
    {
        Instance = this;
        InvokeRepeating(nameof(ProcessItemDrops), IntervalSeconds.Value, IntervalSeconds.Value);
        IntervalSeconds.SettingChanged += OnIntervalSecondsChanged;
    }

    private void OnDestroy()
    {
        IntervalSeconds.SettingChanged -= OnIntervalSecondsChanged;
        DropBuffer.Clear();
        Functions.ClearNearbyContainers();

        if (Instance == this)
            Instance = null;
    }

    private static readonly List<ItemDrop> DropBuffer = new();

    private void ProcessItemDrops()
    {
        if (ShouldPause())
        {
            return;
        }

        if (Boxes.Containers == null || Boxes.Containers.Count == 0)
            return;

        DropBuffer.Clear();

        List<ItemDrop> instances = ItemDrop.s_instances;
        for (int i = 0; i < instances.Count; ++i)
        {
            ItemDrop itemDrop = instances[i];
            if (itemDrop == null || itemDrop.transform == null) continue;
            if (itemDrop.m_nview == null || !itemDrop.m_nview.IsValid()) continue;
            if (itemDrop.IsPiece()) continue;

            DropBuffer.Add(itemDrop);
        }

        if (DropBuffer.Count == 0)
            return;

        try
        {
            Functions.BuildNearbyContainers();

            for (int i = 0; i < DropBuffer.Count; ++i)
                Functions.CheckItemDropInstanceAndStore(DropBuffer[i]);
        }
        finally
        {
            Functions.ClearNearbyContainers();
            DropBuffer.Clear();
        }
    }

    private void OnIntervalSecondsChanged(object sender, EventArgs e)
    {
        CancelInvoke(nameof(ProcessItemDrops));
        InvokeRepeating(nameof(ProcessItemDrops), IntervalSeconds.Value, IntervalSeconds.Value);
    }

    private static bool ShouldPause()
    {
        Player? player = Player.m_localPlayer;
        return player == null || player.IsTeleporting() || player.IsDead();
    }
}
