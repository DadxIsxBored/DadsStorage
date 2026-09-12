using DadsStorage.APIs.MUC.MUCSrc.Helper;
using DadsStorage.APIs.MUC.MUCSrc.Data;

namespace DadsStorage.APIs.MUC.MUCSrc;

public static class PackageHandler
{
    private static readonly Dictionary<int, IPackage> Packages = new Dictionary<int, IPackage>();
    private static readonly Dictionary<int, float> AddedAt = new Dictionary<int, float>();
    private static readonly List<int> Stale = new List<int>();
    private static readonly System.Random Random = new System.Random();
    private static int total;

    private const float PackageTimeout = 60f;

    public static int AddPackage(IPackage package)
    {
        DropStale();

        int id = GetRandomId();
        Packages.Add(id, package);
        AddedAt[id] = Time.time;
#if DEBUG
            Log.LogDebug($"PackageHandler: Added package {id}, total packages: {++total}");
#endif
        return id;
    }

    private static void DropStale()
    {
        if (Packages.Count == 0) return;

        Stale.Clear();
        float now = Time.time;

        foreach (KeyValuePair<int, float> pair in AddedAt)
        {
            if (now - pair.Value >= PackageTimeout)
                Stale.Add(pair.Key);
        }

        for (int i = 0; i < Stale.Count; ++i)
        {
            Packages.Remove(Stale[i]);
            AddedAt.Remove(Stale[i]);
        }

        Stale.Clear();
    }

    public static void RemovePackage(int id)
    {
        Packages.Remove(id);
        AddedAt.Remove(id);
#if DEBUG
            Log.LogDebug($"PackageHandler: Removed package {id}, total packages: {--total}");
#endif
    }

    private static int GetRandomId()
    {
        return Random.Next(int.MinValue, int.MaxValue);
    }

    public static bool GetPackage<T>(int id, out T package) where T : IPackage
    {
        bool containsPackage = Packages.TryGetValue(id, out IPackage result);

        if (containsPackage)
        {
            package = (T)result;
            return true;
        }

        package = default;
        return false;
    }

    public static T GetPackage<T>(int id) where T : IPackage
    {
        if (Packages.TryGetValue(id, out IPackage result))
        {
            return (T)result;
        }

        return default;
    }
}
