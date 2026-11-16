using System;
using System.Runtime.InteropServices;

namespace KamiToolKit.Internal.Classes;

// Native function pointers do not keep their managed delegate owners alive.
// Release this root only after native code can no longer call the managed table.
internal sealed class NativeCallbackLifetime : IDisposable {
    private GCHandle owner;

    internal NativeCallbackLifetime(object value) => owner = GCHandle.Alloc(value, GCHandleType.Normal);

    public void Dispose() {
        if (owner.IsAllocated) owner.Free();
    }
}
