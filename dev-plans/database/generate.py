"""Generates cx2-database-schema.html beside this file.

The column description below is the schema as CapFrameXDbContextModelSnapshot states it, written
once and used for both the SVG entity boxes and the HTML reference tables, so the picture and the
tables cannot drift apart. After a migration, update the description here and run:

    python dev-plans/database/generate.py

Editing the generated HTML by hand means placing the SVG geometry by hand, which is the work this
script exists to do.
"""

import io
import html
import os

# (name, clr, sql, required, tag, note)
SUITES = [
    (None, [
        ("Id", "Guid", "TEXT", True, "PK", "Primary key."),
        ("Name", "string", "TEXT(200)", True, "", "What the suite is called in the library."),
        ("Description", "string?", "TEXT(1000)", False, "", ""),
        ("Type", "SuiteType", "INTEGER", True, "", "0 HardwareReview &middot; 1 GameReview &middot; 2 ComparisonSet &middot; 3 Miscellaneous."),
        ("CreatedAt", "DateTime", "TEXT", True, "", ""),
        ("UpdatedAt", "DateTime", "TEXT", True, "", ""),
    ]),
]

SESSIONS = [
    ("identity", [
        ("Id", "Guid", "TEXT", True, "PK", "Primary key."),
        ("SuiteId", "Guid", "TEXT", True, "FK", "&rarr; <code>Suites.Id</code>, cascade. Mandatory: there is no session outside a suite."),
        ("Hash", "string?", "TEXT(100)", False, "", "Carried over from the 1.x record so both generations can recognise the same capture."),
        ("CreatedAt", "DateTime", "TEXT", True, "", "When the capture was taken, in UTC."),
    ]),
    ("game", [
        ("GameName", "string", "TEXT(200)", True, "", ""),
        ("ProcessName", "string", "TEXT(200)", True, "", "Executable name without extension."),
        ("Comment", "string?", "TEXT(2000)", False, "", ""),
    ]),
    ("hardware", [
        ("Processor", "string", "TEXT(200)", True, "", ""),
        ("Motherboard", "string?", "TEXT(200)", False, "", ""),
        ("SystemRam", "string?", "TEXT(100)", False, "", ""),
        ("Gpu", "string", "TEXT(200)", True, "", ""),
        ("GpuCount", "int?", "INTEGER", False, "", ""),
        ("GpuCoreClock", "int?", "INTEGER", False, "", "MHz."),
        ("GpuMemoryClock", "int?", "INTEGER", False, "", "MHz."),
    ]),
    ("drivers", [
        ("BaseDriverVersion", "string?", "TEXT(50)", False, "", ""),
        ("DriverPackage", "string?", "TEXT(100)", False, "", ""),
        ("GpuDriverVersion", "string?", "TEXT(50)", False, "", ""),
    ]),
    ("system", [
        ("Os", "string", "TEXT(100)", True, "", ""),
        ("ApiInfo", "string?", "TEXT(50)", False, "", "DX11, DX12, Vulkan &hellip;"),
        ("ResizableBar", "bool?", "INTEGER", False, "", "Unknown is a third state, which is why it is nullable."),
        ("WinGameMode", "bool?", "INTEGER", False, "", ""),
        ("Hags", "bool?", "INTEGER", False, "", "Hardware-accelerated GPU scheduling."),
        ("PresentationMode", "string?", "TEXT(50)", False, "", ""),
        ("ResolutionInfo", "string?", "TEXT(50)", False, "", ""),
    ]),
    ("record source", [
        ("SourceFilePath", "string?", "TEXT", False, "UQ", "Where the capture file lives. Unique, filtered to rows that have one; <code>NULL</code> marks a session the service recorded itself."),
        ("SourceFileSize", "long?", "INTEGER", False, "", "Size when the row was written &mdash; one half of the change detection."),
        ("SourceModifiedUtc", "DateTime?", "TEXT", False, "", "Last write time when the row was written &mdash; the other half."),
        ("IndexVersion", "int", "INTEGER", True, "", "Projection version. A row below the current one is re-read; a row above it is left alone."),
    ]),
    ("list projection", [
        ("DurationSeconds", "double?", "REAL", False, "", "Span the frames cover, summed over the runs."),
        ("RunCount", "int?", "INTEGER", False, "", ""),
        ("FrameCount", "int?", "INTEGER", False, "", ""),
        ("SparklineJson", "string?", "TEXT", False, "", "At most 64 min/max-decimated frame times, as a JSON array."),
        ("HasPcLatency", "bool", "INTEGER", True, "", "Whether the capture carries input-to-display latency at all."),
        ("HasDisplayChange", "bool", "INTEGER", True, "", "Whether it carries display-side frame times."),
        ("AverageFps", "double?", "REAL", False, "", "From the analysis adapter, so the list cannot disagree with the record it opens."),
        ("P1Fps", "double?", "REAL", False, "", ""),
        ("P99Fps", "double?", "REAL", False, "", ""),
    ]),
]

