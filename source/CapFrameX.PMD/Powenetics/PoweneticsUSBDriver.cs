using RJCP.IO.Ports;
using System;
using System.Buffers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using CapFrameX.Contracts.PMD;

namespace CapFrameX.PMD.Powenetics
{
    public class PoweneticsUSBDriver : IPoweneticsDriver
    {
        private const byte HeaderByteA = 0xCA;
        private const byte HeaderByteB = 0xAC;
        private const int PacketFrameLength = 69;

        private SerialPortStream _pmd;
        private long _sampleTimeStamp = 0;
        private int _lostPacketsCounter = 0;
        private readonly object _bufferLock = new object();
        private byte[] _receiveBuffer = new byte[4096];
        private int _receiveBufferCount = 0;

        private readonly ILogger<PoweneticsUSBDriver> _logger;
        private readonly ISubject<PoweneticsChannel[]> _pmdChannelStream = new Subject<PoweneticsChannel[]>();
        private readonly ISubject<EPmdDriverStatus> _pmdstatusStream = new Subject<EPmdDriverStatus>();
        private readonly ISubject<int> _lostPacketsCounterStream = new Subject<int>();

        public IObservable<PoweneticsChannel[]> PmdChannelStream => _pmdChannelStream.AsObservable();

        public IObservable<EPmdDriverStatus> PmdstatusStream => _pmdstatusStream.AsObservable();

        public IObservable<int> LostPacketsCounterStream => _lostPacketsCounterStream.AsObservable();

        public PoweneticsUSBDriver(ILogger<PoweneticsUSBDriver> logger)
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
            if (_pmd != null && !_pmd.IsDisposed)
            {
                if (_pmd.IsOpen) _pmd.Close();
                _pmd.Dispose();
            }

            _sampleTimeStamp = 0;
            _lostPacketsCounter = 0;
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
                int bytesToRead = _pmd.BytesToRead;
                if (bytesToRead <= 0)
                {
                    return;
                }

