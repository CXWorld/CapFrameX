using CapFrameX.Contracts.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class OsdAnchorPositionCycleTest
    {
        [DataTestMethod]
        [DataRow(0, 1)]
        [DataRow(1, 2)]
        [DataRow(2, 3)]
        [DataRow(3, 4)]
        [DataRow(4, 0)]
        [DataRow(-1, 0)]
        [DataRow(5, 0)]
        public void GetNext_CyclesAllPositionsAndRecoversFromInvalidValues(
            int currentPosition, int expectedPosition)
        {
            Assert.AreEqual(expectedPosition, OsdAnchorPositionCycle.GetNext(currentPosition));
        }
    }
}
