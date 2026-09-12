namespace DadsStorage.APIs.MUC;

internal static class MUCCompat
{
	internal static bool DoNotPatch => Chainloader.PluginInfos.ContainsKey("com.maxsch.valheim.MultiUserChest")
		|| Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.valheim.quick_stack")
		|| Chainloader.PluginInfos.ContainsKey("aedenthorn.SimpleSort")
		|| Chainloader.PluginInfos.ContainsKey("aedenthorn.QuickStore");

	internal static bool MUCLoaded => Chainloader.PluginInfos.ContainsKey("com.maxsch.valheim.MultiUserChest");

	internal static bool CanRouteToOtherOwner => MUCLoaded || !DoNotPatch;
}
