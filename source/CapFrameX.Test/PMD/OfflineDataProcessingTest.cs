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
            var referenceSamples = new PmdSample<float>[]
            {
                new PmdSample<float>(){PerformanceCounter = 0, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 10, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 20, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 30, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 40, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 50, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 60, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 70, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 80, Value = 100},
                new PmdSample<float>(){PerformanceCounter = 90, Value = 100}
            };

            var mappingSamples = new PmdSample<float>[100];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample<float>() { PerformanceCounter = i, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData<float>(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].PerformanceCounter, mappedSamples[i].PerformanceCounter);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }
    }
}
