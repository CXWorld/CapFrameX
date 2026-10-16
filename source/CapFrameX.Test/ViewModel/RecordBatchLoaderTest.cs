using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Data;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class RecordBatchLoaderTest
    {
        [TestMethod]
        public async Task Results_KeepTheOrderOfTheRecords()
        {
            var records = Records(12);

            // Later records finish first.
            var results = await RecordBatchLoader.LoadAsync(records, record =>
            {
                Thread.Sleep(5 * (12 - Index(record)));
                return record.FullPath;
            }, null);

            CollectionAssert.AreEqual(records.Select(record => record.FullPath).ToArray(), results.ToArray());
        }

        [TestMethod]
        public async Task RecordsWithoutResultOrWithAnError_AreLeftOutAndLogged()
        {
            var records = Records(5);
            var logger = new Mock<ILogger>();

            var results = await RecordBatchLoader.LoadAsync(records, record => Index(record) switch
            {
                1 => null,
                3 => throw new InvalidOperationException("broken"),
                _ => record.FullPath
            }, logger.Object);

            CollectionAssert.AreEqual(new[] { "record-0", "record-2", "record-4" }, results.ToArray());
            logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [TestMethod]
        public async Task Records_AreLoadedOffTheCallingThread()
        {
            int caller = Thread.CurrentThread.ManagedThreadId;
            var threads = new List<int>();

            await RecordBatchLoader.LoadAsync(Records(4), record =>
            {
                lock (threads) threads.Add(Thread.CurrentThread.ManagedThreadId);
                return record.FullPath;
            }, null);

            Assert.AreEqual(4, threads.Count);
            Assert.IsTrue(threads.All(thread => thread != caller));
        }

        [TestMethod]
        public async Task OnlyAFewRecordsAreLoadedAtOnce()
        {
            int running = 0, peak = 0;

            await RecordBatchLoader.LoadAsync(Records(20), record =>
            {
                int now = Interlocked.Increment(ref running);
                InterlockedMax(ref peak, now);
                Thread.Sleep(20);
                Interlocked.Decrement(ref running);
                return record.FullPath;
            }, null);

            Assert.IsTrue(peak <= RecordBatchLoader.MaxParallelLoads, $"{peak} loads ran at once.");
            Assert.IsTrue(peak > 1 || Environment.ProcessorCount == 1, "The loads should overlap.");
        }

        [TestMethod]
        public async Task NoRecords_GiveNoResults()
        {
            Assert.AreEqual(0, (await RecordBatchLoader.LoadAsync<string>(null, record => "x", null)).Count);
            Assert.AreEqual(0, (await RecordBatchLoader.LoadAsync(Array.Empty<IFileRecordInfo>(), record => "x", null)).Count);
        }

        private static IFileRecordInfo[] Records(int count)
            => Enumerable.Range(0, count).Select(index =>
            {
                var record = new Mock<IFileRecordInfo>();
                record.SetupGet(info => info.FullPath).Returns("record-" + index);
                return record.Object;
            }).ToArray();

        private static int Index(IFileRecordInfo record) => int.Parse(record.FullPath.Substring("record-".Length));

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while ((current = Volatile.Read(ref target)) < value
                && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }
}
