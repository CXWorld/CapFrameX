namespace CapFrameX.PMD
{
    public class PmdDataProcessing
    {
        public static PmdSample[] GetMappedPmdData(PmdSample[] referenceSamples, PmdSample[] mappingSamples)
        {
            PmdSample[] mappedSamples = new PmdSample[referenceSamples.Length];
            int startLocalIndex = 0;

            try
            {
                for (int k = 0; k < mappingSamples.Length - 1; k++)
                {
                    if (mappingSamples[k].PerformanceCounter >= referenceSamples[0].PerformanceCounter
                        || mappingSamples[k + 1].PerformanceCounter > referenceSamples[0].PerformanceCounter)
                    {
                        startLocalIndex = k;
                        break;
                    }
                }

                mappedSamples[0] = new PmdSample()
                {
                    PerformanceCounter = referenceSamples[0].PerformanceCounter,
                    Value = mappingSamples[startLocalIndex].Value
                };

                // Global loop reference samples
                for (int i = 0; i < referenceSamples.Length - 1; i++)
                {
                    var startPerformanceCounter = referenceSamples[i].PerformanceCounter;
                    var endPerformanceCounter = referenceSamples[i + 1].PerformanceCounter;
                    float aggregate = 0;
                    int mapCount = 0;

                    // Local loop mapping samples
                    for (int k = startLocalIndex; k < mappingSamples.Length - 1; k++)
                    {
                        if (startPerformanceCounter > mappingSamples[k].PerformanceCounter &&
                            endPerformanceCounter <= mappingSamples[k + 1].PerformanceCounter)
                        {
                            mappedSamples[i + 1] = new PmdSample() 
                            {
                                PerformanceCounter = endPerformanceCounter, 
                                Value = (mappingSamples[k].Value + mappingSamples[k + 1].Value)/2
                            };

                            break;
                        }
                        else if (mappingSamples[k].PerformanceCounter >= startPerformanceCounter && mappingSamples[k].PerformanceCounter < endPerformanceCounter)
                        {
                            aggregate += mappingSamples[k].Value;
                            mapCount++;
                        }
                        else if (mappingSamples[k].PerformanceCounter >= endPerformanceCounter)
                        {
                            mappedSamples[i + 1] = new PmdSample() 
                            { 
                                PerformanceCounter = endPerformanceCounter, 
                                Value = aggregate / mapCount
                            };
                            startLocalIndex = k;

                            break;
                        }
                    }
                }
            }
            catch { mappedSamples = new PmdSample[referenceSamples.Length]; }

            return mappedSamples;
        }
    }
}
