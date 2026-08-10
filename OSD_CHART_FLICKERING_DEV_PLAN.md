# CapFrameX.OSD: Dev-Plan gegen Chart-Flickering und Scroll-Judder

**Stand:** 2026-08-03

**Referenzstand des analysierten OSD-Repositories:** `736181e` (`Optimize chart replay`)

**Status:** Planungsgrundlage, noch keine der beschriebenen Verhaltensänderungen umgesetzt

**Quellpfade:** relativ zum nativen CMake-Projektordner `CapFrameX.OSD/CapFrameX.OSD`
innerhalb des separaten OSD-Repositories

## 1. Kurzfassung

Die beobachtete Unruhe bei einer Chart-Update-Rate von 30 Hz ist nach der Codeanalyse primär
kein klassisches Ausblenden einzelner Overlay-Frames. Der aktuelle External-v2-Pfad zeichnet die
zuletzt hochgeladene Chart-Geometrie bei jedem Game-`Present`, bewegt sie zwischen zwei
Producer-Updates aber nicht. Bei 30 Hz entsteht deshalb ein Sample-and-Hold-Muster: Der Chart
steht ungefähr 33,3 ms und springt anschließend auf die neue Position. An kontrastreichen,
dichten Frametime-Linien wirkt das als Flickering, Shimmering oder Scroll-Judder.

Alex' Hinweis im X-Thread ist damit grundsätzlich richtig: Eine niedrigere OSD-Update-Rate kann
Arbeit sparen, kostet aber Scroll-Glätte. Für den neuen CapFrameX-Renderer ist der Trade-off
jedoch anders als bei einem vollständigen Redraw pro Frame, weil Text und Chart-Geometrie bereits
gecached werden. Das Ziel ist deshalb **nicht**, wieder den kompletten Chart bei jedem Present zu
erzeugen. Stattdessen sollen diese beiden Raten getrennt werden:

```text
PresentMon-Daten
      |
      v
Replay-Puffer und Clock
      |
      | 30/60/90 Hz: Snapshot, Decimation, Tessellierung, Upload nur bei neuer Generation
      v
publizierte Chart-Geometrie + Zeitanker + Guard-Bereich
      |
      | bei jedem Game-Present: Zeitfortschritt -> kleiner X-Offset -> Clip -> Draw
      v
kontinuierlich scrollender Chart
```

Die Umsetzung erfolgt in dieser Reihenfolge:

1. Messbarkeit und reproduzierbare Tests ergänzen.
2. Die Triple-Buffer-Übergaben und den GPU-Cache korrekt gegen Überlappung und Fehler absichern.
3. Per-Present-Scrollen für den DXGI-External-v2-Pfad implementieren.
4. Producer-Scheduling stabilisieren und erst danach die sinnvolle Produktionsrate bestimmen.
5. Dichte Kurven durch pixelbasierte Decimation, saubere Joins und Anti-Aliasing beruhigen.
6. Den Vulkan-Pfad in Text- und Chart-Layer aufteilen und dort dasselbe Bewegungsmodell nutzen.

## 2. Kontext aus dem X-Thread

Der Thread wurde über die X API verifiziert:

- [CapFrameX zum optimierten Renderer](https://x.com/CapFrameX/status/2083919824791474622)
- [Alex Unwinder zur Abhängigkeit von OSD-Inhalt und Update-Rate](https://x.com/AlexUnwinder/status/2083985224195518629)
- [CapFrameX zur beobachteten 30-Hz-Flickering-Problematik](https://x.com/CapFrameX/status/2084169292950466972)

Alex beschreibt, dass RTSS für flüssige Custom-Overlay-Animationen beziehungsweise einen
Frametime-Graphen den Inhalt pro Frame vollständig neu zeichnen kann. Eine Begrenzung auf etwa
30 Hz kann den Aufwand reduzieren, macht das Scrollen aber sichtbar gröber. Die relevante Frage
für CapFrameX lautet daher nicht nur „30 oder 90 Hz?“, sondern: **Welche Arbeit muss wirklich mit
der sichtbaren Framerate laufen, und welche kann unabhängig davon gecached bleiben?**

## 3. Bestätigte technische Ausgangslage

### 3.1 External-v2/DXGI

- `src/core/OsdInstance.cpp:1018-1212` erzeugt Text und Chart-Daten im Background-Producer.
- Text wird langsam aktualisiert (`textHz`, standardmäßig 5 Hz) und als CPU-Raster publiziert.
- Chart-Punkte werden mit der Producer-Rate erzeugt und in `ChartFrame` tesselliert.
- `src/core/OsdInstance.cpp:923-978` lädt eine neue Chart-Generation einmal hoch und zeichnet die
  gecachte Geometrie anschließend bei jedem Game-`Present` erneut.
- Die Standardrate des External-v2-Producers beträgt 90 Hz; `bgHz` kann sie im Debug-Betrieb auf
  5 bis 240 Hz ändern.
- Die Commits `b8271bd` (`Cache chart geometry`) und `736181e` (`Optimize chart replay`) haben
  bereits viel redundante Arbeit beseitigt. Sie führen aber keine zeitliche Interpolation zwischen
  zwei Geometrie-Generationen aus.

Folge: Eine Absenkung von 90 auf 30 Hz spart hauptsächlich CPU-Tessellierung und gelegentliche
Vertex-Uploads. Composite, State-Wechsel und Draw bleiben weiterhin pro Present aktiv. Der
visuelle Nachteil der niedrigeren Rate ist deshalb größer als die eingesparte Per-Present-Arbeit.

### 3.2 Sichtbare Schrittweite

Der Standard-Chart ist 248 px breit. Nach 8 px Innenabstand auf beiden Seiten bleiben 232 px
Plotbreite. Bei einem Standard-Zeitfenster von 5 s ergibt sich folgende horizontale Bewegung je
Producer-Update:

| Producer-Rate | Abstand zweier Updates | Bewegung bei Zoom 1,0 |
|---:|---:|---:|
| 30 Hz | 33,33 ms | 1,55 px |
| 60 Hz | 16,67 ms | 0,77 px |
| 90 Hz | 11,11 ms | 0,52 px |
| 120 Hz | 8,33 ms | 0,39 px |

Formel: `Schrittweite = Plotbreite / (Zeitfenster in Sekunden * Producer-Hz)`.

Bei Zoom 2,0 verdoppelt sich die Schrittweite. Ein kürzeres Zeitfenster erhöht sie ebenfalls.
Das erklärt, warum 30 Hz trotz eines Draws pro Present sichtbar unruhig wirken kann. Mit einer
Per-Present-Transformation läge die Bewegung bei 240 Game-FPS dagegen bei ungefähr 0,19 px pro
Present, ohne dass die Geometrie 240-mal pro Sekunde neu erzeugt werden müsste.

### 3.3 Dichte Geometrie verstärkt Shimmering

Bei 240 FPS enthält ein 5-s-Fenster ungefähr 1.200 Samples. Auf 232 Plotpixel entfallen damit
rund 5,2 Samples pro Pixel. Der aktuelle Pfad:

- bildet alle Samples direkt auf Pixelkoordinaten ab (`src/core/Widget.cpp:356-430`),
- besitzt keine Min/Max-Decimation pro X-Pixel,
- expandiert jedes Liniensegment als unabhängiges Quad
  (`src/core/ChartGeometry.h:25-55`),
- besitzt keine echten Linien-Joins und keine explizite Edge-Coverage für Anti-Aliasing.

Subpixel-Bewegung ändert dadurch die Rasterabdeckung vieler überlappender Dreiecke gleichzeitig.
Das kann zusätzlich zum zeitlichen 30-Hz-Sprung als Helligkeitsflimmern wahrgenommen werden.

### 3.4 Producer-Takt kann unter VRR ungleichmäßig werden

`src/core/OsdInstance.cpp:1235-1257` wartet auf Present-Signale und prüft danach, ob eine Periode
vergangen ist. Nach einem Update wird `last = now` gesetzt. Überschrittene Restzeit wird damit
verworfen. Bei einer Present-Rate, die kein ganzzahliges Vielfaches der Producer-Rate ist, sowie
unter VRR können wechselnde Update-Abstände und langfristiger Phasendrift entstehen. Der Chart
springt dann nicht nur selten, sondern auch mit variierender Schrittweite.

### 3.5 Sekundäre Risiken können echtes Flickering erzeugen

Neben dem dominanten Sample-and-Hold-Effekt bestehen reale Synchronisations- und Fehlerpfade:

- `ChartFrame` wird über einen Triple Buffer und einen atomaren Front-Index übergeben. Der Consumer
  pinnt den gelesenen Slot nicht. Wird der Present-Thread lange präemptiert, kann der Producer den
  Slot nach zwei zwischenzeitlichen Publikationen erneut beschreiben, während Vektoren noch gelesen
  werden. Die übliche Laufzeit macht das selten, nach dem C++-Speichermodell ist es dennoch ein
  Datenrennen.
- Für CPU-Text-Slots gilt dasselbe grundsätzliche Lifetime-Problem, wenn ein Consumer ungewöhnlich
  lange pausiert.
- Im Vulkan-Pfad kann ein CPU-Writer einen Staging-Slot wiederverwenden, obwohl ein bereits
  aufgezeichnetes `vkCmdCopyBufferToImage` diesen Bereich möglicherweise noch benötigt. Die
  vorhandenen Per-Image-Fences schützen Swapchain-Ressourcen, melden dem CPU-Producer aber nicht
  die Belegung des konkreten Staging-Slots.
- Schlägt `Map` oder ein notwendiges Buffer-Wachstum beim Chart-Upload fehl, wird der Cache derzeit
  invalidiert. Dadurch kann der Chart für mindestens einen Present verschwinden, obwohl die zuvor
  erfolgreich geladene Geometrie noch verwendbar wäre.

Diese Punkte sind wahrscheinlich nicht die typische 30-Hz-Ursache, müssen aber vor einer
komplexeren Interpolation bereinigt werden, damit echte Tearing-/Blank-Frames nicht mit Scroll-
Judder vermischt werden.

### 3.6 Vulkan und Windowed-DWM sind getrennte Fälle

- `createCpu` verwendet standardmäßig 30 Hz. Der Vulkan-Feed erhält ein vollständig gerastertes
  Bild, in dem Text und Chart bereits zusammengeführt sind. Dieses Bild wird pro Present erneut
  composited; ein reiner X-Offset würde aktuell auch Text und Panel verschieben.
- Der Windowed-DWM-Pfad besitzt mit `ScrollChart` bereits einen compositor-animierten Marquee-
  Versuch. Kontinuierliche DComp-Animationen wurden jedoch als messbar teuer dokumentiert und sind
  deshalb absichtlich opt-in. Dieser Pfad darf nicht ungeprüft zum Standard werden.
- `src/core/ScrollChart.h` bezeichnet eine langlaufende DComp-Translation noch als kostenlos. Das
  widerspricht den neueren Messnotizen in `CLAUDE.md` und `OsdInstance.cpp` und soll bereinigt werden.

## 4. Zielbild und Nicht-Ziele

### 4.1 Funktionale Ziele

- Solange der Replay-Puffer Daten enthält, bewegt sich der Chart bei jedem Game-`Present` weiter.
- Daten-Snapshot, Decimation und Tessellierung bleiben auf eine konfigurierbare Producer-Rate
  begrenzt.
- Ein Generationenwechsel erzeugt keinen Rücksprung, keinen doppelten Sprung und keinen leeren
  Chart-Frame.
- Bei echter Daten-Starvation hält der Chart bewusst an; es werden keine Frametime-Daten erfunden.
- DXGI und Vulkan verwenden langfristig dasselbe Zeitanker-/Advance-Modell.
- Layout-Wechsel, Zoom, Zeitfensteränderung, Resize und Device-Recreation setzen den Bewegungsanker
  kontrolliert zurück.

### 4.2 Performance-Ziele

- Der DXGI-Present-Fast-Path bleibt allokations-, mutex- und wait-frei.
- Pro Present kommt höchstens ein kleiner Zeit-/Transform-Update hinzu; Tessellierung und
  Vertex-Upload laufen nicht mit der Game-Framerate.
- Die 1%- und 0,1%-Lows verschlechtern sich gegenüber dem aktuellen 90-Hz-Stand nicht außerhalb
  der normalen Messstreuung.
- Die Anzahl der Chart-Vertices wird durch die Plotbreite begrenzt statt durch Game-FPS mal
  Zeitfenster zu wachsen.

### 4.3 Nicht-Ziele

- Kein vollständiger OSD-Redraw bei jedem Present.
- Keine ungebremste Extrapolation über das neueste gepufferte Sample hinaus.
- Kein automatisches Aktivieren des DComp-Marquee-Pfads ohne erneute GPU-/MPO-Messung.
- Keine sofortige öffentliche UI-Option für alle Debug-Knöpfe. Zunächst werden sichere Defaults
  und ein interner Kill-Switch benötigt.
- Keine Änderung der Frametime-Werte oder der 1%-/0,1%-Low-Berechnung; der Plan betrifft die
  Darstellung und Übergabe der Chart-Daten.

## 5. Priorisierte Arbeitspakete

| ID | Priorität | Paket | Nutzen | Aufwand | Abhängigkeit |
|---|---|---|---|---|---|
| CFX-CHART-001 | P0 | Messbarkeit und deterministische Motion-Tests | trennt Judder, Starvation und echte Blank-Frames | M | keine |
| CFX-CHART-002 | P0 | Slot-Ownership für Chart, Text und Vulkan-Staging | beseitigt Datenrennen und GPU-Lifetime-Risiko | M–L | 001 |
| CFX-CHART-003 | P0 | Transaktionaler Chart-Upload mit Stale-Fallback | verhindert leere Frames bei transienten Fehlern | S–M | 001 |
| CFX-CHART-004 | P1 | Per-Present-Zeitanker, X-Offset, Guard und Clipping für DXGI | beseitigt den sichtbaren 30-Hz-Hold | L | 002, 003 |
| CFX-CHART-005 | P1 | Deadline-basierter Producer-Takt | gleichmäßige Generationen unter VRR | M | 001 |
| CFX-CHART-006 | P1 | Min/Max-Decimation pro Pixelspalte | weniger Geometrie, Spitzen bleiben sichtbar | M | 004 |
| CFX-CHART-007 | P2 | Stabile Joins und analytisches Anti-Aliasing | reduziert Subpixel-Shimmering | M–L | 006 |
| CFX-CHART-008 | P1 | Getrennter Text-/Chart-Layer für Vulkan | Per-Present-Scrollen ohne Textbewegung | L–XL | 002, 004 |
| CFX-CHART-009 | P2 | DWM-Dokumentation und experimentelle Backend-Policy | verhindert falsche Annahmen/Defaults | S | 001 |
| CFX-CHART-010 | P1 | Rate-Auswahl und kontrollierter Rollout | evidenzbasierter 30/60/90-Hz-Default | S–M | 004–008 |

## 6. Phase 0 – Messbarkeit und reproduzierbarer Baseline-Test

### 6.1 Instrumentierung

Opt-in-Diagnostik ergänzen, ohne im Present-Hotpath zu loggen:

- Producer-QPC, Producer-Intervall und Chart-Generation.
- hochgeladene und tatsächlich gezeichnete Generation.
- berechneter visueller Play-Cursor, X-Offset und Clamp-Grund
  (`buffer-end`, `rebuffering`, `layout-reset`, `no-frame`).
- Anzahl Slot-Konflikte, ausgelassene Publikationen und Upload-Retries.
- Sample-Anzahl vor/nach Decimation und erzeugte Vertex-Anzahl.
- bestehende Present-Stats um den Chart-Transform-Anteil erweitern.

Samples gehen in einen vorallokierten Ring und werden außerhalb des Present-Threads aggregiert.
Die produktive Standardeinstellung bleibt ohne zusätzliche Dateiausgaben.

### 6.2 Deterministischer Motion-Test

Neuen reinen CPU-Test `apps/chart_motion_test` anlegen. Er simuliert:

- Game-Presents mit 30, 60, 120, 144, 165 und 240 Hz,
- Producer mit 30, 60 und 90 Hz,
- VRR-artige Present-Abstände,
- pünktliche Daten, Burst-Zustellung und echte Starvation,
- Generationenwechsel, Resize, Zoom und Wechsel des Chart-Zeitfensters.

Der Test protokolliert nicht Bilder, sondern die berechnete visuelle Zeit und X-Position. Er prüft:

- monotone Bewegung, solange Advance erlaubt ist,
- höchstens einen Present lang Stillstand bei vorhandenen Daten,
- keinen Rücksprung beim Generationenwechsel,
- keine Extrapolation über den freigegebenen Guard hinaus,
- einen definierten Reset bei Layout-/Timeline-Wechsel.

### 6.3 Visuelle Referenz

Den bestehenden External-Testhost um eine synthetische, kontrastreiche Frametime-Sequenz mit
einzelnen Spikes erweitern. Für jede Kombination aus `bgHz`, Game-FPS, Zoom und Zeitfenster werden
eine Sequenz oder ausgewählte Frames erzeugt. Zusätzlich erfolgt mindestens eine 240-FPS-
Highspeed-Aufnahme in einem echten DX11- und einem Vulkan-Titel.

### 6.4 Exit-Kriterien Phase 0

- Das aktuelle Sample-and-Hold-Verhalten ist in der Motion-Simulation messbar reproduziert.
- Ein bewusst ausgelöster Uploadfehler und eine simulierte Producer-Verzögerung sind eindeutig
  von normalem 30-Hz-Judder unterscheidbar.
- Baseline-Werte für `hookTotal`, Producer-Zeit, Uploadrate und 1%-/0,1%-Lows liegen für die
  Abnahmematrix aus Abschnitt 13 vor.

## 7. Phase 1 – Übergaben und Cache fehlertolerant machen

### 7.1 Explizites Slot-Ownership

Den impliziten „drei Slots sind wahrscheinlich lang genug“-Vertrag durch ein gemeinsames,
nicht-blockierendes Ownership-Protokoll ersetzen:

- Producer reserviert ausschließlich einen freien Slot, markiert ihn als `Writing` und publiziert
  ihn nach vollständigem Schreiben per Release-Operation.
- Consumer erwirbt eine Lease beziehungsweise erhöht einen Referenzzähler, prüft danach erneut
  Generation und Front-Index und liest erst dann die Payload.
- Ein abgelöster Slot wird erst wieder `Free`, wenn kein Consumer und – bei Vulkan – kein GPU-Submit
  mehr darauf verweist.
- Ist kein Slot frei, wartet der Producer nicht. Er verwirft beziehungsweise verschiebt genau
  dieses Update und behält die zuletzt gültige Publikation bei.

Das Protokoll sollte als kleiner, testbarer `PublishedRing<T, N>`-Baustein umgesetzt werden. Für
Vulkan benötigt jeder Staging-Slot zusätzlich einen In-flight-Refcount oder eine Fence-Zuordnung.
Beim Signalisieren beziehungsweise Einsammeln des Submit-Fences wird die Referenz freigegeben.

### 7.2 Transaktionaler GPU-Cache

`ChartLayer::uploadCachedGeometry` darf die letzte gültige Geometrie nicht zerstören, bevor der
Ersatz vollständig bereitsteht:

- Bei ausreichender Kapazität: `Map`/Kopie/`Unmap`; erst danach Generation und Ranges committen.
- Bei notwendigem Wachstum: neuen Buffer als Kandidat anlegen, befüllen und erst nach Erfolg gegen
  den aktiven Buffer tauschen.
- Bei Fehler: alte Generation weiter zeichnen und neue Generation beim nächsten Present erneut
  versuchen.
- Alte Geometrie nur dann ausblenden, wenn sich Panel-/Layout-Abmessungen geändert haben und eine
  falsche Zuordnung sichtbarer wäre als ein fehlender Chart.
- Leere neue Geometrie bleibt ein gültiger Commit und entfernt bewusst die alte Linie.

### 7.3 Tests

`apps/chart_geometry_test` erweitern um:

- Lease während mehrerer Producer-Publikationen,
- Slot-Sättigung ohne Blockieren oder Überschreiben,
- Generation-Wrap beziehungsweise lange Sequenzen,
- fehlgeschlagenen Map-/Resize-Versuch mit weiter gültigem alten Cache,
- leere Generation als bewusste Invalidierung,
- Layout-Mismatch als kontrolliertes Ausblenden.

### 7.4 Exit-Kriterien Phase 1

- Kein Slot kann während einer CPU- oder GPU-Nutzung erneut beschrieben werden.
- Producer und Present-Thread warten niemals gegenseitig.
- Ein transienter Uploadfehler erzeugt bei identischem Layout keinen leeren Chart-Present.
- Debug-Zähler zeigen in einem 30-minütigen Stresstest keine ungültige Lease und keinen
  Staging-Reuse vor Fence-Abschluss.

## 8. Phase 2 – Per-Present-Scrollen im DXGI-External-v2-Pfad

### 8.1 Erweiterte Chart-Publikation

`ChartFrame` erhält zusätzlich zu Vertices, Ranges und Panelgröße:

- `anchorPlayTimeMs`: Replay-Zeit, auf die die Geometrie ausgerichtet wurde.
- `anchorQpc`: QPC-Zeitpunkt desselben Clock-Snapshots.
- pro Chart-Range `plotRect` und `pixelsPerMs`.
- `maxAdvanceMs`: maximal erlaubter visueller Vorlauf aus tatsächlich gepufferten Daten.
- Layout-/Timeline-Generation und Rebuffering-Status.

Der QPC-Zeitpunkt muss aus demselben Producer-Tick stammen wie der Aufruf von
`ReplayClock::advance`; ein erst nach der Tessellierung gelesener Zeitanker würde bereits beim
Publish einen sichtbaren Phasenfehler einbauen.

### 8.2 Guard-Geometrie

Der Producer baut nicht nur exakt das sichtbare Fenster
`[playT - graphInterval, playT]`, sondern nimmt Nachbarpunkte und einen kleinen zeitlichen Guard
links und rechts auf. Empfehlung für den ersten Stand:

```text
guardMs = clamp(2 * producerPeriodMs + measuredJitterReserveMs, 20 ms, 250 ms)
maxAdvanceMs = min(guardMs, latestBufferedSampleMs - anchorPlayTimeMs)
```

Der sichtbare Plot bleibt unverändert breit; außerhalb liegende Guard-Geometrie wird geclippt.
Je ein Nachbarsample außerhalb beider Grenzen verhindert offene Linienenden an der Clipkante.
Während `ReplayClock::rebuffering()` ist `maxAdvanceMs = 0`, bis wieder ein ausreichender Puffer
vorhanden ist.

### 8.3 Present-seitige Transformation

Pro Present wird ausschließlich gerechnet:

```text
elapsedMs       = max(0, qpcNow - anchorQpc)
visualAdvanceMs = clamp(elapsedMs, 0, maxAdvanceMs)
xOffsetPx       = -visualAdvanceMs * pixelsPerMs
```

Der Vertex-Shader addiert `xOffsetPx` in Panel-Pixelkoordinaten vor der NDC-Transformation.
`ChartGeometryRange` trägt den Plot-Clip; der Renderer setzt pro Range einen Scissor-Rect oder
verwirft außerhalb liegende Pixel im Shader. Bei D3D11-Scissoring muss der Hook-State-Backup-
Pfad Scissor-State und Rects nachweislich vollständig sichern und wiederherstellen.

Der Present-Thread führt einen monotonen visuellen Play-Cursor. Bei einer neuen Generation darf
dieser Cursor nicht zurückspringen. Ist der neue Anker leicht hinter der bereits dargestellten
Zeit, wird die Differenz als Offset innerhalb des neuen Guards übernommen. Größere Abweichungen
werden als Timeline-/Layout-Reset diagnostiziert und kontrolliert neu verankert.

### 8.4 Feature-Flag und Fallback

- Zunächst interner Schalter `smoothChartScroll` in `OsdDebug.json`.
- Bei ungültigen Metadaten, fehlendem Guard oder Layout-Mismatch: sichere statische Darstellung
  der letzten gültigen Generation.
- Nach bestandener Abnahmematrix wird der Schalter im External-v2-Pfad standardmäßig aktiv; ein
  Kill-Switch bleibt mindestens einen Release-Zyklus erhalten.

### 8.5 Exit-Kriterien Phase 2

- Bei gefülltem Replay-Puffer ändert sich die Chart-Position bei jedem Game-Present.
- Bei 30-Hz-Producer und 240 Game-FPS gibt es keine 33-ms-Holds mehr.
- Publikationswechsel bleiben innerhalb von
  `1,25 * erwartete Present-Bewegung + 0,25 px`; kein Rücksprung ist zulässig.
- Der Present-Pfad bleibt ohne Heap-Allokation, Mutex und Wait.
- Der zusätzliche CPU-Aufwand im `hookTotal`-p99 beträgt gegenüber dem aktuellen Cached-Draw-
  Pfad höchstens 10 µs; die praktische Zielgröße ist deutlich darunter.

## 9. Phase 3 – Gleichmäßiger Producer-Takt und Rate-Auswahl

### 9.1 Deadline statt `last = now`

Den Producer mit einer absoluten nächsten Deadline betreiben:

- Phasenrest nach einer verspäteten Ausführung erhalten.
- Verpasste Perioden überspringen, aber niemals in einem Catch-up-Burst nachproduzieren.
- Weiterhin höchstens eine Generation pro beobachtetem Game-Present erzeugen.
- Wenn das Spiel nicht präsentiert, idle bleiben und die Deadline beim Wiederanlauf neu ankern.
- `dt` aus zwei tatsächlichen Producer-Zeitpunkten berechnen; Deadline und Simulationszeit nicht
  vermischen.

Die Taktlogik wird in einen reinen CPU-Helfer ausgelagert und mit festen sowie jitternden Present-
Sequenzen getestet.

### 9.2 Producer-Raten entkoppeln

Langfristig drei Begriffe sauber trennen:

- `textHz`: Rasterisierung statischer/numerischer Inhalte.
- `chartHz`: Snapshot, Decimation und Chart-Geometrie beziehungsweise Chart-Raster.
- Present-Rate: ausschließlich Transform und Composite.

`bgHz` bleibt zunächst kompatibler Debug-Alias. Eine neue öffentliche Einstellung ist erst nötig,
wenn Messungen einen sinnvollen Nutzer-Trade-off zeigen.

### 9.3 Default nicht vorab festlegen

Nach Phase 2 werden 30, 60 und 90 Hz verglichen:

- Ist 30 Hz visuell gleichwertig und verbessert messbar Lows beziehungsweise Producer-Kosten,
  kann es Default werden.
- Ist der Performancegewinn innerhalb der Streuung, bleiben 60 oder 90 Hz sinnvoller, weil neue
  Samples und Y-Skalierung schneller sichtbar werden.
- DXGI und Vulkan dürfen unterschiedliche Defaults behalten, solange ihre Arbeit pro Generation
  strukturell verschieden ist.

### 9.4 Exit-Kriterien Phase 3

- In einer 10-minütigen Simulation bleibt der Taktfehler auf höchstens ein Game-Present-Intervall
  begrenzt und akkumuliert nicht.
- VRR erzeugt keine wiederkehrende Lang-/Kurz-Periode durch verlorene Restzeit.
- Die ausgewählte Produktionsrate basiert auf mindestens drei vergleichbaren Captures je Variante
  und ist im Dokument mit Median und Streuung begründet.

## 10. Phase 4 – Pixelbasierte Decimation und Linienqualität

### 10.1 Min/Max-Envelope pro Pixelspalte

Vor der Tessellierung werden timestamp-basierte Samples nach X-Pixelspalte gruppiert. Pro Spalte
bleiben in zeitlicher Reihenfolge höchstens erhalten:

- erster Wert,
- Minimum,
- Maximum,
- letzter Wert.

Minimum und Maximum müssen entsprechend ihrer ursprünglichen Zeitreihenfolge ausgegeben werden.
So bleiben einzelne Frametime-Spikes sichtbar, während redundante Subpixel-Zickzack-Geometrie
entfällt. Je ein Punkt aus der Nachbarspalte sowie die Guard-Samples sichern Linienkontinuität.

Erwartete Obergrenze: `O(Plotbreite)` Punkte und maximal ungefähr vier Punkte pro Pixelspalte,
unabhängig davon, ob der Titel 60 oder 500 FPS liefert.

### 10.2 Saubere Joins und Anti-Aliasing

Die unabhängigen Segment-Quads werden durch eine zusammenhängende Liniengeometrie ersetzt:

- Bevel- oder begrenzte Miter-Joins; Miter-Limit gegen extreme Spikes.
- definierte End-Caps.
- Coverage-Attribut und etwa 1 px breiter Alpha-Fringe beziehungsweise analytische Distanz im
  Pixel-Shader.
- weiterhin premultipliziertes Alpha.

Analytisches Single-Sample-AA ist MSAA vorzuziehen, weil das Game-Target nicht zuverlässig
multisampled ist und der Overlay-Pfad keine Auflösung des Game-Targets kontrollieren darf.

### 10.3 Tests und Exit-Kriterien Phase 4

- Ein ein-Sample-breiter Spike bleibt nach Decimation in Höhe und zeitlicher Position erhalten.
- Punktreihenfolge bleibt monoton; leere/degenerierte Spalten erzeugen keine NaNs.
- Vertex-Anzahl bleibt für zwei Standardcharts bei einem festen Vielfachen der Plotbreite.
- Akute Winkel zeigen weder Löcher noch unbeschränkte Miter-Spitzen.
- Eine langsame Subpixel-Fahrt über kontrastreichem Hintergrund zeigt in der Referenzaufnahme
  deutlich weniger Helligkeitspumpen als der Quad-Baseline-Pfad.
- GPU- und Present-Kosten werden mit AA an/aus gemessen; bei einer relevanten Regression bleibt
  AA über einen Backend-Kill-Switch deaktivierbar.

## 11. Phase 5 – Vulkan-Parität

### 11.1 Kurzfristige Absicherung

- Das Slot-/Fence-Ownership aus Phase 1 zuerst auf den vorhandenen Staging-Ring anwenden.
- Bis zur Layer-Trennung 30 gegen 60 Hz messen. Eine temporäre Anhebung auf 60 Hz ist zulässig,
  wenn sie das sichtbare Problem klar reduziert und die Lows nicht relevant verschlechtert.
- Dies ist nur ein Zwischenzustand, nicht das Zielmodell.

### 11.2 Text und Chart trennen

Der CPU-Producer liefert zwei logisch getrennte Ebenen:

1. Panel, Text, Labels und Hintergründe mit langsamer `textHz`-Kadenz.
2. Transparente Chart-Linien mit Guard-Bereich, Zeitanker und Plot-Clips mit `chartHz`-Kadenz.

Der Vulkan-Compositor zeichnet zwei Quads. Der Text-Quad bleibt ortsfest; nur die UVs
beziehungsweise die Geometrie des Chart-Quads erhalten pro Present den zeitbasierten X-Offset.
Außerhalb der Plot-Rechtecke wird der Chart verworfen. Damit lässt sich das DXGI-Verhalten
zunächst ohne einen vollständigen nativen Vulkan-Linienrenderer abbilden.

Die Callback-/C-ABI wird versioniert erweitert. Der bestehende kombinierte CPU-Frame bleibt als
Fallback erhalten, bis x64- und x86-Layer sowie die ausgelieferten Prebuilt-Binaries gemeinsam
aktualisiert sind.

### 11.3 Langfristige Option

Wenn die zusätzliche Chart-Rasterfläche oder deren Upload messbar teuer bleibt, kann Vulkan
dieselbe backend-neutrale decimierte Geometrie wie DXGI übernehmen und nativ zeichnen. Diese
Variante ist erst nach dem Split-Layer-Milestone zu entscheiden; sie erhöht Shader-, Pipeline- und
Kompatibilitätsumfang deutlich.

### 11.4 Exit-Kriterien Phase 5

- Kein CPU-Staging-Slot wird vor Abschluss aller referenzierenden GPU-Submits überschrieben.
- Text bleibt statisch, während der Chart bei jedem Vulkan-Present scrollt.
- 30-Hz-Chart-Produktion erzeugt bei gefülltem Replay-Puffer keine 33-ms-Holds.
- Swapchain-Recreation, mehrere Queue-Familien, Resize, x64 und x86 laufen ohne stale Descriptor,
  Slot-Leak oder Fence-Wait auf Game-Queues.
- Die Vulkan-Layerregistrierung und Bitness-Verpackung aus `AGENTS.md` bleiben unverändert korrekt.

## 12. Phase 6 – Windowed-DWM, Dokumentation und Rollout

### 12.1 DWM separat behandeln

Der Windowed-DWM-Pfad wird nicht automatisch auf kontinuierliche DComp-Animation umgestellt.
Stattdessen:

- widersprüchliche Kommentare in `ScrollChart.h`, `OsdInstance.cpp` und `CLAUDE.md` vereinheitlichen,
- den Marquee-Pfad weiterhin explizit als experimentell/opt-in kennzeichnen,
- eine spätere Auto-Policy nur bei nachgewiesener unabhängiger MPO-Plane erwägen,
- dieselbe visuelle Testsequenz verwenden, Ergebnisse aber getrennt von DXGI/Vulkan auswerten.

### 12.2 Rollout

Empfohlene Reihenfolge kleiner, reversibler PRs:

1. Instrumentierung und Motion-/Cadence-Tests ohne Verhaltensänderung.
2. Published-Ring-Ownership und transaktionaler Chart-Cache.
3. DXGI-Per-Present-Scroll hinter `smoothChartScroll`.
4. Deadline-Scheduler und Debug-`chartHz`.
5. Decimation.
6. Joins und Anti-Aliasing.
7. Vulkan-Staging-Fix und anschließend Layer-Trennung.
8. Defaults aktivieren, Kill-Switch dokumentieren, Prebuilts aktualisieren.

Nach einem stabilen Release kann der interne Smooth-Scroll-Kill-Switch entfernt werden. Diagnose-
Zähler, Slot-Sicherungen und Cache-Fallback bleiben dauerhaft.

## 13. Abnahme- und Benchmarkmatrix

### 13.1 Backends und Zustände

| Dimension | Varianten |
|---|---|
| API/Pfad | DX11 External-v2, D3D12/D3D11On12, Vulkan x64, Vulkan x86 |
| Game-Present-Rate | 30, 60, 120, 144, 165, 240 Hz; zusätzlich VRR-Sequenz |
| Chart-Producer | 30, 60, 90 Hz |
| Chart-Zeitfenster | 2 s, 5 s, 10 s |
| Zoom | 1,0; 1,5; 2,0 |
| Datenzustand | kontinuierlich, PresentMon-Bursts, Rebuffering, Timeline-Reset |
| Lebenszyklus | Aktivierung, Alt-Tab, Resize, Swapchain-Recreation, Device-Loss, Hide/Show |

Mindestens ein sehr schneller Titel und ein GPU-limitierter Titel werden verwendet. Pro
Performance-Variante erfolgen mindestens drei gleich lange Captures derselben Szene. Startup,
Shader-Kompilierung und Alt-Tab werden separat ausgewiesen und nicht stillschweigend in den
Steady-State-Median gemischt.

### 13.2 Harte Abnahmekriterien

- **Bewegung:** Bei vorhandenem Guard kein Hold länger als ein Present und kein Rücksprung.
- **Datenintegrität:** Keine erfundenen Werte; bei Starvation klarer, diagnostizierter Hold.
- **Stabilität:** Keine leere Chart-Generation durch transienten Uploadfehler bei gleichem Layout.
- **Threading:** Keine Überschreibung eines geleasten oder GPU-in-flight befindlichen Slots.
- **Present-Pfad:** keine Heap-Allokation, kein Mutex, kein blockierender Wait.
- **Performance:** 1%- und 0,1%-Lows nicht mehr als 1 % schlechter als Baseline, sofern die
  Run-to-run-Streuung kleiner ist; andernfalls muss ein Konfidenzintervall beziehungsweise eine
  größere Stichprobe die Nicht-Unterlegenheit zeigen.
- **Komplexität:** Decimation begrenzt die Punktzahl proportional zur Plotbreite.
- **Kompatibilität:** Feature-Kill-Switch stellt den vorherigen Cached-Draw-Pfad wieder her.

### 13.3 Relevante Build-/Testkommandos im nativen OSD-Projektordner

```powershell
# Arbeitsverzeichnis: <OSD-Repository>\CapFrameX.OSD
cmake --preset vs2022
cmake --build build --config RelWithDebInfo
ctest --test-dir build -C RelWithDebInfo --output-on-failure
```

Zusätzlich müssen Hook und Vulkan-Layer jeweils für x64 und x86 gebaut werden. Vor einem Update
der Prebuilt-Binaries ist der vollständige Integrationsbuild von CapFrameX auszuführen.

## 14. Voraussichtlich betroffene Dateien

### Core/DXGI

- `src/core/OsdInstance.h/.cpp` – Producer-Takt, ChartFrame-Metadaten, visueller Cursor, Publikation.
- `src/core/ChartGeometry.h` – Range-Metadaten, Decimation, Joins und testbare Cache-Zustände.
- `src/core/ChartLayer.h/.cpp` – transaktionaler Upload, X-Transform, Clipping und AA-Shader.
- `src/core/Widget.h/.cpp` – Guard-Sample-Auswahl, Plot-Rechtecke und pixelbasierte Decimation.
- `src/core/ReplayBuffer.h` – explizites Advance-Budget beziehungsweise testbare Clock-Anker.
- `src/core/GraphicsDevice.h/.cpp` – Text-Slot-Ownership, sofern nicht in gemeinsamen Ring verlegt.
- `apps/chart_geometry_test/main.cpp` – Geometrie-, Cache- und Decimation-Regressionen.
- `apps/chart_motion_test/main.cpp` – neue zeitliche Bewegungsregression.
- `apps/producer_cadence_test/main.cpp` – neue Scheduler-Regression.
- `CMakeLists.txt` – neue Tests registrieren.

### Vulkan

- `vk_layer/src/osd_feed.h/.cpp` – versionierte Layer-Frames und Writer-Leases.
- `vk_layer/src/vk_compositor.cpp` – Staging-Slot-Fences, getrennte Text-/Chart-Uploads und Draws.
- `vk_layer/shaders/composite.vert/.frag` – Chart-UV-Offset und Plot-Clipping.
- `include/cfx/osd/osd_c_api.h` – versionierte CPU-Layer-Callback-Struktur.

### Dokumentation/Integration

- `src/core/ScrollChart.h`, `CLAUDE.md`, `README.md` – belastbare Aussagen zu DWM und Rate-Policy.
- CapFrameX-Submodule-Pin und `external/CapFrameX.OSD-prebuilt` – erst nach gemeinsamer x64/x86-
  Abnahme aktualisieren.

## 15. Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Visuelle Extrapolation zeigt noch nicht freigegebene Daten | `maxAdvanceMs` strikt aus gepuffertem Look-ahead ableiten; bei Rebuffering halten |
| Neue Generation springt relativ zum alten Frame | gemeinsamen QPC-/Replay-Anker verwenden, visuellen Cursor monoton halten, Übergang testen |
| Scissor-/Shader-State beschädigt Game-State | vorhandenen State-Backup explizit prüfen und im External-Host mit Sentinel-State testen |
| Slot-Lease fügt Waits in den Hotpath ein | ausschließlich atomare Try-Acquire-Operationen; bei Sättigung alte Generation behalten |
| AA erhöht GPU-Kosten | analytisches Single-Sample-AA, A/B-Schalter und feste Performance-Grenze |
| Vulkan-ABI und Prebuilts geraten auseinander | versionierte Struktur, alter Fallback, atomarer x64/x86-Release |
| 30 Hz spart nach Caching praktisch nichts | Default erst nach Messmatrix ändern; 60/90 Hz beibehalten, wenn Gewinn nicht belastbar ist |
| DWM-Ergebnisse werden auf In-Game-Pfade übertragen | Backend-Ergebnisse getrennt dokumentieren und separat freigeben |

## 16. Offene Entscheidungen mit Empfehlung

1. **DXGI: Vertex-Zeitstempel oder globaler X-Transform?**
   Empfehlung: zunächst X-Transform pro Chart-Range. Er verändert die bestehende Tessellierung
   minimal und kostet pro Present nur Constant-Buffer-/Scissor-State. Zeitstempel pro Vertex sind
   erst bei nichtlinearen Animationen nötig.

2. **Vulkan: native Liniengeometrie oder getrennte Raster-Layer?**
   Empfehlung: zuerst getrennte Raster-Layer für schnelle Verhaltensparität. Native Geometrie nur,
   wenn Uploadbandbreite oder Rastergröße anschließend messbar problematisch bleiben.

3. **30, 60 oder 90 Hz als neuer Chart-Default?**
   Empfehlung: offenlassen, bis Phase 2 und 3 gemessen sind. Smooth Motion allein garantiert nicht,
   dass 30 Hz bei neuen Spikes und dynamischer Y-Skalierung subjektiv gleichwertig ist.

4. **Muss `chartHz` in die UI?**
   Empfehlung: nein, zunächst Backend-Default plus Debug-Override. Eine Nutzeroption ist nur dann
   sinnvoll, wenn ein reproduzierbarer Performance-/Qualitäts-Trade-off bestehen bleibt.

## 17. Definition of Done für das Gesamtvorhaben

Das Vorhaben ist abgeschlossen, wenn:

- DXGI und Vulkan bei 30-Hz-Chart-Produktion ohne Sample-and-Hold-Scrollen pro Present bewegen,
- Daten-Starvation als einziger regulärer Grund für einen Chart-Hold übrig bleibt,
- Generationen, Slot-Lifetimes und GPU-Staging durch Tests und Laufzeitdiagnostik abgesichert sind,
- dichte Frametime-Serien eine begrenzte Vertex-Anzahl und stabile, antialiaste Linien ergeben,
- die Abnahmematrix keine relevante Regression der Average-, 1%- oder 0,1%-FPS zeigt,
- x64-/x86-Builds, Prebuilt-Paket und Submodule-Pin denselben verifizierten Stand enthalten,
- DWM-Marquee und In-Game-Smooth-Scroll in Code und Dokumentation klar getrennt sind.