                var tempBuffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
                try
                {
                    int bytesRead = _pmd.Read(tempBuffer, 0, bytesToRead);
                    if (bytesRead <= 0)
                    {
                        return;
                    }

                    lock (_bufferLock)
                    {
                        EnsureReceiveBufferCapacity(_receiveBufferCount + bytesRead);
                        Buffer.BlockCopy(tempBuffer, 0, _receiveBuffer, _receiveBufferCount, bytesRead);
                        _receiveBufferCount += bytesRead;
                        ProcessReceiveBuffer();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempBuffer);
                }
            }
            catch { throw; }
        }

        // max = 65536
        private int _previousPacketNumber = 0;
        private int _currentPacketNumber = 0;

        private int ExtractPacketNumber(byte[] data, int offset)
        {
            return ReadUInt16(data, offset);
        }

        private void ProcessReceiveBuffer()
        {
            int readIndex = 0;
            while (true)
            {
                int headerIndex = FindHeaderIndex(_receiveBuffer, readIndex, _receiveBufferCount);
                if (headerIndex < 0)
                {
                    if (_receiveBufferCount > 0 && _receiveBuffer[_receiveBufferCount - 1] == HeaderByteA)
                    {
                        _receiveBuffer[0] = HeaderByteA;
                        _receiveBufferCount = 1;
                    }
                    else
                    {
                        _receiveBufferCount = 0;
                    }
                    return;
                }

                if (_receiveBufferCount - headerIndex < PacketFrameLength)
                {
                    if (headerIndex > 0)
                    {
                        Buffer.BlockCopy(_receiveBuffer, headerIndex, _receiveBuffer, 0, _receiveBufferCount - headerIndex);
                        _receiveBufferCount -= headerIndex;
                    }
                    return;
                }

                int payloadOffset = headerIndex + 2;
                ProcessData(_receiveBuffer, payloadOffset);
                readIndex = headerIndex + PacketFrameLength;

                if (readIndex >= _receiveBufferCount)
                {
                    _receiveBufferCount = 0;
                    return;
                }
            }
        }

        private static int FindHeaderIndex(byte[] buffer, int startIndex, int count)
        {
            for (int i = startIndex; i < count - 1; i++)
            {
                if (buffer[i] == HeaderByteA && buffer[i + 1] == HeaderByteB)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureReceiveBufferCapacity(int requiredSize)
        {
            if (_receiveBuffer.Length >= requiredSize)
            {
                return;
            }

            int newSize = _receiveBuffer.Length * 2;
            while (newSize < requiredSize)
            {
                newSize *= 2;
            }

            var newBuffer = new byte[newSize];
            Buffer.BlockCopy(_receiveBuffer, 0, newBuffer, 0, _receiveBufferCount);
            _receiveBuffer = newBuffer;
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] * 256 + data[offset + 1];
        }

        private static int ReadUInt24(byte[] data, int offset)
        {
            return data[offset] * 65536 + data[offset + 1] * 256 + data[offset + 2];
        }

        private static float ReadVoltage(byte[] data, int offset)
        {
            return ReadUInt16(data, offset) / 1000f;
        }

        private static float ReadCurrent(byte[] data, int offset)
        {
            return ReadUInt24(data, offset) / 1000f;
        }
        private void ProcessData(byte[] data, int offset)
        {
            try
            {
                _previousPacketNumber = _currentPacketNumber;
                _currentPacketNumber = ExtractPacketNumber(data, offset);

                if (_previousPacketNumber > 0)
                {
                    if (_currentPacketNumber - _previousPacketNumber != 1
                        && _currentPacketNumber > _previousPacketNumber)
                    {
                        _lostPacketsCounter += _currentPacketNumber - _previousPacketNumber - 1;
                        _lostPacketsCounterStream.OnNext(_lostPacketsCounter);
                    }
                }

                var pmdChannels = new PoweneticsChannel[PoweneticsChannelExtensions.PmdChannelIndexMapping.Length];

                //Channel 1: 3.3V        - 24pin ATX
                float voltATX33V_Final = ReadVoltage(data, offset + 2);
                float currentATX33V_Final = ReadCurrent(data, offset + 4);

                pmdChannels[36] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "ATX_33V_Voltage",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltATX33V_Final
                };

                pmdChannels[37] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "ATX_33V_Current",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentATX33V_Final
                };

                pmdChannels[38] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "ATX_33V_Power",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltATX33V_Final * currentATX33V_Final
                };

                //Channel 2: 5Vsb        - 24pin ATX
                //Only if I have a 24 pin connector installed
                if (voltATX33V_Final > 1)
                {
                    float volt5VSB_Final = ReadVoltage(data, offset + 7);
                    float current5VSB_Final = ReadCurrent(data, offset + 9);

                    pmdChannels[39] = new PoweneticsChannel()
                    {
                        Measurand = PoweneticsMeasurand.Voltage,
                        Name = "ATX_STB_Voltage",
                        PmdChannelType = PoweneticsChannelType.ATX,
                        TimeStamp = _sampleTimeStamp,
                        Value = volt5VSB_Final
                    };

                    pmdChannels[40] = new PoweneticsChannel()
                    {
                        Measurand = PoweneticsMeasurand.Current,
                        Name = "ATX_STB_Current",
                        PmdChannelType = PoweneticsChannelType.ATX,
                        TimeStamp = _sampleTimeStamp,
                        Value = current5VSB_Final
                    };

                    pmdChannels[41] = new PoweneticsChannel()
                    {
                        Measurand = PoweneticsMeasurand.Power,
                        Name = "ATX_STB_Power",
                        PmdChannelType = PoweneticsChannelType.ATX,
                        TimeStamp = _sampleTimeStamp,
                        Value = volt5VSB_Final * current5VSB_Final
                    };
                }

                //Channel 3: 12V         - 24pin ATX & 10pin ATX
                float voltATX12V_Final = ReadVoltage(data, offset + 12);
                float currentATX12V_Final = ReadCurrent(data, offset + 14);

                pmdChannels[30] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "ATX_12V_Voltage",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltATX12V_Final
                };

                pmdChannels[31] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "ATX_12V_Current",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentATX12V_Final
                };

                pmdChannels[32] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "ATX_12V_Power",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltATX12V_Final * currentATX12V_Final
                };

                //Channel 4: 5V          - 24pin ATX
                float voltATX5V_Final = ReadVoltage(data, offset + 17);
                float currentATX5V_Final = ReadCurrent(data, offset + 19);

                pmdChannels[33] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "ATX_5V_Voltage",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltATX5V_Final
                };

                pmdChannels[34] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "ATX_5V_Current",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentATX5V_Final
                };

                pmdChannels[35] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "ATX_5V_Power",
                    PmdChannelType = PoweneticsChannelType.ATX,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltATX5V_Final * currentATX5V_Final
                };

                //Channel 5: 12V1        - EPS #1
                float voltEPS12V1_Final = ReadVoltage(data, offset + 22);
                float currentEPS12V1_Final = ReadCurrent(data, offset + 24);

                pmdChannels[21] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "EPS_Voltage1",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltEPS12V1_Final
                };

                pmdChannels[24] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "EPS_Current1",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentEPS12V1_Final
                };

                pmdChannels[27] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "EPS_Power1",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltEPS12V1_Final * currentEPS12V1_Final
                };

                //Channel 7: 12V3        - EPS #3
                float voltEPS12V3_Final = ReadVoltage(data, offset + 32);
                float currentEPS12V3_Final = ReadCurrent(data, offset + 34);

                pmdChannels[23] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "EPS_Voltage3",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltEPS12V3_Final
                };

                pmdChannels[26] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "EPS_Current3",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentEPS12V3_Final
                };

                pmdChannels[29] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "EPS_Power3",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltEPS12V3_Final * currentEPS12V3_Final
                };

                //Channel 8: 12V2        - EPS #2
                float voltEPS12V2_Final = ReadVoltage(data, offset + 37);
                float currentEPS12V2_Final = ReadCurrent(data, offset + 39);

                pmdChannels[22] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "EPS_Voltage2",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltEPS12V2_Final
                };

                pmdChannels[25] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "EPS_Current2",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentEPS12V2_Final
                };

                pmdChannels[28] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "EPS_Power2",
                    PmdChannelType = PoweneticsChannelType.EPS,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltEPS12V2_Final * currentEPS12V2_Final
                };

                //Channel 9: 12V6        - PCIe 6+2 pin #3
                float voltPCIe3_Final = ReadVoltage(data, offset + 42);
                float currentPCIe3_Final = ReadCurrent(data, offset + 44);

                pmdChannels[8] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_12V_Voltage3",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe3_Final
                };

                pmdChannels[13] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_12V_Current3",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentPCIe3_Final
                };

                pmdChannels[18] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_12V_Power3",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe3_Final * currentPCIe3_Final
                };

                //Channel 10: 12V5       - PCIe 6+2 pin #2 & PCIe 12+4 pin #2
                float voltPCIe2_Final = ReadVoltage(data, offset + 47);
                float currentPCIe2_Final = ReadCurrent(data, offset + 49);

                pmdChannels[7] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_12V_Voltage2",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe2_Final
                };

                pmdChannels[12] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_12V_Current2",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentPCIe2_Final
                };

                pmdChannels[17] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_12V_Power2",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe2_Final * currentPCIe2_Final
                };

                //Channel 11: 3.3V OPTI  - PCIe Expansion Board
                float voltPCIe_Slot_33_Final = ReadVoltage(data, offset + 52);
                float currentPCIe_Slot_33_Final = ReadCurrent(data, offset + 54);

                pmdChannels[3] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_Slot_33V_Voltage",
                    PmdChannelType = PoweneticsChannelType.PCIeSlot,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe_Slot_33_Final
                };

                pmdChannels[4] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_Slot_33V_Current",
                    PmdChannelType = PoweneticsChannelType.PCIeSlot,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentPCIe_Slot_33_Final
                };

                pmdChannels[5] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_Slot_33V_Power",
                    PmdChannelType = PoweneticsChannelType.PCIeSlot,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe_Slot_33_Final * currentPCIe_Slot_33_Final
                };

                //Channel 12: 12V OPTI   - PCIe Expansion Board
                float voltPCIe_Slot_12_Final = ReadVoltage(data, offset + 57);
                float currentPCIe_Slot_12_Final = ReadCurrent(data, offset + 59);

                pmdChannels[0] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_Slot_12V_Voltage",
                    PmdChannelType = PoweneticsChannelType.PCIeSlot,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe_Slot_12_Final
                };

                pmdChannels[1] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_Slot_12V_Current",
                    PmdChannelType = PoweneticsChannelType.PCIeSlot,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentPCIe_Slot_12_Final
                };

                pmdChannels[2] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_Slot_12V_Power",
                    PmdChannelType = PoweneticsChannelType.PCIeSlot,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe_Slot_12_Final * currentPCIe_Slot_12_Final
                };

                //Channel 13: 12V4       - PCIe 6+2 pin #1 & PCIe 12+4 pin #1
                float voltPCIe1_Final = ReadVoltage(data, offset + 62);
                float currentPCIe1_Final = ReadCurrent(data, offset + 64);

                pmdChannels[6] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_12V_Voltage1",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe1_Final
                };

                pmdChannels[11] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_12V_Current1",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = currentPCIe1_Final
                };

                pmdChannels[16] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_12V_Power1",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = voltPCIe1_Final * currentPCIe1_Final
                };

                // Dummy set PCIe_12V_4
                pmdChannels[9] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_12V_Voltage4",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = 0
                };

                pmdChannels[14] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_12V_Current4",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = 0
                };

                pmdChannels[19] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_12V_Power4",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = 0
                };

                // Dummy set PCIe_12V_5
                pmdChannels[10] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Voltage,
                    Name = "PCIe_12V_Voltage5",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = 0
                };

                pmdChannels[15] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Current,
                    Name = "PCIe_12V_Current5",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = 0
                };

                pmdChannels[20] = new PoweneticsChannel()
                {
                    Measurand = PoweneticsMeasurand.Power,
                    Name = "PCIe_12V_Power5",
                    PmdChannelType = PoweneticsChannelType.PCIe,
                    TimeStamp = _sampleTimeStamp,
                    Value = 0
                };

                _pmdChannelStream.OnNext(pmdChannels);
                _sampleTimeStamp++;
            }
            catch { throw; }
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
