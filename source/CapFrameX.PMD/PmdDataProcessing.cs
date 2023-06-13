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
                    if (mappingSamples[k].Time >= referenceSamples[0].Time
                        || mappingSamples[k + 1].Time > referenceSamples[0].Time)
                    {
                        startLocalIndex = k;
                        break;
                    }
                }

                mappedSamples[0] = new PmdSample()
                {
                    Time = referenceSamples[0].Time,
                    Value = mappingSamples[startLocalIndex].Value
                };

                // Global loop reference samples
                for (int i = 0; i < referenceSamples.Length - 1; i++)
                {
                    var startTime = referenceSamples[i].Time;
                    var endTime = referenceSamples[i + 1].Time;
                    double aggregate = 0;
                    int mapCount = 0;

                    // Local loop mapping samples
                    for (int k = startLocalIndex; k < mappingSamples.Length - 1; k++)
                    {
                        if (startTime > mappingSamples[k].Time &&
                            endTime <= mappingSamples[k + 1].Time)
                        {
                            mappedSamples[i + 1] = new PmdSample() 
                            {
                                Time = endTime, 
                                Value = (mappingSamples[k].Value + mappingSamples[k + 1].Value)/2
                            };

                            break;
                        }
                        else if (mappingSamples[k].Time >= startTime && mappingSamples[k].Time < endTime)
                        {
                            aggregate += mappingSamples[k].Value;
                            mapCount++;
                        }
                        else if (mappingSamples[k].Time >= endTime)
                        {
							// Error: aggregate/0
							mappedSamples[i + 1] = new PmdSample() 
                            { 
                                Time = endTime, 
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
