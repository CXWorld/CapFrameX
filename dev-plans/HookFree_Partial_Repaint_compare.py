"""Compare overlay variants of one game session from CapFrameX capture files.

Companion of HookFree_Partial_Repaint_DevPlan.md. A variant is a capture comment; several captures
with the same comment are runs of that variant. For every run the script prints the display
interval statistics and the GPU slips, and for every variant with more than one run the mean,
minimum and maximum over its runs.

    python HookFree_Partial_Repaint_compare.py --list
    python HookFree_Partial_Repaint_compare.py "FUSION E? hardware" "FUSION W? WARP" "FUSION session, overlay off"
    python HookFree_Partial_Repaint_compare.py --pattern "CapFrameX-DOOMTheDarkAges.exe-2026-11-10T*.json" --tick

Variants are matched against the comment with shell wildcards (fnmatch), so "FUSION E? hardware"
collects "FUSION E1 hardware" and "FUSION E2 hardware". Without variants every comment found is a
variant of its own.

Columns
    frames    frames of the capture
    disp med  median of MsBetweenDisplayChange (ms)
    stdev     population standard deviation of the display intervals (ms)
    p99, p99.9  quantiles of the display intervals (ms)
    >1.1x, >1.25x, <0.75x  share of display intervals beyond these multiples of the median (%)
    P1, P0.2  1 % and 0.2 % percentile of 1000 / display interval (FPS, R-8 quantile as in CapFrameX)
    GPU p50, p99  GpuActive of the frames that carry GPU work (ms). With frame generation only the
              first frame of a pair carries it, so frames at or below 3 ms are left out.
    >16ms     share of those frames with more than 16 ms GPU work (%)
    slips/s   frames whose GPU work exceeds the median by more than 1.2 ms, per second

--tick adds, per run, how strongly the GPU excess is locked to the overlay's 100 ms tick: the
present start of frame i is TimeInSeconds[i + 1] - MsInPresentAPI[i]; frames whose GPU work exceeds
the median by 0.8 ms are folded modulo every period between 99.00 and 100.99 ms, and the best
period's share within a 20 ms slot is reported together with the mean largest excess per tick
position 0..9 (position = tick index modulo 10, so one position is the 1 Hz content repaint).
"""
import argparse
import bisect
import fnmatch
import glob
import json
import math
import os
import statistics as st
from collections import Counter

DEFAULT_FOLDER = r"C:\Users\CapFrameX\Documents\CapFrameX\Captures\The Witcher 3"
DEFAULT_PATTERN = "CapFrameX-DOOMTheDarkAges.exe-*.json"
NO_COMMENT = "(no comment)"

COLUMNS = [
    ("frames", "frames", "{:6.0f}"),
    ("disp med", "median", "{:8.2f}"),
    ("stdev", "stdev", "{:6.2f}"),
    ("p99", "p99", "{:6.2f}"),
    ("p99.9", "p999", "{:6.2f}"),
    (">1.1x", "over110", "{:6.2f}"),
    (">1.25x", "over125", "{:7.2f}"),
    ("<0.75x", "under75", "{:7.2f}"),
    ("P1", "p1", "{:7.1f}"),
    ("P0.2", "p02", "{:7.1f}"),
    ("GPU p50", "gpu50", "{:7.2f}"),
    ("p99", "gpu99", "{:6.2f}"),
    (">16ms", "gpuOver16", "{:6.2f}"),
    ("slips/s", "slips", "{:7.2f}"),
]


def quantile(values, p):
    """Sorted-index quantile, as used for the display interval tails."""
    s = sorted(values)
    return s[min(len(s) - 1, int(len(s) * p))]


def r8(values, x):
    """R-8 quantile, the definition CapFrameX uses for P1 and P0.2."""
    s = sorted(values)
    n = len(s)
    h = (n + 1 / 3) * x + 1 / 3
    if h <= 1:
        return s[0]
    if h >= n:
        return s[-1]
    k = math.floor(h)
    return s[k - 1] + (h - k) * (s[k] - s[k - 1])


def slot_info(events, period, width=20.0):
    """Largest share of the events that falls into one slot of the given width modulo the period."""
    res = sorted(x % period for x in events)
    m = len(res)
    if not m:
        return 0.0, 0.0
    res2 = res + [x + period for x in res]
    j = best = 0
    start = 0.0
    for i in range(m):
        while j < 2 * m and res2[j] < res[i] + width:
            j += 1
        if j - i > best:
            best, start = j - i, res[i]
    return best / m, start


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        d = json.load(f)
    comment = (d.get("Info") or {}).get("Comment") or NO_COMMENT
    return comment, d["Runs"][0]["CaptureData"]


def column(capture, name, n):
    values = capture.get(name)
    return list(values[:n]) if values else None


def statistics_of(capture):
    t = [x * 1000 for x in capture["TimeInSeconds"]]
    n = len(t) - 1
    md = column(capture, "MsBetweenDisplayChange", n)
    ga = column(capture, "GpuActive", n)
    med = st.median(md)
    fps = [1000 / x for x in md if x > 0.01]
    seconds = t[n - 1] / 1000
    row = {
        "frames": n,
        "seconds": seconds,
        "median": med,
        "stdev": st.pstdev(md),
        "p99": quantile(md, 0.99),
        "p999": quantile(md, 0.999),
        "over110": 100 * sum(1 for x in md if x > 1.1 * med) / n,
        "over125": 100 * sum(1 for x in md if x > 1.25 * med) / n,
        "under75": 100 * sum(1 for x in md if x < 0.75 * med) / n,
        "p1": r8(fps, 0.01),
        "p02": r8(fps, 0.002),
    }
    work = [x for x in ga if x > 3] if ga else []
    if work:
        base = st.median(work)
        row["gpu50"] = base
        row["gpu99"] = quantile(work, 0.99)
        row["gpuOver16"] = 100 * sum(1 for x in work if x > 16) / len(work)
        row["slips"] = sum(1 for x in work if x > base + 1.2) / seconds
    return row


