using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace DadsStorage.Patches.Favoriting;

public class UserConfig
{
    private static readonly Dictionary<long, UserConfig> PlayerConfigs = new Dictionary<long, UserConfig>();

    public static UserConfig GetPlayerConfig(long playerID)
    {
        if (PlayerConfigs.TryGetValue(playerID, out UserConfig userConfig))
        {
            return userConfig;
        }
        else
        {
            userConfig = new UserConfig(playerID);
            PlayerConfigs[playerID] = userConfig;

            return userConfig;
        }
    }

    /// <summary>
    /// Create a user config for this local save file
    /// </summary>
    public UserConfig(long uid)
    {
        string autoStorePath = Path.Combine(Paths.ConfigPath, $"{ModName}_player_{uid}.dat");
        string extendedPlayerInventoryPath = Path.Combine(Paths.ConfigPath, $"DadsEPI_player_{uid}.dat");

        if (Chainloader.PluginInfos.ContainsKey("goldenrevolver.quick_stack_store"))
        {
            _configPath = Path.Combine(Paths.ConfigPath, $"QuickStackStore_player_{uid}.dat");
            _mirrorPaths = [autoStorePath, extendedPlayerInventoryPath];
        }
        else
        {
            _configPath = autoStorePath;
            _mirrorPaths = [extendedPlayerInventoryPath];
        }

        Load();
    }

    internal void ResetAllFavoriting()
    {
        DadsStorageLogger.LogWarning("Resetting all favoriting data!");

        _favoritedSlots = [];
        _favoritedItems = [];

        Save();
    }

    private void Save()
    {
        WriteFile(_configPath);

        // Only mirrors that already exist, don't litter config with files for mods they never had
        foreach (string mirrorPath in _mirrorPaths.Where(File.Exists))
        {
            try
            {
                WriteFile(mirrorPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DadsStorageLogger.LogWarning($"Couldn't sync favorites to {mirrorPath}: {exception.Message}");
            }
        }
    }

    private void WriteFile(string path)
    {
        using Stream stream = File.Open(path, FileMode.Create);
        List<Tuple<int, int>>? tupledSlots = _favoritedSlots.Select(item => new Tuple<int, int>(item.x, item.y)).ToList();

        Bf.Serialize(stream, tupledSlots);
        Bf.Serialize(stream, _favoritedItems.ToList());
    }

    private static object TryDeserialize(Stream stream)
    {
        object result;

        try
        {
            result = Bf.Deserialize(stream);
        }
        catch (SerializationException)
        {
            result = null!;
        }

        return result;
    }

    private static void LoadProperty<T>(Stream file, out T property) where T : new()
    {
        object obj = TryDeserialize(file);

        if (obj is T property1)
        {
            property = property1;
            return;
        }

        property = Activator.CreateInstance<T>();
    }

    private void Load()
    {
        _favoritedSlots = [];
        _favoritedItems = [];

        // Merge, don't pick one. A lost favorite gets your shit stored away, an extra one doesn't hurt
        foreach (string path in _mirrorPaths.Prepend(_configPath).Where(File.Exists))
        {
            try
            {
                ReadFileInto(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DadsStorageLogger.LogWarning($"Couldn't read favorites from {path}: {exception.Message}");
            }
        }
    }

    private void ReadFileInto(string path)
    {
        using Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        List<Tuple<int, int>>? deserializedFavoritedSlots = [];
        LoadProperty(stream, out deserializedFavoritedSlots);

        if (deserializedFavoritedSlots != null)
            foreach (Tuple<int, int>? item in deserializedFavoritedSlots)
            {
                _favoritedSlots.Add(new Vector2i(item.Item1, item.Item2));
            }

        List<string>? deserializedFavoritedItems = [];
        LoadProperty(stream, out deserializedFavoritedItems);

        if (deserializedFavoritedItems != null)
            foreach (string? item in deserializedFavoritedItems)
            {
                _favoritedItems.Add(item);
            }
    }

    public void ToggleSlotFavoriting(Vector2i position)
    {
        _favoritedSlots.XAdd(position);
        Save();
    }

    public bool ToggleItemNameFavoriting(ItemDrop.ItemData.SharedData item)
    {
        _favoritedItems.XAdd(item.m_name);
        Save();

        return true;
    }

    public bool IsSlotFavorited(Vector2i position)
    {
        return _favoritedSlots.Contains(position);
    }

    public bool IsItemNameFavorited(ItemDrop.ItemData.SharedData item)
    {
        return _favoritedItems.Contains(item.m_name);
    }

    public bool IsItemNameOrSlotFavorited(ItemDrop.ItemData item)
    {
        return IsItemNameFavorited(item.m_shared) || IsSlotFavorited(item.m_gridPos);
    }

    private readonly string _configPath;
    private readonly string[] _mirrorPaths;
    private HashSet<Vector2i> _favoritedSlots = null!;
    private HashSet<string> _favoritedItems = null!;
    private static readonly BinaryFormatter Bf = new BinaryFormatter();
}

public static class CollectionExtension
{
    public static bool XAdd<T>(this HashSet<T> instance, T item)
    {
        if (instance.Contains(item))
        {
            instance.Remove(item);
            return false;
        }
        else
        {
            instance.Add(item);
            return true;
        }
    }
}
