# API 1 native UI fix, 0.1.6.1

The isolated API 1 runtime prepared TreeTools/TreeToolKit but never initialized KamiToolKitLibrary. Node finalization could dereference its missing services; native module windows could not be created reliably.

Initialize the isolated native library asynchronously before TreeToolKit and before module OnStart. Match owned addons from that runtime. Dispose native addons/nodes after module shutdown and before disposing TreeTools. Route both ConfigDirectory and GetPluginConfigDirectory to api1-runtime so native window state cannot collide with legacy DR state.

Validation: release build, DR compilation/type binding 303/311, existing lifecycle/settings tests, and Api1NativeRuntimeCheck (real dependency signatures, ownership properties, both configuration-directory accessors). Eight existing DR modules remain unavailable; this release does not claim to repair them. Native game hooks, windows and interactions require in-game verification.

Standalone user-macro/glamour modules also have a fail-fast guard for an uninitialized native runtime. Updated modules are separate local imports; the host release does not silently install them.

Packaging preserves the fourth version component to avoid overwriting the prior 0.1.6 assets.
