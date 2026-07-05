# CapFrameX.OSD prebuilt binaries

Fallback binaries for the hook-free OSD, built from the private
[CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) repository.

The build uses these automatically when the `external/CapFrameX.OSD` submodule is not
checked out (developers without access to the private repo). With the submodule present,
the OSD is built from source instead and these files are ignored.

## Contents

- `net472/`, `net9.0-windows/` — `CapFrameX.OSD.Interop.dll` (managed P/Invoke bridge, x64)
- `native/cfx_osd_core.dll` — native renderer (x64, RelWithDebInfo)

## Updating (requires access to the private repo)

```
cd CapFrameX.OSD                      # the OSD repo
cmake --preset vs2022 && cmake --build build --config RelWithDebInfo   # in CapFrameX.OSD/CapFrameX.OSD
dotnet build CapFrameX.OSD.sln -c Release
```

Then copy `CapFrameX.OSD.Interop\bin\x64\Release\<tfm>\CapFrameX.OSD.Interop.dll` and
`CapFrameX.OSD\build\bin\RelWithDebInfo\cfx_osd_core.dll` here, and bump the submodule
to the matching commit.
