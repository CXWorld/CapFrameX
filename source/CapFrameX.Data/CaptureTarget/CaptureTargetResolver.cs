using CapFrameX.Capture.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapFrameX.Data.CaptureTarget
{
    /// <summary>
    /// One listed process as the automatic target selection sees it.
    /// </summary>
    public sealed class CaptureTargetCandidate
    {
        public CaptureTargetCandidate(string name, int processId)
        {
            Name = name;
            ProcessId = processId;
        }

        public string Name { get; }

        public int ProcessId { get; }

        /// <summary>
        /// Present statistics of the capture service; null when it measured nothing.
        /// </summary>
        public CaptureProcessActivity Activity { get; set; }

        /// <summary>
        /// Seconds since the process owned the foreground window; null when it never did while
        /// it was listed.
        /// </summary>
        public double? SecondsSinceForeground { get; set; }

        /// <summary>
        /// The process owns the foreground window at a capture request.
        /// </summary>
        public bool IsForegroundNow { get; set; }

        /// <summary>
        /// The process list names it as a game: an entry with a game name that is not ignored.
        /// </summary>
        public bool IsKnownGame { get; set; }

        /// <summary>
        /// Its executable lies in a game library (<see cref="CaptureTargetResolver.IsGameLibraryPath"/>).
        /// </summary>
        public bool IsInGameLibrary { get; set; }
    }

    public enum ECaptureTargetReason
    {
        NoCandidate,
        OnlyCandidate,
        ForegroundAtCapture,
        GpuDominant,
        PreviousTarget,
        LastForeground,
        OnlyActive,
        KnownGame,
        PresentsLikeAGame,
        SameExecutable,
        Ambiguous
    }

    public sealed class CaptureTargetResolution
    {
        public CaptureTargetResolution(CaptureTargetCandidate target, ECaptureTargetReason reason, string detail)
        {
            Target = target;
            Reason = reason;
            Detail = detail;
        }

        /// <summary>
        /// The selected process; null when there is no candidate or no rule decides.
        /// </summary>
        public CaptureTargetCandidate Target { get; }

        public ECaptureTargetReason Reason { get; }

        /// <summary>
        /// Why the rule decided, for the log.
        /// </summary>
        public string Detail { get; }
    }

    /// <summary>
    /// Selects the capture and overlay target among several listed processes. The rules apply in
    /// order and the first one that names exactly one process decides; none decides when
    /// nothing tells the processes apart, e.g. two games running at once. The user's selection
    /// is not part of it: it always wins.
    /// <list type="number">
    /// <item>A capture request takes the process in the foreground: the hotkey is pressed in the game.</item>
    /// <item>A process that keeps the GPU busy far longer than every other one.</item>
    /// <item>The previous target, as long as it is listed: a paused or minimized game keeps it.</item>
    /// <item>The process that owned the foreground window last.</item>
    /// <item>The only process that presented during the last seconds.</item>
    /// <item>The only known game: a game name in the process list, or a game library folder.</item>
    /// <item>The only process that presents like a game: independent flip, exclusive fullscreen, generated frames.</item>
    /// <item>Processes of one executable: the first, as before the rules existed.</item>
    /// </list>
    /// The thresholds are preliminary; the log line of every decision lists what each candidate
    /// measured, to calibrate them.
    /// </summary>
    public static class CaptureTargetResolver
    {
        /// <summary>
        /// A process that presented within this many seconds is active.
        /// </summary>
        public const int ActiveIdleSeconds = 2;

        /// <summary>
        /// Minimum GPU busy share of a dominant process.
        /// </summary>
        public const double MinimumDominantGpuShare = 0.10;

        /// <summary>
        /// A dominant process keeps the GPU busy at least this many times as long as any other.
        /// Going back takes the same ratio the other way, so the target does not flap between two
        /// processes of similar load.
        /// </summary>
        public const double GpuDominanceRatio = 3.0;

        // Libraries of the stores. A launcher that lives inside one is excluded explicitly.
        private static readonly string[] GameLibraryFolders =
        {
            @"\steamapps\common\",
            @"\Epic Games\",
            @"\GOG Galaxy\Games\",
            @"\GOG Games\",
            @"\XboxGames\",
            @"\Ubisoft Game Launcher\games\",
            @"\EA Games\",
            @"\Origin Games\",
            @"\Amazon Games\Library\",
        };

        private static readonly string[] LauncherFolders =
        {
            @"\Epic Games\Launcher\",
        };

        public static CaptureTargetResolution Resolve(IReadOnlyList<CaptureTargetCandidate> candidates,
            int previousTargetProcessId, bool captureRequest)
        {
            if (candidates == null || candidates.Count == 0)
                return new CaptureTargetResolution(null, ECaptureTargetReason.NoCandidate, "no process listed");

            if (candidates.Count == 1)
                return new CaptureTargetResolution(candidates[0], ECaptureTargetReason.OnlyCandidate, "only listed process");

            if (captureRequest)
            {
                var foreground = Single(candidates.Where(candidate => candidate.IsForegroundNow && !IsKnownIdle(candidate)));
                if (foreground != null)
                    return new CaptureTargetResolution(foreground, ECaptureTargetReason.ForegroundAtCapture,
                        "in the foreground at the capture request");
            }

            var dominant = FindGpuDominant(candidates, out string dominanceDetail);
            if (dominant != null)
                return new CaptureTargetResolution(dominant, ECaptureTargetReason.GpuDominant, dominanceDetail);

            if (previousTargetProcessId != 0)
            {
                var previous = candidates.FirstOrDefault(candidate => candidate.ProcessId == previousTargetProcessId);
                if (previous != null)
                    return new CaptureTargetResolution(previous, ECaptureTargetReason.PreviousTarget, "previous target");
            }

            var lastForeground = candidates
                .Where(candidate => candidate.SecondsSinceForeground.HasValue && !IsKnownIdle(candidate))
                .OrderBy(candidate => candidate.SecondsSinceForeground.Value)
                .FirstOrDefault();
            if (lastForeground != null)
                return new CaptureTargetResolution(lastForeground, ECaptureTargetReason.LastForeground,
                    $"in the foreground {FormatSeconds(lastForeground.SecondsSinceForeground.Value)} ago");

            // A process that stopped presenting is no target while another one presents. Without
            // statistics for every candidate the rule cannot tell.
            IReadOnlyList<CaptureTargetCandidate> pool = candidates;
            if (candidates.All(candidate => candidate.Activity != null))
            {
                var active = candidates.Where(IsActive).ToList();
                if (active.Count == 1)
                    return new CaptureTargetResolution(active[0], ECaptureTargetReason.OnlyActive,
                        $"only process that presented within {ActiveIdleSeconds} s");
                if (active.Count > 1)
                    pool = active;
            }

            var knownGame = Single(pool.Where(candidate => candidate.IsKnownGame || candidate.IsInGameLibrary));
            if (knownGame != null)
                return new CaptureTargetResolution(knownGame, ECaptureTargetReason.KnownGame,
                    knownGame.IsKnownGame ? "only known game" : "only process in a game library");

            var gameLike = Single(pool.Where(PresentsLikeAGame));
            if (gameLike != null)
                return new CaptureTargetResolution(gameLike, ECaptureTargetReason.PresentsLikeAGame,
                    $"only process that presents like a game ({DescribePresentation(gameLike.Activity)})");

            if (pool.Select(candidate => candidate.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
                return new CaptureTargetResolution(pool[0], ECaptureTargetReason.SameExecutable,
                    "all candidates run the same executable");

            return new CaptureTargetResolution(null, ECaptureTargetReason.Ambiguous, "no rule tells the processes apart");
        }

        /// <summary>
        /// True when the executable lies in a game library of Steam, Epic, GOG, Xbox, Ubisoft, EA
        /// or Amazon. The Microsoft Store folder (WindowsApps) is no such library: it also holds
        /// apps that present, like media players.
        /// </summary>
        public static bool IsGameLibraryPath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return false;

            string path = imagePath.Replace('/', '\\');
            return GameLibraryFolders.Any(folder => path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0)
                && !LauncherFolders.Any(folder => path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Independent flip and exclusive fullscreen ("Hardware: ...") present without the
        /// compositor, as games do. Browsers and tools are composed by DWM, except in fullscreen.
        /// </summary>
        public static bool IsHardwarePresentMode(string presentMode)
            => presentMode != null && presentMode.StartsWith("Hardware", StringComparison.Ordinal);

        /// <summary>
        /// What the rules saw of one candidate, for the log.
        /// </summary>
        public static string Describe(CaptureTargetCandidate candidate)
        {
            var parts = new List<string>();
            var activity = candidate.Activity;
            if (activity == null)
            {
                parts.Add("no statistics");
            }
            else
            {
                parts.Add(activity.PresentsPerSecond.ToString("0.#", CultureInfo.InvariantCulture) + " presents/s");
                parts.Add("GPU " + (activity.GpuBusyShare.HasValue ? FormatPercent(activity.GpuBusyShare.Value) : "n/a"));
                parts.Add("idle " + activity.IdleSeconds.ToString(CultureInfo.InvariantCulture) + " s");
                parts.Add(DescribePresentation(activity));
            }

            if (candidate.SecondsSinceForeground.HasValue)
                parts.Add("foreground " + FormatSeconds(candidate.SecondsSinceForeground.Value) + " ago");
            if (candidate.IsForegroundNow)
                parts.Add("foreground now");
            if (candidate.IsKnownGame)
                parts.Add("known game");
            if (candidate.IsInGameLibrary)
                parts.Add("game library");

            return $"'{candidate.Name}' (PID {candidate.ProcessId}): {string.Join(", ", parts)}";
        }

        private static CaptureTargetCandidate FindGpuDominant(IReadOnlyList<CaptureTargetCandidate> candidates, out string detail)
        {
            detail = null;
            // An unmeasured candidate may be the game that has just started.
            if (candidates.Any(candidate => candidate.Activity?.GpuBusyShare == null))
                return null;

            var ordered = candidates.OrderByDescending(candidate => candidate.Activity.GpuBusyShare.Value).ToList();
            double top = ordered[0].Activity.GpuBusyShare.Value;
            double second = ordered[1].Activity.GpuBusyShare.Value;
            if (top < MinimumDominantGpuShare || top < GpuDominanceRatio * second)
                return null;

            detail = $"GPU busy {FormatPercent(top)}, next '{ordered[1].Name}' {FormatPercent(second)}";
            return ordered[0];
        }

        private static bool PresentsLikeAGame(CaptureTargetCandidate candidate)
            => candidate.Activity != null &&
               (candidate.Activity.HasGeneratedFrames || IsHardwarePresentMode(candidate.Activity.PresentMode));

        private static bool IsActive(CaptureTargetCandidate candidate)
            => candidate.Activity != null && candidate.Activity.IdleSeconds <= ActiveIdleSeconds;

        private static bool IsKnownIdle(CaptureTargetCandidate candidate)
            => candidate.Activity != null && candidate.Activity.IdleSeconds > ActiveIdleSeconds;

        private static CaptureTargetCandidate Single(IEnumerable<CaptureTargetCandidate> candidates)
        {
            CaptureTargetCandidate single = null;
            foreach (var candidate in candidates)
            {
                if (single != null)
                    return null;
                single = candidate;
            }
            return single;
        }

        private static string DescribePresentation(CaptureProcessActivity activity)
        {
            string mode = string.IsNullOrEmpty(activity.PresentMode) ? "present mode unknown" : activity.PresentMode;
            return activity.HasGeneratedFrames ? mode + ", generated frames" : mode;
        }

        private static string FormatPercent(double share)
            => (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + " %";

        private static string FormatSeconds(double seconds)
            => seconds.ToString("0", CultureInfo.InvariantCulture) + " s";
    }
}
