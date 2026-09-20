namespace CapFrameX.Service.Records;

/// <summary>
/// Reduces a frame time series to a handful of points for the record list's sparkline.
/// </summary>
/// <remarks>
/// Min/max decimation, not sampling or averaging. The sparkline's job in the list is to show at a
/// glance whether a capture had a stutter, and a single 200 ms frame among thousands of 16 ms ones
/// disappears under an average and is missed entirely by every-nth sampling. Keeping the extreme of
/// each bucket is what makes the line worth drawing.
/// </remarks>
public static class FrametimeSparkline
{
    /// <summary>Points a sparkline carries unless a caller asks for another size.</summary>
    public const int DefaultPointCount = 64;

    /// <summary>
    /// Decimates a series to at most <paramref name="maxPoints"/> values, in time order.
    /// </summary>
    /// <param name="values">Frame times in milliseconds.</param>
    /// <param name="maxPoints">Upper bound on the result; at least two.</param>
    /// <exception cref="ArgumentOutOfRangeException">Fewer than two points were asked for.</exception>
    public static double[] Decimate(IReadOnlyList<double> values, int maxPoints = DefaultPointCount)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 2);

        if (values.Count <= maxPoints)
        {
            return [.. values];
        }

        // Each bucket contributes its lowest and its highest value, so the budget buys half as many
        // buckets as points.
        var buckets = maxPoints / 2;
        var result = new List<double>(maxPoints);

        for (var bucket = 0; bucket < buckets; bucket++)
        {
            // Computed from the bucket index rather than accumulated, so the last bucket ends
            // exactly at the end of the series however the division falls.
            var start = (int)((long)bucket * values.Count / buckets);
            var end = (int)((long)(bucket + 1) * values.Count / buckets);

            if (start >= end)
            {
                continue;
            }

            var min = values[start];
            var max = values[start];
            var minIndex = start;
            var maxIndex = start;

            for (var i = start + 1; i < end; i++)
            {
                if (values[i] < min)
                {
                    min = values[i];
                    minIndex = i;
                }
                else if (values[i] > max)
                {
                    max = values[i];
                    maxIndex = i;
                }
            }

            // Emit the two in the order they occurred, so a rising series comes out rising.
            if (minIndex <= maxIndex)
            {
                result.Add(min);
                result.Add(max);
            }
            else
            {
                result.Add(max);
                result.Add(min);
            }
        }

        return [.. result];
    }
}
