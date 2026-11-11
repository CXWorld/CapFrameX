using CapFrameX.Contracts.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.ViewModel
{
    /// <summary>
    /// Loads dropped records off the UI thread. A dropped folder can hold dozens of records, and
    /// parsing them one after the other on the UI thread froze the view for seconds.
    /// </summary>
    internal static class RecordBatchLoader
    {
        // Each load holds a whole session; a few at a time keep the cores busy without holding
        // dozens of large captures at once.
        internal const int MaxParallelLoads = 4;

        /// <summary>
        /// The results in the order of <paramref name="records"/>, without the records whose load
        /// returned null or failed.
        /// </summary>
        internal static async Task<List<T>> LoadAsync<T>(IReadOnlyList<IFileRecordInfo> records,
            Func<IFileRecordInfo, T> load, ILogger logger) where T : class
        {
            if (records == null || records.Count == 0)
                return new List<T>();

            using var slots = new SemaphoreSlim(Math.Min(Environment.ProcessorCount, MaxParallelLoads));
            var loads = records.Select(async record =>
            {
                await slots.WaitAsync().ConfigureAwait(false);
                try
                {
                    return await Task.Run(() => load(record)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error while loading the dropped record {path}", record?.FullPath);
                    return null;
                }
                finally
                {
                    slots.Release();
                }
            }).ToList();

            T[] results = await Task.WhenAll(loads).ConfigureAwait(false);
            return results.Where(result => result != null).ToList();
        }
    }
}
