# CapFrameX.OSD prebuilt binaries

Fallback binaries for the hook-free OSD, built from the private
[CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) repository.

The build uses these automatically when the `external/CapFrameX.OSD` submodule is not
checked out (developers without access to the private repo). With the submodule present,
the OSD is built from source instead and these files are ignored.

## Contents

- `net472/`, `net9.0-windows/` — `CapFrameX.OSD.Interop.dll` (managed P/Invoke bridge, x64)
- `native/cfx_osd_core.dll` — native renderer (x64, RelWithDebInfo)
- `native/cfx_osd_hook.dll` — x64 DXGI hook
- `native/x86/` — x86 DXGI hook and injection helper
- `native/vk/cfx_osd_vklayer.dll` — x64 Vulkan implicit layer
- `native/vk/cfx_osd_vklayer_v1.json` — versioned Vulkan loader manifest

The WiX installer registers the Vulkan manifest under
`HKLM\SOFTWARE\Khronos\Vulkan\ImplicitLayers`. Portable builds stage the files but do not
change the registry.

## Updating (requires access to the private repo)

```
cd CapFrameX.OSD                      # the OSD repo
cmake --preset vs2022 && cmake --build build --config RelWithDebInfo   # in CapFrameX.OSD/CapFrameX.OSD
dotnet build CapFrameX.OSD.sln -c Release
```

Then copy the managed bridge, core, x64/x86 hooks, injection helper, and Vulkan layer/manifest
from their `RelWithDebInfo` outputs into the matching folders above, and bump the submodule to
the matching commit.