RUNS = [
    ("identity", [
        ("Id", "Guid", "TEXT", True, "PK", "Primary key."),
        ("SessionId", "Guid", "TEXT", True, "FK", "&rarr; <code>Sessions.Id</code>, cascade."),
        ("Hash", "string?", "TEXT(100)", False, "", ""),
        ("CreatedAt", "DateTime", "TEXT", True, "", ""),
        ("PresentMonRuntime", "string?", "TEXT(50)", False, "", "DXGI, D3D9, &hellip;"),
        ("SampleTime", "double", "REAL", True, "", "Length of the run in seconds."),
    ]),
    ("payload &middot; JSON", [
        ("CaptureDataJson", "string?", "TEXT", False, "", "Frame arrays. Empty for an indexed capture &mdash; the file on disk holds them."),
        ("SensorDataJson", "string?", "TEXT", False, "", "CPU and GPU temperatures, clocks, usage, power."),
        ("RtssFrameTimesJson", "string?", "TEXT", False, "", ""),
        ("PmdGpuPowerJson", "string?", "TEXT", False, "", ""),
        ("PmdCpuPowerJson", "string?", "TEXT", False, "", ""),
        ("PmdSystemPowerJson", "string?", "TEXT", False, "", ""),
    ]),
    ("frame rate", [
        ("MaxFps", "double?", "REAL", False, "", ""),
        ("P99Fps", "double?", "REAL", False, "", ""),
        ("P95Fps", "double?", "REAL", False, "", ""),
        ("AverageFps", "double?", "REAL", False, "", ""),
        ("MedianFps", "double?", "REAL", False, "", ""),
        ("P5Fps", "double?", "REAL", False, "", ""),
        ("P1Fps", "double?", "REAL", False, "", ""),
        ("P0_1Fps", "double?", "REAL", False, "", ""),
        ("P0_01Fps", "double?", "REAL", False, "", ""),
    ]),
    ("sensor averages", [
        ("AvgCpuTemp", "double?", "REAL", False, "", "&deg;C."),
        ("AvgGpuTemp", "double?", "REAL", False, "", "&deg;C."),
        ("AvgCpuPower", "double?", "REAL", False, "", "W."),
        ("AvgGpuPower", "double?", "REAL", False, "", "W."),
        ("AvgCpuUsage", "double?", "REAL", False, "", "%."),
        ("AvgGpuUsage", "double?", "REAL", False, "", "%."),
    ]),
]

ACCENT_GROUPS = {"record source", "list projection"}

HEADER_H = 54
GROUP_H = 20
ROW_H = 15
PAD = 10
CHAR_W = 6.9


def box_height(groups):
    height = HEADER_H + PAD
    for label, rows in groups:
        if label:
            height += GROUP_H
        height += ROW_H * len(rows)
    return height


