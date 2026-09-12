namespace DadsStorage.APIs.Compatibility.WardIsLove;


public class WardIsLovePlugin : WILCompat {
    private const string GUID = "Dad_Is_Bored.WardIsLove";
    private static readonly System.Version MinVersion = new(2, 3, 3);

    private static Type ClassType() {
        return Type.GetType("WardIsLove.WardIsLovePlugin, WardIsLove");
    }

    public static bool IsLoaded() {
        return Chainloader.PluginInfos.ContainsKey(GUID) && Chainloader.PluginInfos[GUID].Metadata.Version >= MinVersion;
    }

    public static ConfigEntry<bool>? WardEnabled() {
        return GetField<ConfigEntry<bool>>(ClassType(), null!, "WardEnabled");
    }
}