namespace OmniToolbox.TreePublic;
internal static class NativeRuntimeGuard {
    internal static void RequireInitialized() {
        if (KamiToolKit.KamiToolKitLibrary.PluginInterface is null)
            throw new InvalidOperationException("宿主尚未初始化 API 1 的原生界面运行库。请更新到 Yanjiu 0.1.6.1 或更新版本后再启用本模块。");
    }
}
