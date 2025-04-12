using CapFrameX.PMD.Powenetics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PMD
{
    [TestClass]
    public class OfflineDataProcessingTest
    {
        [TestMethod]
        public void Mapping_Frametimes_Aggregation()
        {
            var referenceSamples = new PoweneticsSample[]
            {
                new PoweneticsSample(){Time = 0, Value = 100},
                new PoweneticsSample(){Time = 10, Value = 100},
                new PoweneticsSample(){Time = 20, Value = 100},
                new PoweneticsSample(){Time = 30, Value = 100},
                new PoweneticsSample(){Time = 40, Value = 100},
                new PoweneticsSample(){Time = 50, Value = 100},
                new PoweneticsSample(){Time = 60, Value = 100},
                new PoweneticsSample(){Time = 70, Value = 100},
                new PoweneticsSample(){Time = 80, Value = 100},
                new PoweneticsSample(){Time = 90, Value = 100}
            };

            var mappingSamples = new PoweneticsSample[100];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PoweneticsSample() { Time = i, Value = 50 };
            }

            var mappedSamples = PoweneticsDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

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
            var referenceSamples = new PoweneticsSample[]
            {
                new PoweneticsSample(){Time = 10, Value = 100},
                new PoweneticsSample(){Time = 20, Value = 100},
                new PoweneticsSample(){Time = 30, Value = 100},
                new PoweneticsSample(){Time = 40, Value = 100},
                new PoweneticsSample(){Time = 50, Value = 100},
                new PoweneticsSample(){Time = 60, Value = 100},
                new PoweneticsSample(){Time = 70, Value = 100},
                new PoweneticsSample(){Time = 80, Value = 100},
                new PoweneticsSample(){Time = 90, Value = 100},
                new PoweneticsSample(){Time = 100, Value = 100}
            };

            var mappingSamples = new PoweneticsSample[110];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PoweneticsSample() { Time = i, Value = 50 };
            }

            var mappedSamples = PoweneticsDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

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
            var referenceSamples = new PoweneticsSample[]
            {
                new PoweneticsSample(){Time = 1, Value = 100},
                new PoweneticsSample(){Time = 2, Value = 100},
                new PoweneticsSample(){Time = 3, Value = 100},
                new PoweneticsSample(){Time = 4, Value = 100},
                new PoweneticsSample(){Time = 100, Value = 100},
                new PoweneticsSample(){Time = 200, Value = 100},
                new PoweneticsSample(){Time = 300, Value = 100},
                new PoweneticsSample(){Time = 400, Value = 100},
                new PoweneticsSample(){Time = 500, Value = 100},
                new PoweneticsSample(){Time = 600, Value = 100},
                new PoweneticsSample(){Time = 700, Value = 100},
                new PoweneticsSample(){Time = 800, Value = 100},
                new PoweneticsSample(){Time = 900, Value = 100},
                new PoweneticsSample(){Time = 1000, Value = 100}
            };

            var mappingSamples = new PoweneticsSample[110];

            for (int i = 0; i < mappingSamples.Length; i++)
            {
                mappingSamples[i] = new PoweneticsSample() { Time = i * 10, Value = 50 };
            }

            var mappedSamples = PoweneticsDataProcessing.GetMappedPmdData(referenceSamples, mappingSamples);

            Assert.AreEqual(mappedSamples.Length, referenceSamples.Length);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                Assert.AreEqual(referenceSamples[i].Time, mappedSamples[i].Time);
                Assert.AreEqual(50, mappedSamples[i].Value);
            }
        }
    }
}
