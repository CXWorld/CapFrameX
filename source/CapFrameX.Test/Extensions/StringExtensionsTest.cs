using CapFrameX.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test
{
    [TestClass]
    public class StringExtensionsTest
    {
        [TestMethod]
        public void SubString_CorrectValue()
        {
            const string fileName = "OCAT-ACOdyssey.exe-2018-11-16T220300.csv";

            string gameName = fileName.Substring("OCAT-", ".exe");
            string creationDate = fileName.Substring("exe-", "T");
            string recordTime = fileName.Substring(creationDate + "T", ".csv");

            Assert.AreEqual("ACOdyssey", gameName);
            Assert.AreEqual("2018-11-16", creationDate);
            Assert.AreEqual("220300", recordTime);
        }

        [TestMethod]
        public void GetSha1_MatchesKnownValue()
        {
            Assert.AreEqual(
                "A9993E364706816ABA3E25717850C26C9CD0D89D",
                "abc".GetSha1());
        }
    }
}