def draw_box(x, y, w, title, subtitle, groups, rows_out):
    """Emits one entity box; records absolute row centres in rows_out."""
    height = box_height(groups)
    parts = [
        f'<rect class="tbl" x="{x}" y="{y}" width="{w}" height="{height}" rx="4"/>',
        f'<text class="th" x="{x + 14}" y="{y + 25}">{title}</text>',
        f'<text class="s" x="{x + 14}" y="{y + 42}">{subtitle}</text>',
        f'<line class="hl" x1="{x}" y1="{y + HEADER_H - 4}" x2="{x + w}" y2="{y + HEADER_H - 4}"/>',
    ]

    cursor = y + HEADER_H

    for label, rows in groups:
        if label:
            if label in ACCENT_GROUPS:
                band = GROUP_H + ROW_H * len(rows)
                parts.append(
                    f'<rect class="accentband" x="{x + 1}" y="{cursor}" '
                    f'width="{w - 2}" height="{band}"/>')
            parts.append(f'<text class="grp" x="{x + 14}" y="{cursor + 14}">{label}</text>')
            cursor += GROUP_H

        for name, _clr, sql, required, tag, _note in rows:
            centre = cursor + ROW_H / 2
            baseline = cursor + 11
            css = "col req" if required else "col"
            parts.append(f'<text class="{css}" x="{x + 14}" y="{baseline}">{name}</text>')

            if tag:
                parts.append(
                    f'<text class="tag" x="{x + 14 + len(name) * CHAR_W + 7}" '
                    f'y="{baseline}">{tag}</text>')

            parts.append(f'<text class="ty" x="{x + w - 14}" y="{baseline}">{sql}</text>')
            rows_out[(title, name)] = centre
            cursor += ROW_H

    return "\n        ".join(parts), height


def entity_svg():
    rows = {}
    suites, h_suites = draw_box(
        40, 40, 300, "Suites",
        "groups related sessions", SUITES, rows)
    sessions, h_sessions = draw_box(
        420, 40, 430, "Sessions",
        "one capture &mdash; a file on disk, or recorded live", SESSIONS, rows)
    runs, h_runs = draw_box(
        930, 40, 310, "SessionRuns",
        "one run inside a capture", RUNS, rows)

    height = 40 + max(h_suites, h_sessions, h_runs) + 40

    suite_id = rows[("Suites", "Id")]
    session_suite = rows[("Sessions", "SuiteId")]
    session_id = rows[("Sessions", "Id")]
    run_session = rows[("SessionRuns", "SessionId")]

    relations = f"""
        <path class="rel" d="M340,{suite_id} H380 V{session_suite} H414" marker-end="url(#ah)"/>
        <text class="card" x="346" y="{suite_id - 6}">1</text>
        <text class="card" x="398" y="{session_suite - 6}">N</text>
        <text class="s" x="380" y="{session_suite + 24}" text-anchor="middle">cascade delete</text>

        <path class="rel" d="M850,{session_id} H890 V{run_session} H924" marker-end="url(#ah)"/>
        <text class="card" x="856" y="{session_id - 6}">1</text>
        <text class="card" x="908" y="{run_session - 6}">N</text>
        <text class="s" x="890" y="{run_session + 24}" text-anchor="middle">cascade delete</text>"""

    return f"""<svg viewBox="0 0 1280 {height}" role="img" aria-label="Three tables. Suites on the left holds six columns and is the parent of Sessions in the middle, which holds thirty-four columns grouped into identity, game, hardware, drivers, system, record source and list projection. Sessions is the parent of SessionRuns on the right, which holds twenty-seven columns grouped into identity, JSON payload, frame rate metrics and sensor averages. Both relationships are one-to-many with cascade delete.">
        <defs>
          <marker id="ah" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" fill="currentColor"/>
          </marker>
        </defs>
        {suites}
        {sessions}
        {runs}
{relations}
      </svg>"""


def reference_table(title, groups):
    body = []
    for label, rows in groups:
        if label:
            body.append(
                f'      <tr class="grouprow"><th colspan="5">{label}</th></tr>')
        for name, clr, sql, required, tag, note in rows:
            marks = []
            if tag:
                marks.append(f'<span class="pill">{tag}</span>')
            body.append(
                "      <tr>"
                f"<td><code>{name}</code>{''.join(marks)}</td>"
                f"<td><code>{html.escape(clr)}</code></td>"
                f"<td><code>{sql}</code></td>"
                f"<td>{'&mdash;' if required else 'null'}</td>"
                f"<td>{note}</td>"
                "</tr>")

    return f"""    <h3><code>{title}</code></h3>
    <div class="tablewrap">
    <table>
      <thead><tr><th>Column</th><th>Model</th><th>Storage</th><th>Nullable</th><th>Note</th></tr></thead>
      <tbody>
{chr(10).join(body)}
      </tbody>
    </table>
    </div>"""


