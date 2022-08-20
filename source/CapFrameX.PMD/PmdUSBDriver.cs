using RJCP.IO.Ports;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text;
using CapFrameX.Contracts.PMD;

namespace CapFrameX.PMD
{
    public class PmdUSBDriver : IPmdDriver
    {
        const char SEPERATOR = '-';

        private SerialPortStream _pmd;
        private StringBuilder _resultsStringBuilder = new StringBuilder();
        private long _sampleTimeStamp = 0;

        private readonly ILogger<PmdUSBDriver> _logger;
        private readonly ISubject<PmdChannel[]> _pmdChannelStream = new Subject<PmdChannel[]>();
        private readonly ISubject<EPmdDriverStatus> _pmdstatusStream = new Subject<EPmdDriverStatus>();

        public IObservable<PmdChannel[]> PmdChannelStream => _pmdChannelStream.AsObservable();

        public IObservable<EPmdDriverStatus> PmdstatusStream => _pmdstatusStream.AsObservable();

        public PmdUSBDriver(ILogger<PmdUSBDriver> logger)
        {
            _logger = logger;
            _pmdstatusStream.OnNext(EPmdDriverStatus.Ready);
        }

        public bool Connect(string comPort, bool calibrationMode)
        {
            _pmd = new SerialPortStream(comPort, 921600, 8, Parity.None, StopBits.One);
            _pmd.DataReceived += new EventHandler<SerialDataReceivedEventArgs>(SerialPortDataReceived);
            _pmd.ErrorReceived += new EventHandler<SerialErrorReceivedEventArgs>(SerialPortErrorReceived);

            try
            {
                _pmd.Open();
                if (!calibrationMode) Task.Run(async () => await ConfigurePMD());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while opening PMD!");
                _pmdstatusStream.OnNext(EPmdDriverStatus.Error);
                return false;
            }

            _pmdstatusStream.OnNext(EPmdDriverStatus.Connected);

            return true;
        }

        public bool Disconnect()
        {
            if (_pmd != null)
            {
                if (_pmd.IsOpen) _pmd.Close();
                _pmd.Dispose();
            }

            _resultsStringBuilder.Clear();
            _sampleTimeStamp = 0;
            _pmdstatusStream.OnNext(EPmdDriverStatus.Ready);

            return true;
        }

        private void SerialPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // ToDo: maybe more detailed info later
            _pmdstatusStream.OnNext(EPmdDriverStatus.Error);
        }

        private void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int ibtr = _pmd.BytesToRead;
                byte[] bget = new byte[ibtr];
                _pmd.Read(bget, 0, ibtr);

                //Read data
                //Every 207 Characters I have a new feed
                //CA-AC-F8-1B-00-00-00-00-00-13-BB-00-00-09-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-42-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-
                _resultsStringBuilder.Append(BitConverter.ToString(bget));

