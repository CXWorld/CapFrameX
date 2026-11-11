// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;

namespace LibreHardwareMonitor.Hardware;

/// <summary>
/// Limits how often a slow sensor source is read, independently of the poll interval; its sensors
/// keep their last value in between. The first call reads at once, so a sensor has a value right
/// after discovery. After that, reads follow a fixed grid of <see cref="Interval"/>, shifted by the
/// phase: poll jitter does not stretch the interval, and devices with different phases do not read
/// in the same poll. A gap longer than one interval (nobody polled) restarts the grid.
/// Not thread-safe: the sensor poll calls it from one thread.
/// </summary>
internal sealed class RefreshThrottle
{
    private readonly Func<long> _clockMilliseconds;
    private readonly long _phaseMilliseconds;
    private bool _started;
    private long _nextDueMilliseconds;

    /// <param name="interval">Minimum time between two reads.</param>
    /// <param name="phase">Delays the second read and thereby every later one.</param>
    /// <param name="clockMilliseconds">Monotonic clock in milliseconds; <see cref="Environment.TickCount64"/> by default.</param>
    public RefreshThrottle(TimeSpan interval, TimeSpan phase = default, Func<long> clockMilliseconds = null)
    {
        Interval = interval;
        _phaseMilliseconds = Math.Max(0L, (long)phase.TotalMilliseconds);
        _clockMilliseconds = clockMilliseconds ?? (() => Environment.TickCount64);
    }

    /// <summary>
    /// Gets or sets the minimum time between two reads. Zero or less reads on every call.
    /// </summary>
    public TimeSpan Interval { get; set; }

    /// <summary>
    /// Returns true when the source should be read now and schedules the next read. A failed read
    /// counts as well, so a failing device is retried once per interval instead of on every poll.
    /// </summary>
    public bool TryBegin()
    {
        long now = _clockMilliseconds();
        long interval = (long)Interval.TotalMilliseconds;

        if (!_started)
        {
            _started = true;
            _nextDueMilliseconds = now + interval + _phaseMilliseconds;
            return true;
        }

        if (interval <= 0)
            return true;

        if (now < _nextDueMilliseconds)
            return false;

        // On time: stay on the grid. After a gap: restart it from now.
        _nextDueMilliseconds = now - _nextDueMilliseconds < interval
            ? _nextDueMilliseconds + interval
            : now + interval;

        return true;
    }
}
