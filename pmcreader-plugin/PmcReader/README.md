# MsrUtil
Performance Counter Reader

# THIS SOFTWARE IS CONSIDERED EXPERIMENTAL. OUTPUT FROM THE APPLICATION MAY BE INACCURATE. NOT ALL CPU ARCHITECTURES ARE SUPPORTED.

A messy attempt at reading performance counters for various CPUs and displaying derived metrics in real time. Probably due for a rewrite/rethink of how I approach this pretty soon, whenever I have time. The current structure is a bit messy. Winring0 interface code adapted from LibreHardwareMontor at https://github.com/LibreHardwareMonitor/LibreHardwareMonitor

## Building
Open the sln in Visual Studio, hit build.

## Running
Right click, run as admin. It needs admin privileges to use the winring0 driver.

## Supported Platforms
Every CPU has tons of performance monitoring events, and in most cases it's not practical to cover them all. CPUs have been largely supported on an ad-hoc basis whenever I (Clam) wanted to investigate performance characteristics on that platform.

### AMD, Core Events
Zen 2 has the most thorough coverage. Piledriver events are also covered, though in a more limited way because of counter restrictions.
Code is present for Zen 1 and 3, but testing has been minimal on those CPUs because I don't have examples of them.

### AMD, Non-Core Events
Basic L3 counter support is implemented for all Zen generations, but data fabric (Infinity Fabric) support is mostly not present because those counters are largely undocumented, especially on client platforms.

Piledriver's northbridge is decently well covered. 
### Intel, Core Events
Sandy Bridge and Haswell have the best core event coverage. Skylake and Goldmont Plus are a work in progress, with most basic events covered. On other Intel cores, I have code that can read "architectural" events (instructions, cycles, branch mispredicts, last level cache misses), but other events won't be supported.

There might be some code for Alder Lake, but we don't talk about that. Because it has never been tested.

### Intel, Non-Core Events
The program can read basic counters on Haswell client/HEDT and Skylake client uncores for L3 hitrate and system agent arbitration queue events. 

There's pretty extensive support for Sandy Bridge HEDT L3 performance counters. Sandy Bridge's Power Control Unit (PCU) can be monitored as well.

## Use of Undocumented Events
In some places, I use events and unit mask combinations not explcitly documented by AMD or Intel. In some cases, I use a combination of unit mask bits that isn't directly in Intel's docs (since they provide umask values, and don't document what's selected by individual bits). Or, I set combinations of edge/count mask fields that aren't directly documented. I expect those cases to work fine. 

In others, I might use a completely undocumented event/umask bit, with basic testing to ensure it does count what I think it counts. I think I've marked most of these cases with a '?', but I may have missed some.

Anyway, it's best to do your own verification before taking the results as truth. For example, you can verify L3 hitrate is reported correctly by reading from an array that fits within L3, and seeing that the hitrate is indeed high.

## General Disclaimer
Even documented performance monitoring events may be inaccurate. There's *plenty* of errata around performance monitoring events, and they're often never fixed by the manufacturers because an incorrectly counting perf event won't cause crashes or break user programs. And inaccuracies are usually small enough to not seriously affect code optimization efforts.

Also, it's good to read about the events in use in Intel/AMD's docs before interpreting them. I don't expect everyone to do this because documentation can be really hard to parse, so there are the major things to be aware of:
- Cache requests and misses are generally tracked per cache line. For example, if three instructions miss L1D but requested data from the same 64B cache line, that'll count as one L1D miss/fill request in the cache hierarchy.
- Many events are "speculative" meaning that counts could be triggered by instructions that are never retired (committed, or have their results made final). For example, instructions could be fetched, pass through rename/execute and cause event count increments there, but then be thrown away before retirement because they came down a mispredicted path. In some cases, similar events on AMD and Intel cannot be directly compared because one is speculative and the other is not.
- Non-core events should always be considered speculative.

## Other

There's testing controls under the 'Do not push these buttons' section. They may or may not work and I generally recommend avoiding them unless you really know what you're doing. They'll most likely decrease performance, and could cause weird behavior. 

### Intel, Testing Controls
Prefetchers can be turned on and off, using MSRs documented by Intel. Specifically:
- L2 HW PF: L2 hardware prefetcher
- L2 Adj PF: L2 adjacent cache line prefetcher. On a L2 miss, this prefetcher fetches an adjacent cache line as well, taking advantage of spatial locality.
- L1D Adj PF: Adjacent line prefetcher for L1D misses
- L1D IP PF: Instruction pointer based prefetcher that tracks the address of previous load instructions and uses that to prefetch extra cache lines.

### AMD, Testing Controls
For 17h and newer CPUs (Zen stuff):
- Op Cache: Can be used to disable the micro-op cache. Not documented by AMD, generally drops performance by a few percent. Use at your own risk.
- Core Performance Boost: Can be used to disable Core Performance Boost, which will prevent the CPU from raising frequencies beyond base clock. Potentially useful for ensuring clock consistency when microbenchmarking, or just making your CPU more power efficient.
- L1D Stream Prefetcher, L2 Stream Prefetcher: Toggles MSR bits that should request the respective prefetchers to be disabled, but I'm not sure if it works.
- Set CPU Name String: Can be used to set the CPU name reported by the CPUID instruction. This can be funny, but can also cause strange behavior. Benchmark apps and CPU-Z may misidentify your CPU. Ryzen Master may think you're on a different CPU and not show your saved profiles.