                // process the data every 100ms, or when resultCarbinet.Length >=20700
                if (_resultsStringBuilder.Length >= 20700)
                {
                    var resultString = _resultsStringBuilder.ToString();

                    //process data async
                    Task.Run(() =>
                    {
                        string resultStringStepA = resultString.Replace(SEPERATOR.ToString(), string.Empty);
                        string resultStringStepB = resultStringStepA.Replace("CAAC", SEPERATOR.ToString());
                        string[] resultSplit = resultStringStepB.Split(SEPERATOR);
                        ProcessData(resultSplit);
                    });

                    _resultsStringBuilder.Clear();
                }
            }
            catch { throw; }
        }

        private void ProcessData(string[] results)
        {
            try
            {
                //Process each element of results_split if it has 67 bytes
                //I don't take into account the first 2 bytes because they are the sequence number
                foreach (var result in results)
                {
                    if (result.Length == 134)
                    {
                        var pmdChannels = new PmdChannel[PmdChannelExtensions.PmdChannelIndexMapping.Length];

                        //Channel 1: 3.3V        - 24pin ATX
                        string voltATX33Va = result.Substring(4, 2);
                        string voltATX33Vb = result.Substring(6, 2);
                        float voltATX33Va1 = HexToInt(voltATX33Va);
                        float voltATX33Vb1 = HexToInt(voltATX33Vb);
                        float voltATX33V_Final = (voltATX33Va1 * 256 + voltATX33Vb1) / 1000f;

                        string currentATX33Va = result.Substring(8, 2);
                        string currentATX33Vb = result.Substring(10, 2);
                        string currentATX33Vc = result.Substring(12, 2);
                        float currentATX33Va1 = HexToInt(currentATX33Va);
                        float currentATX33Vb1 = HexToInt(currentATX33Vb);
                        float currentATX33Vc1 = HexToInt(currentATX33Vc);
                        float currentATX33V_Final = (currentATX33Va1 * 65536 + currentATX33Vb1 * 256 + currentATX33Vc1) / 1000f;

                        pmdChannels[36] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "ATX_33V_Voltage",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltATX33V_Final
                        };

                        pmdChannels[37] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "ATX_33V_Current",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentATX33V_Final
                        };

                        pmdChannels[38] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "ATX_33V_Power",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltATX33V_Final * currentATX33V_Final
                        };

                        //Channel 2: 5Vsb        - 24pin ATX
                        //Only if I have a 24 pin connector installed
                        if (voltATX33V_Final > 1)
                        {
                            string volt5VSBa = result.Substring(14, 2);
                            string volt5VSBb = result.Substring(16, 2);
                            float volt5VSBa1 = HexToInt(volt5VSBa);
                            float volt5VSBb1 = HexToInt(volt5VSBb);
                            float volt5VSB_Final = (volt5VSBa1 * 256 + volt5VSBb1) / 1000f;

                            string current5VSBa = result.Substring(18, 2);
                            string current5VSBb = result.Substring(20, 2);
                            string current5VSBc = result.Substring(22, 2);
                            float current5VSBa1 = HexToInt(current5VSBa);
                            float current5VSBb1 = HexToInt(current5VSBb);
                            float current5VSBc1 = HexToInt(current5VSBc);
                            float current5VSB_Final = (current5VSBa1 * 65536 + current5VSBb1 * 256 + current5VSBc1) / 1000f;

                            pmdChannels[39] = new PmdChannel()
                            {
                                Measurand = PmdMeasurand.Voltage,
                                Name = "ATX_STB_Voltage",
                                PmdChannelType = PmdChannelType.ATX,
                                TimeStamp = _sampleTimeStamp,
                                Value = volt5VSB_Final
                            };

                            pmdChannels[40] = new PmdChannel()
                            {
                                Measurand = PmdMeasurand.Current,
                                Name = "ATX_STB_Current",
                                PmdChannelType = PmdChannelType.ATX,
                                TimeStamp = _sampleTimeStamp,
                                Value = current5VSB_Final
                            };

                            pmdChannels[41] = new PmdChannel()
                            {
                                Measurand = PmdMeasurand.Power,
                                Name = "ATX_STB_Power",
                                PmdChannelType = PmdChannelType.ATX,
                                TimeStamp = _sampleTimeStamp,
                                Value = volt5VSB_Final * current5VSB_Final
                            };
                        }

                        //Channel 3: 12V         - 24pin ATX & 10pin ATX
                        string voltATX12Va = result.Substring(24, 2);
                        string voltATX12Vb = result.Substring(26, 2);
                        float voltATX12Va1 = HexToInt(voltATX12Va);
                        float voltATX12Vb1 = HexToInt(voltATX12Vb);
                        float voltATX12V_Final = (voltATX12Va1 * 256 + voltATX12Vb1) / 1000f;

                        string currentATX12Va = result.Substring(28, 2);
                        string currentATX12Vb = result.Substring(30, 2);
                        string currentATX12Vc = result.Substring(32, 2);
                        float currentATX12Va1 = HexToInt(currentATX12Va);
                        float currentATX12Vb1 = HexToInt(currentATX12Vb);
                        float currentATX12Vc1 = HexToInt(currentATX12Vc);
                        float currentATX12V_Final = (currentATX12Va1 * 65536 + currentATX12Vb1 * 256 + currentATX12Vc1) / 1000f;

                        pmdChannels[30] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "ATX_12V_Voltage",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltATX12V_Final
                        };

                        pmdChannels[31] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "ATX_12V_Current",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentATX12V_Final
                        };

                        pmdChannels[32] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "ATX_12V_Power",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltATX12V_Final * currentATX12V_Final
                        };

                        //Channel 4: 5V          - 24pin ATX
                        string voltATX5Va = result.Substring(34, 2);
                        string voltATX5Vb = result.Substring(36, 2);
                        float voltATX5Va1 = HexToInt(voltATX5Va);
                        float voltATX5Vb1 = HexToInt(voltATX5Vb);
                        float voltATX5V_Final = (voltATX5Va1 * 256 + voltATX5Vb1) / 1000f;

                        string currentATX5Va = result.Substring(38, 2);
                        string currentATX5Vb = result.Substring(40, 2);
                        string currentATX5Vc = result.Substring(42, 2);
                        float currentATX5Va1 = HexToInt(currentATX5Va);
                        float currentATX5Vb1 = HexToInt(currentATX5Vb);
                        float currentATX5Vc1 = HexToInt(currentATX5Vc);
                        float currentATX5V_Final = (currentATX5Va1 * 65536 + currentATX5Vb1 * 256 + currentATX5Vc1) / 1000f;

                        pmdChannels[33] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "ATX_5V_Voltage",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltATX5V_Final
                        };

                        pmdChannels[34] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "ATX_5V_Current",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentATX5V_Final
                        };

                        pmdChannels[35] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "ATX_5V_Power",
                            PmdChannelType = PmdChannelType.ATX,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltATX5V_Final * currentATX5V_Final
                        };

                        //Channel 5: 12V1        - EPS #1
                        string voltEPS12V1a = result.Substring(44, 2);
                        string voltEPS12V1b = result.Substring(46, 2);
                        float voltEPS12V1a1 = HexToInt(voltEPS12V1a);
                        float voltEPS12V1b1 = HexToInt(voltEPS12V1b);
                        float voltEPS12V1_Final = (voltEPS12V1a1 * 256 + voltEPS12V1b1) / 1000f;

                        string currentEPS12V1a = result.Substring(48, 2);
                        string currentEPS12V1b = result.Substring(50, 2);
                        string currentEPS12V1c = result.Substring(52, 2);
                        float currentEPS12V1a1 = HexToInt(currentEPS12V1a);
                        float currentEPS12V1b1 = HexToInt(currentEPS12V1b);
                        float currentEPS12V1c1 = HexToInt(currentEPS12V1c);
                        float currentEPS12V1_Final = (currentEPS12V1a1 * 65536 + currentEPS12V1b1 * 256 + currentEPS12V1c1) / 1000f;

                        pmdChannels[21] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "EPS_Voltage1",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltEPS12V1_Final
                        };

                        pmdChannels[24] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "EPS_Current1",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentEPS12V1_Final
                        };

                        pmdChannels[27] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "EPS_Power1",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltEPS12V1_Final * currentEPS12V1_Final
                        };

                        //Channel 8: 12V2        - EPS #2
                        string voltEPS12V2a = result.Substring(74, 2);
                        string voltEPS12V2b = result.Substring(76, 2);
                        float voltEPS12V2a1 = HexToInt(voltEPS12V2a);
                        float voltEPS12V2b1 = HexToInt(voltEPS12V2b);
                        float voltEPS12V2_Final = (voltEPS12V2a1 * 256 + voltEPS12V2b1) / 1000f;

                        string currentEPS12V2a = result.Substring(78, 2);
                        string currentEPS12V2b = result.Substring(80, 2);
                        string currentEPS12V2c = result.Substring(82, 2);
                        float currentEPS12V2a1 = HexToInt(currentEPS12V2a);
                        float currentEPS12V2b1 = HexToInt(currentEPS12V2b);
                        float currentEPS12V2c1 = HexToInt(currentEPS12V2c);
                        float currentEPS12V2_Final = (currentEPS12V2a1 * 65536 + currentEPS12V2b1 * 256 + currentEPS12V2c1) / 1000f;

                        pmdChannels[22] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "EPS_Voltage2",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltEPS12V2_Final
                        };

                        pmdChannels[25] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "EPS_Current2",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentEPS12V2_Final
                        };

                        pmdChannels[28] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "EPS_Power2",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltEPS12V2_Final * currentEPS12V2_Final
                        };

                        //Channel 7: 12V3        - EPS #3
                        string voltEPS12V3a = result.Substring(64, 2);
                        string voltEPS12V3b = result.Substring(66, 2);
                        float voltEPS12V3a1 = HexToInt(voltEPS12V3a);
                        float voltEPS12V3b1 = HexToInt(voltEPS12V3b);
                        float voltEPS12V3_Final = (voltEPS12V3a1 * 256 + voltEPS12V3b1) / 1000f;

                        string currentEPS12V3a = result.Substring(68, 2);
                        string currentEPS12V3b = result.Substring(70, 2);
                        string currentEPS12V3c = result.Substring(72, 2);
                        float currentEPS12V3a1 = HexToInt(currentEPS12V3a);
                        float currentEPS12V3b1 = HexToInt(currentEPS12V3b);
                        float currentEPS12V3c1 = HexToInt(currentEPS12V3c);
                        float currentEPS12V3_Final = (currentEPS12V3a1 * 65536 + currentEPS12V3b1 * 256 + currentEPS12V3c1) / 1000f;

                        pmdChannels[23] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "EPS_Voltage3",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltEPS12V3_Final
                        };

                        pmdChannels[26] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "EPS_Current3",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentEPS12V3_Final
                        };

                        pmdChannels[29] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "EPS_Power3",
                            PmdChannelType = PmdChannelType.EPS,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltEPS12V3_Final * currentEPS12V3_Final
                        };

                        //Channel 13: 12V4       - PCIe 6+2 pin #1 & PCIe 12+4 pin #1 
                        string voltPCIe1a = result.Substring(124, 2);
                        string voltPCIe1b = result.Substring(126, 2);
                        float voltPCIe1a1 = HexToInt(voltPCIe1a);
                        float voltPCIe1b1 = HexToInt(voltPCIe1b);
                        float voltPCIe1_Final = (voltPCIe1a1 * 256 + voltPCIe1b1) / 1000f;

                        string currentPCIe1a = result.Substring(128, 2);
                        string currentPCIe1b = result.Substring(130, 2);
                        string currentPCIe1c = result.Substring(132, 2);
                        float currentPCIe1a1 = HexToInt(currentPCIe1a);
                        float currentPCIe1b1 = HexToInt(currentPCIe1b);
                        float currentPCIe1c1 = HexToInt(currentPCIe1c);
                        float currentPCIe1_Final = (currentPCIe1a1 * 65536 + currentPCIe1b1 * 256 + currentPCIe1c1) / 1000f;

                        pmdChannels[6] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_12V_Voltage1",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe1_Final
                        };

                        pmdChannels[11] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_12V_Current1",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentPCIe1_Final
                        };

                        pmdChannels[16] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_12V_Power1",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe1_Final * currentPCIe1_Final
                        };

                        //Channel 10: 12V5       - PCIe 6+2 pin #2 & PCIe 12+4 pin #2 
                        string voltPCIe2a = result.Substring(94, 2);
                        string voltPCIe2b = result.Substring(96, 2);
                        float voltPCIe2a1 = HexToInt(voltPCIe2a);
                        float voltPCIe2b1 = HexToInt(voltPCIe2b);
                        float voltPCIe2_Final = (voltPCIe2a1 * 256 + voltPCIe2b1) / 1000f;

                        string currentPCIe2a = result.Substring(98, 2);
                        string currentPCIe2b = result.Substring(100, 2);
                        string currentPCIe2c = result.Substring(102, 2);
                        float currentPCIe2a1 = HexToInt(currentPCIe2a);
                        float currentPCIe2b1 = HexToInt(currentPCIe2b);
                        float currentPCIe2c1 = HexToInt(currentPCIe2c);
                        float currentPCIe2_Final = (currentPCIe2a1 * 65536 + currentPCIe2b1 * 256 + currentPCIe2c1) / 1000f;

                        pmdChannels[7] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_12V_Voltage2",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe2_Final
                        };

                        pmdChannels[12] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_12V_Current2",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentPCIe2_Final
                        };

                        pmdChannels[17] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_12V_Power2",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe2_Final * currentPCIe2_Final
                        };

                        //Channel 9: 12V6        - PCIe 6+2 pin #3  
                        string voltPCIe3a = result.Substring(84, 2);
                        string voltPCIe3b = result.Substring(86, 2);
                        float voltPCIe3a1 = HexToInt(voltPCIe3a);
                        float voltPCIe3b1 = HexToInt(voltPCIe3b);
                        float voltPCIe3_Final = (voltPCIe3a1 * 256 + voltPCIe3b1) / 1000f;

                        string currentPCIe3a = result.Substring(88, 2);
                        string currentPCIe3b = result.Substring(90, 2);
                        string currentPCIe3c = result.Substring(92, 2);
                        float currentPCIe3a1 = HexToInt(currentPCIe3a);
                        float currentPCIe3b1 = HexToInt(currentPCIe3b);
                        float currentPCIe3c1 = HexToInt(currentPCIe3c);
                        float currentPCIe3_Final = (currentPCIe3a1 * 65536 + currentPCIe3b1 * 256 + currentPCIe3c1) / 1000f;

                        pmdChannels[8] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_12V_Voltage3",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe3_Final
                        };

                        pmdChannels[13] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_12V_Current3",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentPCIe3_Final
                        };

                        pmdChannels[18] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_12V_Power3",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe3_Final * currentPCIe3_Final
                        };

                        // Dummy set PCIe_12V_4
                        pmdChannels[9] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_12V_Voltage4",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = 0
                        };

                        pmdChannels[14] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_12V_Current4",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = 0
                        };

                        pmdChannels[19] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_12V_Power4",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = 0
                        };

                        // Dummy set PCIe_12V_5
                        pmdChannels[10] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_12V_Voltage5",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = 0
                        };

                        pmdChannels[15] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_12V_Current5",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = 0
                        };

                        pmdChannels[20] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_12V_Power5",
                            PmdChannelType = PmdChannelType.PCIe,
                            TimeStamp = _sampleTimeStamp,
                            Value = 0
                        };

                        //Channel 11: 3.3V OPTI  - PCIe Expansion Board
                        string voltPCIe_Slot_33a = result.Substring(104, 2);
                        string voltPCIe_Slot_33b = result.Substring(106, 2);
                        float voltPCIe_Slot_33a1 = HexToInt(voltPCIe_Slot_33a);
                        float voltPCIe_Slot_33b2 = HexToInt(voltPCIe_Slot_33b);
                        float voltPCIe_Slot_33_Final = (voltPCIe_Slot_33a1 * 256 + voltPCIe_Slot_33b2) / 1000f;

                        string currentPCIe_Slot_33a = result.Substring(108, 2);
                        string currentPCIe_Slot_33b = result.Substring(110, 2);
                        string currentPCIe_Slot_33c = result.Substring(112, 2);
                        float currentPCIe_Slot_33a1 = HexToInt(currentPCIe_Slot_33a);
                        float currentPCIe_Slot_33b1 = HexToInt(currentPCIe_Slot_33b);
                        float currentPCIe_Slot_33c1 = HexToInt(currentPCIe_Slot_33c);
                        float currentPCIe_Slot_33_Final = (currentPCIe_Slot_33a1 * 65536 + currentPCIe_Slot_33b1 * 256 + currentPCIe_Slot_33c1) / 1000f;

                        pmdChannels[3] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_Slot_33V_Voltage",
                            PmdChannelType = PmdChannelType.PCIeSlot,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe_Slot_33_Final
                        };

                        pmdChannels[4] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_Slot_33V_Current",
                            PmdChannelType = PmdChannelType.PCIeSlot,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentPCIe_Slot_33_Final
                        };

                        pmdChannels[5] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_Slot_33V_Power",
                            PmdChannelType = PmdChannelType.PCIeSlot,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe_Slot_33_Final * currentPCIe_Slot_33_Final
                        };

                        //Channel 12: 12V OPTI   - PCIe Expansion Board
                        string voltPCIe_Slot_12a = result.Substring(114, 2);
                        string voltPCIe_Slot_12b = result.Substring(116, 2);
                        float voltPCIe_Slot_12a1 = HexToInt(voltPCIe_Slot_12a);
                        float voltPCIe_Slot_12b2 = HexToInt(voltPCIe_Slot_12b);
                        float voltPCIe_Slot_12_Final = (voltPCIe_Slot_12a1 * 256 + voltPCIe_Slot_12b2) / 1000f;

                        string currentPCIe_Slot_12a = result.Substring(118, 2);
                        string currentPCIe_Slot_12b = result.Substring(120, 2);
                        string currentPCIe_Slot_12c = result.Substring(122, 2);
                        float currentPCIe_Slot_12a1 = HexToInt(currentPCIe_Slot_12a);
                        float currentPCIe_Slot_12b1 = HexToInt(currentPCIe_Slot_12b);
                        float currentPCIe_Slot_12c1 = HexToInt(currentPCIe_Slot_12c);
                        float currentPCIe_Slot_12_Final = (currentPCIe_Slot_12a1 * 65536 + currentPCIe_Slot_12b1 * 256 + currentPCIe_Slot_12c1) / 1000f;

                        pmdChannels[0] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Voltage,
                            Name = "PCIe_Slot_12V_Voltage",
                            PmdChannelType = PmdChannelType.PCIeSlot,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe_Slot_12_Final
                        };

                        pmdChannels[1] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Current,
                            Name = "PCIe_Slot_12V_Current",
                            PmdChannelType = PmdChannelType.PCIeSlot,
                            TimeStamp = _sampleTimeStamp,
                            Value = currentPCIe_Slot_12_Final
                        };

                        pmdChannels[2] = new PmdChannel()
                        {
                            Measurand = PmdMeasurand.Power,
                            Name = "PCIe_Slot_12V_Power",
                            PmdChannelType = PmdChannelType.PCIeSlot,
                            TimeStamp = _sampleTimeStamp,
                            Value = voltPCIe_Slot_12_Final * currentPCIe_Slot_12_Final
                        };

                        _pmdChannelStream.OnNext(pmdChannels);
                        _sampleTimeStamp++;
                    }
                }
            }
            catch { throw; }
        }

        private int HexToInt(string value)
        {
            return int.Parse(value, System.Globalization.NumberStyles.HexNumber);
        }

        private async Task ConfigurePMD()
        {
            if (_pmd != null && _pmd.IsOpen)
            {
                try
                {
                    //I need to send Calibration OK command first
                    //PMD.Write(new byte[] { 0xCA,0xAC,0xBD,0x01 }, 0, 4);
                    _pmd.Write(PMDCommands.Calibration_OK, 0, 4);
                    await Task.Delay(100);
                    //Set the PMD in data stream mode
                    //PMD.Write(new byte[] { 0xCA,0xAC,0xBD,0x90 }, 0, 4);
                    _pmd.Write(PMDCommands.Stream_Mode, 0, 4);
                    await Task.Delay(100);
                }
                catch { throw; }
            }
        }
    }

    public static class PMDCommands
    {
        public static readonly byte[] Calibration_OK = new byte[] { 0xCA, 0xAC, 0xBD, 0x01 };
        public static readonly byte[] Clear_Calibration = new byte[] { 0xCA, 0xAC, 0xBD, 0x00 };
        public static readonly byte[] Stream_Mode = new byte[] { 0xCA, 0xAC, 0xBD, 0x90 };
    }

    public static class StatusCodes
    {
        public const string Calibration_Error = "CA-AC";
    }
}
