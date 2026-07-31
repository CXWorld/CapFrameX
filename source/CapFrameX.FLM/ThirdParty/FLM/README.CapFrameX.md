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

License texts are in `LICENSE-FLM.txt`, `external/amf/LICENSE.txt`, and
`external/ini/LICENCE.txt`.