def tick_lock(capture):
    t = [x * 1000 for x in capture["TimeInSeconds"]]
    n = len(t) - 1
    ip = column(capture, "MsInPresentAPI", n)
    ga = column(capture, "GpuActive", n)
    if not ip or not ga:
        return ["   tick lock: the capture has no MsInPresentAPI or GpuActive column"]
    starts = [t[i + 1] - ip[i] for i in range(n)]
    work = sorted((i for i in range(n) if ga[i] > 3), key=lambda i: starts[i])
    if not work:
        return ["   tick lock: no frame carries GPU work"]
    base = st.median(ga[i] for i in work)
    events = [starts[i] for i in work if ga[i] > base + 0.8]
    work_starts = [starts[i] for i in work]
    modes = Counter(capture.get("PresentMode") or []).most_common(2)
    layers = Counter(capture.get("LayerIndex") or []).most_common(2)
    lines = [f"   modes {modes}, layers {layers}, frames over +0.8 ms GPU: {len(events)}"]
    if len(events) < 6:
        return lines
    period = max(((slot_info(events, k / 100)[0], k / 100) for k in range(9900, 10100)), key=lambda x: x[0])[1]
    share, phase = slot_info(events, period)
    lines.append(f"   tick lock: period {period:.2f} ms, {100 * share:.0f} % of them in a 20 ms slot")
    first = int((work_starts[0] - phase) // period) + 1
    last = int((work_starts[-1] - phase) // period)
    per = {p: [] for p in range(10)}
    for k in range(first, last):
        tick = phase + k * period
        lo = bisect.bisect_left(work_starts, tick - 3)
        hi = bisect.bisect_right(work_starts, tick + 25)
        if hi > lo:
            per[k % 10].append(max(ga[work[j]] - base for j in range(lo, hi)))
    means = {p: st.mean(v) for p, v in per.items() if v}
    if means:
        lines.append("   mean largest GPU excess per tick position (ms): " + " ".join(f"{p}:{v:4.2f}" for p, v in means.items()))
        ordered = sorted(means.values(), reverse=True)
        lines.append(f"   strongest position {ordered[0]:.2f} ms, median position {st.median(ordered):.2f} ms")
    return lines


def format_row(label, row):
    cells = [(fmt.format(row[key]) if key in row else " " * len(fmt.format(0))) for _, key, fmt in COLUMNS]
    return f"{label:44s} " + " ".join(cells)


def header():
    cells = [f"{title:>{len(fmt.format(0))}s}" for title, _, fmt in COLUMNS]
    return f"{'variant / run':44s} " + " ".join(cells)


def main():
    parser = argparse.ArgumentParser(description="Compare overlay variants of one game session from CapFrameX capture files.")
    parser.add_argument("variants", nargs="*", help="capture comments to compare (shell wildcards allowed); default: every comment found")
    parser.add_argument("--folder", default=DEFAULT_FOLDER, help="capture folder (default: %(default)s)")
    parser.add_argument("--pattern", default=DEFAULT_PATTERN, help="file name pattern inside the folder (default: %(default)s)")
    parser.add_argument("--list", action="store_true", help="list the matching captures with their comment and exit")
    parser.add_argument("--tick", action="store_true", help="also report how the GPU excess is locked to the overlay's 100 ms tick")
    args = parser.parse_args()

    captures = []
    for path in sorted(glob.glob(os.path.join(args.folder, args.pattern))):
        try:
            comment, capture = load(path)
        except (OSError, ValueError, KeyError, IndexError) as error:
            print(f"skipped {os.path.basename(path)}: {error}")
            continue
        captures.append((path, comment, capture))
    if not captures:
        print(f"no capture matches {os.path.join(args.folder, args.pattern)}")
        return 1

    if args.list:
        for path, comment, capture in captures:
            t = capture["TimeInSeconds"]
            print(f"{os.path.basename(path):58s} {len(t):6d} frames {t[-1]:6.1f} s  {comment}")
        return 0

    variants = args.variants
    if not variants:
        variants = list(dict.fromkeys(comment for _, comment, _ in captures))

    print(header())
    details = []
    for variant in variants:
        runs = [(path, comment, capture) for path, comment, capture in captures if fnmatch.fnmatchcase(comment, variant)]
        if not runs:
            print(f"{variant:44s} no capture with this comment")
            continue
        rows = []
        for path, comment, capture in runs:
            row = statistics_of(capture)
            rows.append(row)
            stamp = os.path.basename(path).rsplit("-", 1)[-1].replace(".json", "")
            label = f"{comment[:28]} [{stamp[-6:]}]"
            print(format_row(label, row))
            if args.tick:
                details.append((label, tick_lock(capture)))
        if len(rows) > 1:
            keys = [key for _, key, _ in COLUMNS if all(key in row for row in rows)]
            print(format_row(f"  mean of {len(rows)} runs", {key: st.mean(row[key] for row in rows) for key in keys}))
            print(format_row("  min", {key: min(row[key] for row in rows) for key in keys}))
            print(format_row("  max", {key: max(row[key] for row in rows) for key in keys}))

    for label, lines in details:
        print()
        print(f"== {label}")
        for line in lines:
            print(line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
