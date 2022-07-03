using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.PMD;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PMD
{
    [TestClass]
    public class OfflineDataProcessingTest
    {
        [TestMethod]
        public void Mapping_Frametimes_Aggregation()
        {
            var referenceSamples = new PmdSample[]
            {
                new PmdSample(){PerformanceCounter = 0, Value = 100},
                new PmdSample(){PerformanceCounter = 10, Value = 100},
                new PmdSample(){PerformanceCounter = 20, Value = 100},
                new PmdSample(){PerformanceCounter = 30, Value = 100},
                new PmdSample(){PerformanceCounter = 40, Value = 100},
                new PmdSample(){PerformanceCounter = 50, Value = 100},
                new PmdSample(){PerformanceCounter = 60, Value = 100},
                new PmdSample(){PerformanceCounter = 70, Value = 100},
                new PmdSample(){PerformanceCounter = 80, Value = 100},
                new PmdSample(){PerformanceCounter = 90, Value = 100}
            };

            var mappingSamples = new PmdSample[100];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample() { PerformanceCounter = i, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].PerformanceCounter, mappedSamples[i].PerformanceCounter);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }

        [TestMethod]
        public void Mapping_Frametimes_Overlap()
        {
            var referenceSamples = new PmdSample[]
            {
                new PmdSample(){PerformanceCounter = 10, Value = 100},
                new PmdSample(){PerformanceCounter = 20, Value = 100},
                new PmdSample(){PerformanceCounter = 30, Value = 100},
                new PmdSample(){PerformanceCounter = 40, Value = 100},
                new PmdSample(){PerformanceCounter = 50, Value = 100},
                new PmdSample(){PerformanceCounter = 60, Value = 100},
                new PmdSample(){PerformanceCounter = 70, Value = 100},
                new PmdSample(){PerformanceCounter = 80, Value = 100},
                new PmdSample(){PerformanceCounter = 90, Value = 100},
                new PmdSample(){PerformanceCounter = 100, Value = 100}
            };

            var mappingSamples = new PmdSample[110];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample() { PerformanceCounter = i, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].PerformanceCounter, mappedSamples[i].PerformanceCounter);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }

        [TestMethod]
        public void Mapping_Frametimes_Oversampling()
        {
            var referenceSamples = new PmdSample[]
            {
                new PmdSample(){PerformanceCounter = 1, Value = 100},
                new PmdSample(){PerformanceCounter = 2, Value = 100},
                new PmdSample(){PerformanceCounter = 3, Value = 100},
                new PmdSample(){PerformanceCounter = 4, Value = 100},
                new PmdSample(){PerformanceCounter = 100, Value = 100},
                new PmdSample(){PerformanceCounter = 200, Value = 100},
                new PmdSample(){PerformanceCounter = 300, Value = 100},
                new PmdSample(){PerformanceCounter = 400, Value = 100},
                new PmdSample(){PerformanceCounter = 500, Value = 100},
                new PmdSample(){PerformanceCounter = 600, Value = 100},
                new PmdSample(){PerformanceCounter = 700, Value = 100},
                new PmdSample(){PerformanceCounter = 800, Value = 100},
                new PmdSample(){PerformanceCounter = 900, Value = 100},
                new PmdSample(){PerformanceCounter = 1000, Value = 100}
            };

            var mappingSamples = new PmdSample[110];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample() { PerformanceCounter = i * 10, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].PerformanceCounter, mappedSamples[i].PerformanceCounter);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }
    }
}