SHAPES_SVG = """<svg viewBox="0 0 1280 330" role="img" aria-label="Two shapes a session row takes. On the left an indexed capture: the Sessions row carries a source file path and the list projection, has no SessionRuns rows, and points at a capture file on disk that holds the frame data. On the right a session recorded by the service: the source file path is null and the SessionRuns rows carry the frame data as JSON.">
        <defs>
          <marker id="ah2" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" fill="currentColor"/>
          </marker>
        </defs>

        <rect class="band" x="30" y="20" width="600" height="290" rx="4"/>
        <text class="th" x="48" y="46">Indexed capture &mdash; the usual case</text>
        <text class="s" x="48" y="66">written by CapFrameX 1.x, copied in by hand, or on a share</text>

        <rect class="shared" x="48" y="84" width="564" height="74" rx="3"/>
        <text class="t" x="64" y="108">Sessions row</text>
        <text class="m" x="64" y="126">SourceFilePath = &apos;&hellip;\\Captures\\run.json&apos; &middot; IndexVersion = 1</text>
        <text class="m" x="64" y="142">DurationSeconds &middot; RunCount &middot; FrameCount &middot; SparklineJson</text>

        <path class="rel" d="M477,158 V186" marker-end="url(#ah2)"/>
        <text class="m" x="487" y="178">SourceFilePath</text>

        <rect class="ghost" x="48" y="190" width="270" height="54" rx="3"/>
        <text class="t" x="64" y="213">SessionRuns</text>
        <text class="s" x="64" y="232">none &mdash; nothing to store</text>

        <rect class="b" x="342" y="190" width="270" height="54" rx="3"/>
        <text class="t" x="358" y="213">run.json on disk</text>
        <text class="s" x="358" y="232">holds every frame time</text>

        <text class="s" x="48" y="270">the row is an index over the file</text>
        <text class="s" x="48" y="290">delete the file and the row goes with it on the next scan</text>

        <rect class="band" x="660" y="20" width="590" height="290" rx="4"/>
        <text class="th" x="678" y="46">Recorded by the service</text>
        <text class="s" x="678" y="66">a capture the service took itself</text>

        <rect class="plain" x="678" y="84" width="554" height="74" rx="3"/>
        <text class="t" x="694" y="108">Sessions row</text>
        <text class="m" x="694" y="126">SourceFilePath = NULL</text>
        <text class="m" x="694" y="142">no file behind it, so a folder scan says nothing about it</text>

        <rect class="b" x="678" y="190" width="554" height="54" rx="3"/>
        <text class="t" x="694" y="213">SessionRuns rows</text>
        <text class="m" x="694" y="232">CaptureDataJson &middot; SensorDataJson &middot; metrics per run</text>

        <text class="s" x="678" y="270">the row is the record</text>
        <text class="s" x="678" y="290">nothing outside the database has to survive for it to be readable</text>
      </svg>"""


