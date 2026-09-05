using CapFrameX.Contracts.Configuration;
using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class PresentMonServiceConfigurationTest
    {
        [TestMethod]
        public void ConfigParameterToArguments_DefaultsToTheShippedCircularBufferSize()
        {
            var configuration = new PresentMonServiceConfiguration { RedirectOutputStream = true };

            var arguments = configuration.ConfigParameterToArguments();

            Assert.AreEqual(4096, PresentMonCircularBuffer.DefaultSize);
            StringAssert.Contains(arguments, "--set_circular_buffer_size 4096");
        }

        [TestMethod]
        public void ConfigParameterToArguments_UsesTheConfiguredCircularBufferSize()
        {
            foreach (var size in PresentMonCircularBuffer.Sizes)
            {
                var redirected = new PresentMonServiceConfiguration
                {
                    RedirectOutputStream = true,
                    CircularBufferSize = size
                };

                var toFile = new PresentMonServiceConfiguration
                {
                    ProcessName = "vkcube.exe",
                    OutputFilename = "capture.csv",
                    CircularBufferSize = size
                };

                StringAssert.Contains(redirected.ConfigParameterToArguments(),
                    $"--set_circular_buffer_size {size}");
                StringAssert.Contains(toFile.ConfigParameterToArguments(),
                    $"--set_circular_buffer_size {size}");
            }
        }

        [TestMethod]
        public void ConfigParameterToArguments_FallsBackWhenTheSizeIsNoPowerOfTwo()
        {
            // PresentMon refuses to start on a rejected argument, which would take down the whole
            // capture service - so a hand-edited configuration must not reach the command line.
            var configuration = new PresentMonServiceConfiguration
            {
                RedirectOutputStream = true,
                CircularBufferSize = 3000
            };

            StringAssert.Contains(configuration.ConfigParameterToArguments(),
                "--set_circular_buffer_size 4096");
        }

        [TestMethod]
        public void CircularBufferSizes_AreThePowersOfTwoPresentMonAccepts()
        {
            CollectionAssert.AreEqual(new List<int> { 2048, 4096, 8192 },
                PresentMonCircularBuffer.Sizes.ToList());

            foreach (var size in PresentMonCircularBuffer.Sizes)
            {
                Assert.AreEqual(0, size & (size - 1), $"{size} is no power of two.");
                Assert.AreEqual(size, PresentMonCircularBuffer.Normalize(size));
            }

            Assert.AreEqual(PresentMonCircularBuffer.DefaultSize, PresentMonCircularBuffer.Normalize(0));
            Assert.AreEqual(PresentMonCircularBuffer.DefaultSize, PresentMonCircularBuffer.Normalize(1024));
            Assert.AreEqual(PresentMonCircularBuffer.DefaultSize, PresentMonCircularBuffer.Normalize(16384));
        }
    }
}
