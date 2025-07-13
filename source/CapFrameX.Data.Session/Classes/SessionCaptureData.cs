using CapFrameX.Data.Session.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Data.Session.Classes
{
    public class SessionCaptureData : ISessionCaptureData
    {
        public double[] TimeInSeconds { get; set; }
        public double[] MsBetweenPresents { get; set; }
        public double[] MsInPresentAPI { get; set; }
        public double[] MsBetweenDisplayChange { get; set; }
        public double[] MsUntilRenderComplete { get; set; }
        public double[] MsUntilDisplayed { get; set; }
        public double[] QPCTime { get; set; }
        public int[] PresentMode { get; set; }
        public int[] AllowsTearing { get; set; }
        public int[] SyncInterval { get; set; }
        public bool[] Dropped { get; set; }
        public double[] PcLatency { get; set; }
        public double[] GpuActive { get; set; }
        public double[] CpuActive { get; set; }

        public SessionCaptureData(int numberOfCapturePoints)
        {
            TimeInSeconds = new double[numberOfCapturePoints];
            MsBetweenPresents = new double[numberOfCapturePoints];
            MsInPresentAPI = new double[numberOfCapturePoints];
            MsBetweenDisplayChange = new double[numberOfCapturePoints];
            MsUntilRenderComplete = new double[numberOfCapturePoints];
            MsUntilDisplayed = new double[numberOfCapturePoints];
            QPCTime = new double[numberOfCapturePoints];
            PresentMode = new int[numberOfCapturePoints];
            AllowsTearing = new int[numberOfCapturePoints];
            SyncInterval = new int[numberOfCapturePoints];
            Dropped = new bool[numberOfCapturePoints];
            PcLatency = new double[numberOfCapturePoints];
            GpuActive = new double[numberOfCapturePoints];
            CpuActive = new double[numberOfCapturePoints];
        }

        public IEnumerable<CaptureDataEntry> LineWise()
        {
            for (int i = 0; i < MsBetweenPresents.Count(); i++)
            {
                yield return new CaptureDataEntry()
                {
                    TimeInSeconds = TimeInSeconds[i],
                    MsBetweenPresents = MsBetweenPresents[i],
                    MsInPresentAPI = MsInPresentAPI[i],
                    MsBetweenDisplayChange = MsBetweenDisplayChange[i],
                    MsUntilRenderComplete = MsUntilRenderComplete[i],
                    MsUntilDisplayed = MsUntilDisplayed[i],
                    QPCTime = QPCTime[i],
                    PresentMode = PresentMode[i],
                    AllowsTearing = AllowsTearing[i],
                    SyncInterval = SyncInterval[i],
                    Dropped = Dropped[i],
                    PcLatency = PcLatency != null ? PcLatency[i] : double.NaN,
                    GpuActive = GpuActive[i],
                    CpuActive = CpuActive[i]
                };
            }
        }
    }

    public struct CaptureDataEntry
    {
        public double TimeInSeconds { get; set; }
        public double MsBetweenPresents { get; set; }
        public double MsInPresentAPI { get; set; }
        public double MsBetweenDisplayChange { get; set; }
        public double MsUntilRenderComplete { get; set; }
        public double MsUntilDisplayed { get; set; }
        public double QPCTime { get; set; }
        public int PresentMode { get; set; }
        public int AllowsTearing { get; set; }
        public int SyncInterval { get; set; }
        public bool Dropped { get; set; }
        public double PcLatency { get; set; }
        public double GpuActive { get; set; }
        public double CpuActive { get; set; }
    }
}