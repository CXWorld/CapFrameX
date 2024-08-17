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
                new PmdSample(){Time = 0, Value = 100},
                new PmdSample(){Time = 10, Value = 100},
                new PmdSample(){Time = 20, Value = 100},
                new PmdSample(){Time = 30, Value = 100},
                new PmdSample(){Time = 40, Value = 100},
                new PmdSample(){Time = 50, Value = 100},
                new PmdSample(){Time = 60, Value = 100},
                new PmdSample(){Time = 70, Value = 100},
                new PmdSample(){Time = 80, Value = 100},
                new PmdSample(){Time = 90, Value = 100}
            };

            var mappingSamples = new PmdSample[100];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample() { Time = i, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].Time, mappedSamples[i].Time);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }

        [TestMethod]
        public void Mapping_Frametimes_Overlap()
        {
            var referenceSamples = new PmdSample[]
            {
                new PmdSample(){Time = 10, Value = 100},
                new PmdSample(){Time = 20, Value = 100},
                new PmdSample(){Time = 30, Value = 100},
                new PmdSample(){Time = 40, Value = 100},
                new PmdSample(){Time = 50, Value = 100},
                new PmdSample(){Time = 60, Value = 100},
                new PmdSample(){Time = 70, Value = 100},
                new PmdSample(){Time = 80, Value = 100},
                new PmdSample(){Time = 90, Value = 100},
                new PmdSample(){Time = 100, Value = 100}
            };

            var mappingSamples = new PmdSample[110];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample() { Time = i, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].Time, mappedSamples[i].Time);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }

        [TestMethod]
        public void Mapping_Frametimes_Oversampling()
        {
            var referenceSamples = new PmdSample[]
            {
                new PmdSample(){Time = 1, Value = 100},
                new PmdSample(){Time = 2, Value = 100},
                new PmdSample(){Time = 3, Value = 100},
                new PmdSample(){Time = 4, Value = 100},
                new PmdSample(){Time = 100, Value = 100},
                new PmdSample(){Time = 200, Value = 100},
                new PmdSample(){Time = 300, Value = 100},
                new PmdSample(){Time = 400, Value = 100},
                new PmdSample(){Time = 500, Value = 100},
                new PmdSample(){Time = 600, Value = 100},
                new PmdSample(){Time = 700, Value = 100},
                new PmdSample(){Time = 800, Value = 100},
                new PmdSample(){Time = 900, Value = 100},
                new PmdSample(){Time = 1000, Value = 100}
            };

            var mappingSamples = new PmdSample[110];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PmdSample() { Time = i * 10, Value = 50 };
            }

            var mappedSamples = PmdDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].Time, mappedSamples[i].Time);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }
    }
}
