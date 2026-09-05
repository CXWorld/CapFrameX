# AMD Frame Latency Meter in CapFrameX

Upstream: https://github.com/GPUOpen-Tools/frame_latency_meter

Imported revision: `460a05cb9543d1c2b50642a3c5312c2b64307c15`
(`FLM_v1.0-3-g460a05c`, imported 2026-07-31).

This directory contains the FLM backend and the required subsets of AMD AMF
and SimpleIni. CapFrameX builds them into `CapFrameX.FLM.dll`; it does not ship
or launch the upstream FLM executable.

CapFrameX-specific changes are guarded by `CAPFRAMEX_FLM_EMBEDDED` where
possible. They remove console/UI, keyboard, INI and CSV behavior; add explicit
start/stop control and a bounded sample ring; expose QPC-based latency samples;
and make cleanup safe for repeated in-process capture sessions.

The interop API accepts only passive `MOUSE_CLICK` mode. An input poller records
real left-button down edges, including rejected clicks while the scene moves.
The capture worker publishes the first detected response immediately, using
the captured frame's QPC timestamp. It does not discard the first valid click,
wait for the picture to become still after a response, or re-arm a held button.
The upstream synthetic `MOUSE_MOVE` mode must never be enabled in CapFrameX.

The capture baseline warms up for 100 captured frames. A click needs a recent,
below-threshold frame before it; overlapping clicks are rejected because their
responses cannot be attributed reliably. Responses must arrive within 300 ms.
Background estimation is frozen while a response is pending and reset per
session. The input poller sleeps 1 ms, so input polling and scheduling remain
sources of measurement error. This measures software-visible screen response,
not physical mouse actuation or panel response time.

`FlmGetDiagnostics` reports warm-up, waiting for click/response, scene motion,
timeout, and absent frames, with frame/click/rejection/timeout counters.
Missing DLLs and initialization errors are surfaced in settings. A missing or
stale live measurement is `N/A`, not zero. Settings retain the last click result
with its age; sensor logging and the live average retain their freshness limits.

Capture output, normalized region and SAD threshold are configurable in
CapFrameX settings. The default threshold coefficient is 3 (previously 5).
Lower thresholds can detect smaller responses but also unrelated animation.
Output indices are zero-based on the capture adapter, not Windows display IDs;
multi-adapter capture is not implemented. Region preview is a schematic of the
selected output, not a live screenshot. AMF initializes through DX12 by default
as required by upstream for Vulkan. DX11 compatibility and DXGI desktop capture
are selectable. The old frame-generation setting had no effect on passive
clicks and is no longer presented as an optimization.

In the embedded build, acquisition and conversion run on the host's capture
worker to keep buffer resets/rebuilds serialized. AMF frame queries have a
100 ms deadline and format discovery a 2 s deadline; shutdown cancels polling.
This bounds retry loops, but cannot interrupt a hung third-party driver call.

The main app has an explicit native project dependency and stages the FLM DLL
from that project's output into both build and publish payloads. CI rejects
missing runtime files before building the installer. The standalone native
regression tests can be run without a GPU:

```bat
msbuild source\CapFrameX.FLM.Tests\CapFrameX.FLM.Tests.vcxproj /p:Configuration=Release /p:Platform=x64
source\CapFrameX.FLM.Tests\x64\Release\CapFrameX.FLM.Tests.exe
```

Managed tests also load the shipped DLL, check exports/ABI validation, exercise
configuration bounds and unavailable overlay values. Real AMD/AMF game latency
and capture overhead still require validation on an AMD system.

License texts are in `LICENSE-FLM.txt`, `external/amf/LICENSE.txt`, and
`external/ini/LICENCE.txt`.