PAGE = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>CapFrameX 2.0 Database Schema</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:wght@400;500;600&display=swap">
<style>
html{color-scheme:light dark}
body{margin:0}
:root{
  --bg:#FBFBF9; --surface:#FFFFFF; --subtle:#F1F2EF;
  --text:#1A1C1E; --muted:#5A6068; --faint:#878D95;
  --line:#C9CDD2; --line-strong:#8E949C;
  --accent:#185FA5; --accent-bg:#E6F1FB; --accent-line:#85B7EB;
  --alert:#B4481F; --alert-bg:#FAECE7;
  --good:#1D7F60;
}
@media (prefers-color-scheme: dark){
  :root:not([data-theme="light"]){
    --bg:#16181A; --surface:#1E2124; --subtle:#25292D;
    --text:#ECEDEA; --muted:#A9AEB4; --faint:#7C828A;
    --line:#3A4046; --line-strong:#6A7179;
    --accent:#85B7EB; --accent-bg:#11304D; --accent-line:#2F6FB0;
    --alert:#F0997B; --alert-bg:#43200F;
    --good:#5DCAA5;
  }
}
:root[data-theme="dark"]{
  --bg:#16181A; --surface:#1E2124; --subtle:#25292D;
  --text:#ECEDEA; --muted:#A9AEB4; --faint:#7C828A;
  --line:#3A4046; --line-strong:#6A7179;
  --accent:#85B7EB; --accent-bg:#11304D; --accent-line:#2F6FB0;
  --alert:#F0997B; --alert-bg:#43200F;
  --good:#5DCAA5;
}
body{background:var(--bg);color:var(--text);font-family:"IBM Plex Sans",system-ui,"Segoe UI",sans-serif;font-size:15px;line-height:1.55}
.page{max-width:1240px;margin:0 auto;padding-inline:20px;padding-block:36px 56px;display:flex;flex-direction:column;gap:40px}
header{display:flex;flex-direction:column;gap:10px}
.eyebrow{font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace;font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:var(--muted)}
h1{margin:0;font-size:30px;line-height:1.15;font-weight:600;text-wrap:balance}
h2{margin:0;font-size:19px;font-weight:600;text-wrap:balance}
h3{margin:22px 0 0;font-size:15px;font-weight:600}
h3 code{font-size:15px}
.lead{margin:0;max-width:70ch;color:var(--muted)}
section{display:flex;flex-direction:column;gap:14px}
.scroller{overflow-x:auto;border:1px solid var(--line);border-radius:6px;background:var(--surface)}
figure{margin:0;display:flex;flex-direction:column;gap:10px}
figcaption{max-width:78ch;color:var(--muted);font-size:14px}
svg{display:block;width:100%;min-width:1080px;height:auto;color:var(--text);font-family:"IBM Plex Sans",system-ui,sans-serif}
.b{fill:var(--surface);stroke:var(--line-strong);stroke-width:1}
.band{fill:var(--subtle);stroke:var(--line);stroke-width:1}
.shared{fill:var(--accent-bg);stroke:var(--accent-line);stroke-width:1.2}
.plain{fill:var(--subtle);stroke:var(--line-strong);stroke-width:1.2}
.ghost{fill:var(--surface);stroke:var(--line-strong);stroke-width:1;stroke-dasharray:4 4}
.tbl{fill:var(--surface);stroke:var(--line-strong);stroke-width:1.2}
.accentband{fill:var(--accent-bg)}
.hl{stroke:var(--line);stroke-width:1}
.t{fill:var(--text);font-size:12.5px;font-weight:600}
.th{fill:var(--text);font-size:14px;font-weight:600}
.s{fill:var(--muted);font-size:11px}
.m{fill:var(--muted);font-size:10.5px;font-family:"IBM Plex Mono",ui-monospace,monospace}
.grp{fill:var(--faint);font-size:10px;letter-spacing:.08em;text-transform:uppercase}
.col{fill:var(--muted);font-size:11.5px;font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace}
.col.req{fill:var(--text)}
.ty{fill:var(--faint);font-size:10px;font-family:"IBM Plex Mono",ui-monospace,monospace;text-anchor:end}
.tag{fill:var(--accent);font-size:9.5px;font-weight:500;font-family:"IBM Plex Mono",ui-monospace,monospace}
.card{fill:var(--accent);font-size:11px;font-family:"IBM Plex Mono",ui-monospace,monospace}
.rel{stroke:var(--accent);stroke-width:1.4;fill:none}
.a{stroke:currentColor;stroke-width:1.3;fill:none}
.legend{display:flex;flex-wrap:wrap;gap:8px 22px;font-size:13px;color:var(--muted)}
.legend span{display:inline-flex;align-items:center;gap:8px}
.sw{width:22px;height:12px;border-radius:2px;border:1.5px solid var(--line-strong);background:var(--surface)}
.sw.sh{background:var(--accent-bg);border-color:var(--accent-line)}
.sw.gh{border-style:dashed}
.key{font-family:"IBM Plex Mono",ui-monospace,monospace;color:var(--text);font-weight:600}
.keym{font-family:"IBM Plex Mono",ui-monospace,monospace;color:var(--muted)}
.tablewrap{overflow-x:auto}
table{border-collapse:collapse;width:100%;min-width:760px;font-size:14px}
th,td{text-align:left;vertical-align:top;padding:8px 12px;border-bottom:1px solid var(--line)}
thead th{font-size:12px;letter-spacing:.05em;text-transform:uppercase;color:var(--muted);font-weight:500}
.grouprow th{font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:var(--faint);font-weight:500;background:var(--subtle);padding-block:5px}
td:first-child{white-space:nowrap}
code{font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace;font-size:13px}
.pill{margin-left:7px;font-family:"IBM Plex Mono",ui-monospace,monospace;font-size:10px;color:var(--accent);border:1px solid var(--accent-line);border-radius:3px;padding:1px 4px;background:var(--accent-bg)}
.cols{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:14px 32px}
.cols div{display:flex;flex-direction:column;gap:2px;border-top:1px solid var(--line);padding-top:10px}
.cols b{font-family:"IBM Plex Mono",ui-monospace,monospace;font-size:13.5px;font-weight:500;color:var(--accent)}
.cols span{color:var(--muted);font-size:14px}
.open{margin:0;padding-left:18px;color:var(--muted);max-width:82ch}
.open li{margin-block:6px}
.open b{color:var(--text);font-weight:600}
</style>
</head>
<body>
<div class="page">
  <header>
    <div class="eyebrow">CapFrameX 2.0 &middot; Schema state 2026-09-20 &middot; Branch release/2.0.0 &middot; EF Core 10 on SQLite</div>
    <h1>Service Database Schema</h1>
    <p class="lead">Three tables. The database is an index over the capture files, not a second copy of them &mdash; which is what shapes almost every decision below.</p>
  </header>

  <section>
    <h2>Tables and relationships</h2>
    <figure>
      <div class="scroller">
      {entities}
      </div>
      <div class="legend">
        <span><span class="key">Column</span> not null</span>
        <span><span class="keym">Column</span> nullable</span>
        <span><span class="sw sh"></span> added by migration <code>AddRecordSource</code> for the record index</span>
        <span><span class="pill">PK</span> primary key</span>
        <span><span class="pill">FK</span> foreign key</span>
        <span><span class="pill">UQ</span> unique index</span>
      </div>
      <figcaption>Both relationships cascade: deleting a suite deletes its sessions, deleting a session deletes its runs. <code>Sessions.SuiteId</code> is required, so importing a capture always means creating or choosing a suite &mdash; there is no loose session.</figcaption>
    </figure>
  </section>

  <section>
    <h2>The two shapes a session takes</h2>
    <figure>
      <div class="scroller">
      {shapes}
      </div>
      <figcaption>The same table serves both, and <code>SourceFilePath</code> is what tells them apart. Frame data is never copied in for an indexed capture: it would double the storage and create a second version of the same capture that can drift from the first &mdash; and CapFrameX 1.x keeps writing those files.</figcaption>
    </figure>
  </section>

  <section>
    <h2>Column reference</h2>
{suites_table}
{sessions_table}
{runs_table}
  </section>

  <section>
    <h2>Indexes</h2>
    <div class="tablewrap">
    <table>
      <thead><tr><th>Table</th><th>Index</th><th>What it is for</th></tr></thead>
      <tbody>
        <tr><td rowspan="3"><code>Suites</code></td><td><code>Type</code></td><td>Filtering the library by suite kind.</td></tr>
        <tr><td><code>CreatedAt</code></td><td>Newest first.</td></tr>
        <tr><td><code>(Type, CreatedAt)</code></td><td>Both at once, which is how the library lists them.</td></tr>

        <tr><td rowspan="9"><code>Sessions</code></td><td><code>SourceFilePath</code> &mdash; unique, filtered to <code>NOT NULL</code></td><td><b>A rule, not a speed-up.</b> One file cannot enter the index twice. Filtered so that self-recorded sessions, which have no file, do not all collide on <code>NULL</code>.</td></tr>
        <tr><td><code>IndexVersion</code></td><td>Finds the rows a changed projection has to re-read, without a schema migration.</td></tr>
        <tr><td><code>GameName</code></td><td>Search and grouping.</td></tr>
        <tr><td><code>ProcessName</code></td><td>Search.</td></tr>
        <tr><td><code>Processor</code></td><td>Filtering by hardware.</td></tr>
        <tr><td><code>Gpu</code></td><td>Filtering by hardware.</td></tr>
        <tr><td><code>CreatedAt</code></td><td>The record list orders by it, newest first.</td></tr>
        <tr><td><code>(GameName, CreatedAt)</code></td><td>One game&apos;s captures over time.</td></tr>
        <tr><td><code>(SuiteId, CreatedAt)</code></td><td>One suite&apos;s captures over time.</td></tr>

        <tr><td rowspan="6"><code>SessionRuns</code></td><td><code>SessionId</code></td><td>Loading a capture&apos;s runs.</td></tr>
        <tr><td><code>(SessionId, CreatedAt)</code></td><td>In the order they were recorded.</td></tr>
        <tr><td><code>CreatedAt</code></td><td>Newest first across sessions.</td></tr>
        <tr><td><code>AverageFps</code></td><td>Sorting and range filters over metrics.</td></tr>
        <tr><td><code>P1Fps</code></td><td>Sorting and range filters over metrics.</td></tr>
        <tr><td><code>P99Fps</code></td><td>Sorting and range filters over metrics.</td></tr>
      </tbody>
    </table>
    </div>
  </section>

  <section>
    <h2>What the schema takes for granted</h2>
    <ul class="open">
      <li><b>The capture file is the record.</b> An indexed session stores a projection &mdash; duration, counts, a sparkline, two capability flags &mdash; and nothing else. Everything the analysis needs is read from the file when it is opened.</li>
      <li><b>Change detection is size plus last write time</b>, not a content hash. Hashing means reading every byte of a folder that runs to hundreds of megabytes, on every scan, to answer a question the file system already answers.</li>
      <li><b>A version column replaces a migration for projection changes.</b> Raising <code>RecordIndexPlanner.CurrentIndexVersion</code> makes the next scan re-read every record; a row written by a <i>newer</i> service is left alone, so an older one sharing the database cannot undo it.</li>
      <li><b>Large arrays are JSON, not rows.</b> A run holds ten thousand frames and more; a row per frame would mean millions of rows and joins across them for every chart.</li>
      <li><b>The frame-rate metrics come from the analysis, not from the index.</b> <code>RecordIndex</code> asks the same adapter over <code>CapFrameX.Statistics.NetStandard</code> that the analysis view asks, and stores the answer. A second calculation here is how the list and the open record would come to disagree.</li>
    </ul>
  </section>

  <section>
    <h2>Where it lives, and how it is built</h2>
    <div class="cols">
      <div><b>Windows</b><span><code>%LOCALAPPDATA%\\CapFrameX\\capframex.db</code></span></div>
      <div><b>Linux</b><span><code>$XDG_DATA_HOME/capframex/capframex.db</code>, falling back to <code>~/.local/share</code></span></div>
      <div><b>Portable mode</b><span>next to the binaries, under the portable root</span></div>
      <div><b>Never hard-coded</b><span>the path always comes from <code>IAppPaths.DataDirectory</code></span></div>
    </div>
    <div class="tablewrap">
    <table>
      <thead><tr><th>Migration</th><th>Brings</th></tr></thead>
      <tbody>
        <tr><td><code>20251226142228_InitialCreate</code></td><td>Suites, Sessions, SessionRuns and their indexes.</td></tr>
        <tr><td><code>20260920153818_AddRecordSource</code></td><td>The record source and the list projection on <code>Sessions</code>; the JSON columns on <code>SessionRuns</code> become optional; the unique filtered index and the version index.</td></tr>
        <tr><td><code>20260920164331_AddRecordMetrics</code></td><td>The frame-rate metrics the record list shows, on <code>Sessions</code>. <code>IndexVersion</code> went to 2 with it, which is what makes the existing rows re-read themselves.</td></tr>
      </tbody>
    </table>
    </div>
    <p class="lead"><code>DatabaseMigrationService</code> applies pending migrations at start-up and refuses to start when it cannot, so nothing has to be run by hand on a user&apos;s machine. New migrations are generated against <code>CapFrameX.Service.Windows/tools/CapFrameX.DatabaseTool</code> as the startup project &mdash; <code>Microsoft.EntityFrameworkCore.Design</code> is deliberately absent from the data project, because it dragged Roslyn and MSBuild into the service publish.</p>
  </section>
</div>
</body>
</html>
"""


def main():
    page = PAGE.replace("{entities}", entity_svg())
    page = page.replace("{shapes}", SHAPES_SVG)
    page = page.replace("{suites_table}", reference_table("Suites", SUITES))
    page = page.replace("{sessions_table}", reference_table("Sessions", SESSIONS))
    page = page.replace("{runs_table}", reference_table("SessionRuns", RUNS))

    target = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cx2-database-schema.html")
    io.open(target, "wb").write(page.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))
    print("written", target, len(page), "chars")


main()
