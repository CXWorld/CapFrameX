// ported from: https://gitlab.com/leogx9r/ryzen_smu
// and: https://github.com/irusanov/SMUDebugTool
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenHardwareMonitor.Hardware
{
    internal class RyzenSMU
    {
        private const byte SMU_PCI_ADDR_REG = 0xC4;
        private const byte SMU_PCI_DATA_REG = 0xC8;
        private const uint SMU_REQ_MAX_ARGS = 6;
        private const uint SMU_RETRIES_MAX = 2*8096;

        private uint _argsAddr;
        private uint _cmdAddr;
        private ulong _dramBaseAddr;
        private uint _pmTableSize;
        private uint _pmTableSizeAlt;
        private uint _pmTableVersion;
        private uint _rspAddr;

        private readonly CpuCodeName _cpuCodeName;
        private readonly Mutex _mutex = new Mutex();
        private readonly bool _supportedCPU;

        private readonly Dictionary<uint, Dictionary<uint, SmuSensorType>> _supportedPmTableVersions
            = new Dictionary<uint, Dictionary<uint, SmuSensorType>>()
        {
            {
                // Zen Raven Ridge APU
                0x001E0004, new Dictionary<uint, SmuSensorType>
                {
                    { 7, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 11, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    //{ 61, new SmuSensorType { Name = "Core", Type = SensorType.Voltage } },
                    //{ 62, new SmuSensorType { Name = "Core", Type = SensorType.Current, Scale = 1} },
                    //{ 63, new SmuSensorType { Name = "Core", Type = SensorType.Power, Scale = 1 } },
                    //{ 65, new SmuSensorType { Name = "SoC", Type = SensorType.Voltage } },
                    { 66, new SmuSensorType { Name = "SoC", Type = SensorType.Current, Scale = 1 } },
                    { 67, new SmuSensorType { Name = "SoC", Type = SensorType.Power, Scale = 1 } },
                    //{ 96, new SmuSensorType { Name = "Core #1", Type = SensorType.Power } },
                    //{ 97, new SmuSensorType { Name = "Core #2", Type = SensorType.Power } },
                    //{ 98, new SmuSensorType { Name = "Core #3", Type = SensorType.Power } },
                    //{ 99, new SmuSensorType { Name = "Core #4", Type = SensorType.Power } },
                    { 108, new SmuSensorType { Name = "Core #1", Type = SensorType.Temperature, Scale = 1 } },
                    { 109, new SmuSensorType { Name = "Core #2", Type = SensorType.Temperature, Scale = 1 } },
                    { 110, new SmuSensorType { Name = "Core #3", Type = SensorType.Temperature, Scale = 1 } },
                    { 111, new SmuSensorType { Name = "Core #4", Type = SensorType.Temperature, Scale = 1 } },
                    { 150, new SmuSensorType { Name = "GFX", Type = SensorType.Voltage, Scale = 1 } },
                    { 151, new SmuSensorType { Name = "GFX", Type = SensorType.Temperature, Scale = 1 } },
                    { 154, new SmuSensorType { Name = "GFX", Type = SensorType.Clock, Scale = 1 } },
                    { 156, new SmuSensorType { Name = "GFX", Type = SensorType.Load, Scale = 1 } },
                    { 166, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 177, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 178, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 342, new SmuSensorType { Name = "Displays", Type = SensorType.Factor, Scale = 1 } },
                }
            },
            {
                // Zen 2
                0x00240903, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    //{ 66, new SmuSensorType { Name = "Bus Speed", Type = SensorType.Clock, Scale = 1 } },
                    //{ 188, new SmuSensorType { Name = "Core #1", Type = SensorType.Clock, Scale = 1000 } },
                    //{ 189, new SmuSensorType { Name = "Core #2", Type = SensorType.Clock, Scale = 1000 } },
                    //{ 190, new SmuSensorType { Name = "Core #3", Type = SensorType.Clock, Scale = 1000 } },
                    //{ 191, new SmuSensorType { Name = "Core #4", Type = SensorType.Clock, Scale = 1000 } },
                    //{ 192, new SmuSensorType { Name = "Core #5", Type = SensorType.Clock, Scale = 1000 } },
                    //{ 193, new SmuSensorType { Name = "Core #6", Type = SensorType.Clock, Scale = 1000 } },
                }
            },
            {
                // Zen 3
                0x00380805, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                //{ 66, new SmuSensorType { Name = "Bus Speed", Type = SensorType.Clock, Scale = 1 } },
                //{ 188, new SmuSensorType { Name = "Core #1", Type = SensorType.Clock, Scale = 1000 } },
                //{ 189, new SmuSensorType { Name = "Core #2", Type = SensorType.Clock, Scale = 1000 } },
                //{ 190, new SmuSensorType { Name = "Core #3", Type = SensorType.Clock, Scale = 1000 } },
                //{ 191, new SmuSensorType { Name = "Core #4", Type = SensorType.Clock, Scale = 1000 } },
                //{ 192, new SmuSensorType { Name = "Core #5", Type = SensorType.Clock, Scale = 1000 } },
                //{ 193, new SmuSensorType { Name = "Core #6", Type = SensorType.Clock, Scale = 1000 } },
                }
            },
            {
                // Zen 3+ Rembrandt
                0x00440005, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    // Additional sensors could be added if layouts are known, but using basic for now
                }
            },
            {
                // Zen 4 Raphael
                0x00610905, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    // Additional sensors could be added if layouts are known, but using basic for now
                }
            },
            {
                // Zen 4 Phoenix
                0x00740005, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    // Additional sensors could be added if layouts are known, but using basic for now
                }
            },
            {
                // Zen 4 Hawk Point
                0x004C0008, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    // Additional sensors could be added if layouts are known, but using basic for now
                }
            },
            {
                // Zen 5 Granite Ridge
                0x00440905, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    // Additional sensors could be added if layouts are known, but using basic for now
                }
            },
            {
                // Zen 5 Strix Point
                0x005D0008, new Dictionary<uint, SmuSensorType>
                {
                    { 15, new SmuSensorType { Name = "TDC", Type = SensorType.Current, Scale = 1 } },
                    { 21, new SmuSensorType { Name = "EDC", Type = SensorType.Current, Scale = 1 } },
                    { 48, new SmuSensorType { Name = "Fabric", Type = SensorType.Clock, Scale = 1 } },
                    { 50, new SmuSensorType { Name = "Uncore", Type = SensorType.Clock, Scale = 1 } },
                    { 51, new SmuSensorType { Name = "Memory", Type = SensorType.Clock, Scale = 1 } },
                    { 115, new SmuSensorType { Name = "SoC", Type = SensorType.Temperature, Scale = 1 } },
                    // Additional sensors could be added if layouts are known, but using basic for now
                }
            }
        };

        public RyzenSMU(uint family, uint model, uint packageType)
        {
            _cpuCodeName = GetCpuCodeName(family, model, packageType);
            _supportedCPU = Environment.Is64BitOperatingSystem == Environment.Is64BitProcess && SetAddresses(_cpuCodeName);
            if (_supportedCPU)
            {
                InpOut.Open();
                SetupPmTableAddrAndSize();
            }
        }
        private static CpuCodeName GetCpuCodeName(uint family, uint model, uint packageType)
        {
            if (family == 0x17)
            {
                switch (model)
                {
                    case 0x01:
                        {
                            return packageType == 7 ? CpuCodeName.Threadripper : CpuCodeName.SummitRidge;
                        }
                    case 0x08:
                        {
                            return packageType == 7 ? CpuCodeName.Colfax : CpuCodeName.PinnacleRidge;
                        }
                    case 0x11:
                        {
                            return CpuCodeName.RavenRidge;
                        }
                    case 0x18:
                        {
                            return packageType == 2 ? CpuCodeName.RavenRidge2 : CpuCodeName.Picasso;
                        }
                    case 0x20:
                        {
                            return CpuCodeName.Dali;
                        }
                    case 0x31:
                        {
                            return CpuCodeName.CastlePeak;
                        }
                    case 0x60:
                        {
                            return CpuCodeName.Renoir;
                        }
                    case 0x71:
                        {
                            return CpuCodeName.Matisse;
                        }
                    case 0x90:
                        {
                            return CpuCodeName.Vangogh;
                        }
                    default:
                        {
                            return CpuCodeName.Undefined;
                        }
                }
            }

            if (family == 0x19)
            {
                switch (model)
                {
                    case 0x00:
                        {
                            return CpuCodeName.Milan;
                        }
                    case 0x20:
                    case 0x21:
                        {
                            return CpuCodeName.Vermeer;
                        }
                    case 0x40:
                        {
                            return CpuCodeName.Rembrandt;
                        }
                    case 0x50:
                        {
                            return CpuCodeName.Cezanne;
                        }
                    case 0x61:
                        {
                            return CpuCodeName.Raphael;
                        }
                    case 0x74:
                        {
                            return CpuCodeName.Phoenix;
                        }
                    case 0x78:
                        {
                            return CpuCodeName.HawkPoint;
                        }
                    default:
                        {
                            return CpuCodeName.Undefined;
                        }
                }
            }

            if (family == 0x1A)
            {
                switch (model)
                {
                    case 0x24:
                        {
                            return CpuCodeName.StrixPoint;
                        }
                    case 0x44:
                        {
                            return CpuCodeName.GraniteRidge;
                        }
                    default:
                        {
                            return CpuCodeName.Undefined;
                        }
                }
            }

            return CpuCodeName.Undefined;
        }

        private bool SetAddresses(CpuCodeName codeName)
        {
            switch (codeName)
            {
                case CpuCodeName.CastlePeak:
                case CpuCodeName.Matisse:
                case CpuCodeName.Vermeer:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    {
                        _cmdAddr = 0x3B10524;
                        _rspAddr = 0x3B10570;
                        _argsAddr = 0x3B10A40;
                        return true;
                    }
                case CpuCodeName.Colfax:
                case CpuCodeName.SummitRidge:
                case CpuCodeName.Threadripper:
                case CpuCodeName.PinnacleRidge:
                    {
                        _cmdAddr = 0x3B1051C;
                        _rspAddr = 0x3B10568;
                        _argsAddr = 0x3B10590;
                        return true;
                    }
                case CpuCodeName.Renoir:
                case CpuCodeName.Picasso:
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Dali:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Phoenix:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.StrixPoint:
                    {
                        _cmdAddr = 0x3B10A20;
                        _rspAddr = 0x3B10A80;
                        _argsAddr = 0x3B10A88;
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }
        public uint GetSmuVersion()
        {
            uint[] args = { 1 };
            if (SendCommand(0x02, ref args))
                return args[0];
            return 0;
        }
        public Dictionary<uint, SmuSensorType> GetPmTableStructure()
        {
            if (!IsPmTableLayoutDefined())
                return new Dictionary<uint, SmuSensorType>();
            return _supportedPmTableVersions[_pmTableVersion];
        }
        public bool IsPmTableLayoutDefined()
        {
            return _supportedPmTableVersions.ContainsKey(_pmTableVersion);
        }
        public float[] GetPmTable()
        {
            if (!_supportedCPU || !TransferTableToDram())
                return new float[] { 0 };
            float[] table = ReadDramToArray();
            // Fix for Zen+ empty values on first call.
            if (table.Length == 0 || table[0] == 0)
            {
                Thread.Sleep(100);
                TransferTableToDram();
                table = ReadDramToArray();
            }
            return table;
        }

        private float[] ReadDramToArray()
        {
            float[] table = new float[_pmTableSize / 4];
            byte[] bytes = InpOut.ReadMemory(new IntPtr((long)_dramBaseAddr), _pmTableSize);
            if (bytes != null)
                Buffer.BlockCopy(bytes, 0, table, 0, bytes.Length);
            return table;
        }

        private bool SetupPmTableAddrAndSize()
        {
            if (_pmTableSize == 0)
                SetupPmTableSize();
            if (_dramBaseAddr == 0)
                SetupDramBaseAddr();
            return _dramBaseAddr != 0 && _pmTableSize != 0;
        }

        private void SetupPmTableSize()
        {
            if (!GetPmTableVersion(ref _pmTableVersion))
                return;

            switch (_cpuCodeName)
            {
                case CpuCodeName.Matisse:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x240902:
                                {
                                    _pmTableSize = 0x514;
                                    break;
                                }
                            case 0x240903:
                                {
                                    _pmTableSize = 0x518;
                                    break;
                                }
                            case 0x240802:
                                {
                                    _pmTableSize = 0x7E0;
                                    break;
                                }
                            case 0x240803:
                                {
                                    _pmTableSize = 0x7E4;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Vermeer:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x2D0903:
                                {
                                    _pmTableSize = 0x594;
                                    break;
                                }
                            case 0x380904:
                                {
                                    _pmTableSize = 0x5A4;
                                    break;
                                }
                            case 0x380905:
                                {
                                    _pmTableSize = 0x5D0;
                                    break;
                                }
                            case 0x2D0803:
                                {
                                    _pmTableSize = 0x894;
                                    break;
                                }
                            case 0x380804:
                                {
                                    _pmTableSize = 0x8A4;
                                    break;
                                }
                            case 0x380805:
                                {
                                    _pmTableSize = 0x8F0;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Raphael:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x610902:
                                {
                                    _pmTableSize = 0x594;
                                    break;
                                }
                            case 0x610903:
                                {
                                    _pmTableSize = 0x5A4;
                                    break;
                                }
                            case 0x610904:
                                {
                                    _pmTableSize = 0x5D0;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.GraniteRidge:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x440902:
                                {
                                    _pmTableSize = 0x594;
                                    break;
                                }
                            case 0x440903:
                                {
                                    _pmTableSize = 0x5A4;
                                    break;
                                }
                            case 0x440904:
                                {
                                    _pmTableSize = 0x5D0;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Renoir:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x370000:
                                {
                                    _pmTableSize = 0x794;
                                    break;
                                }
                            case 0x370001:
                                {
                                    _pmTableSize = 0x884;
                                    break;
                                }
                            case 0x370002:
                            case 0x370003:
                                {
                                    _pmTableSize = 0x88C;
                                    break;
                                }
                            case 0x370004:
                                {
                                    _pmTableSize = 0x8AC;
                                    break;
                                }
                            case 0x370005:
                                {
                                    _pmTableSize = 0x8C8;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Cezanne:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x400005:
                                {
                                    _pmTableSize = 0x944;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Rembrandt:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x440005:
                                {
                                    _pmTableSize = 0x944;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Phoenix:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x740005:
                                {
                                    _pmTableSize = 0x944;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.HawkPoint:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x4C0008:
                                {
                                    _pmTableSize = 0x944;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.StrixPoint:
                    {
                        switch (_pmTableVersion)
                        {
                            case 0x5D0008:
                                {
                                    _pmTableSize = 0x944;
                                    break;
                                }
                            default:
                                {
                                    return;
                                }
                        }
                        break;
                    }
                case CpuCodeName.Picasso:
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    {
                        _pmTableSizeAlt = 0xA4;
                        _pmTableSize = 0x608 + _pmTableSizeAlt;
                        break;
                    }
                default:
                    {
                        return;
                    }
            }
        }
        private bool GetPmTableVersion(ref uint version)
        {
            uint[] args = { 0 };
            uint fn;
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.Picasso:
                    {
                        fn = 0x0c;
                        break;
                    }
                case CpuCodeName.Matisse:
                case CpuCodeName.Vermeer:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    {
                        fn = 0x08;
                        break;
                    }
                case CpuCodeName.Renoir:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Phoenix:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.StrixPoint:
                    {
                        fn = 0x06;
                        break;
                    }
                default:
                    {
                        return false;
                    }
            }
            bool ret = SendCommand(fn, ref args);
            version = args[0];
            return ret;
        }

        private void SetupAddrClass1(uint[] fn)
        {
            uint[] args = { 1, 1 };
            bool command = SendCommand(fn[0], ref args);
            if (!command)
                return;
            _dramBaseAddr = args[0] | ((ulong)args[1] << 32);
        }

        private void SetupAddrClass2(uint[] fn)
        {
            uint[] args = { 0, 0, 0, 0, 0, 0 };
            bool command = SendCommand(fn[0], ref args);
            if (!command)
                return;
            args = new uint[] { 0 };
            command = SendCommand(fn[1], ref args);
            if (!command)
                return;
            _dramBaseAddr = args[0];
        }

        private void SetupAddrClass3(uint[] fn)
        {
            uint[] parts = { 0, 0 };
            // == Part 1 ==
            uint[] args = { 3 };
            bool command = SendCommand(fn[0], ref args);
            if (!command)
                return;
            args = new uint[] { 3 };
            command = SendCommand(fn[2], ref args);
            if (!command)
                return;
            // 1st Base.
            parts[0] = args[0];
            // == Part 1 End ==
            // == Part 2 ==
            args = new uint[] { 3 };
            command = SendCommand(fn[1], ref args);
            if (!command)
                return;
            args = new uint[] { 5 };
            command = SendCommand(fn[0], ref args);
            if (!command)
                return;
            args = new uint[] { 5 };
            command = SendCommand(fn[2], ref args);
            if (!command)
                return;
            // 2nd base.
            parts[1] = args[0];
            // == Part 2 End ==
            _dramBaseAddr = parts[0] & 0xFFFFFFFFUL;
        }

        private void SetupDramBaseAddr()
        {
            uint[] fn = { 0, 0, 0 };
            switch (_cpuCodeName)
            {
                case CpuCodeName.Vermeer:
                case CpuCodeName.Matisse:
                case CpuCodeName.CastlePeak:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    {
                        fn[0] = 0x06;
                        SetupAddrClass1(fn);
                        return;
                    }
                case CpuCodeName.Renoir:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Phoenix:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.StrixPoint:
                    {
                        fn[0] = 0x66;
                        SetupAddrClass1(fn);
                        return;
                    }
                case CpuCodeName.Colfax:
                case CpuCodeName.PinnacleRidge:
                    {
                        fn[0] = 0x0b;
                        fn[1] = 0x0c;
                        SetupAddrClass2(fn);
                        return;
                    }
                case CpuCodeName.Dali:
                case CpuCodeName.Picasso:
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    {
                        fn[0] = 0x0a;
                        fn[1] = 0x3d;
                        fn[2] = 0x0b;
                        SetupAddrClass3(fn);
                        return;
                    }
                default:
                    {
                        return;
                    }
            }
        }
        public bool TransferTableToDram()
        {
            uint[] args = { 0 };
            uint fn;
            switch (_cpuCodeName)
            {
                case CpuCodeName.Matisse:
                case CpuCodeName.Vermeer:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    {
                        fn = 0x05;
                        break;
                    }
                case CpuCodeName.Renoir:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Phoenix:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.StrixPoint:
                    {
                        args[0] = 3;
                        fn = 0x65;
                        break;
                    }
                case CpuCodeName.Picasso:
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    {
                        args[0] = 3;
                        fn = 0x3d;
                        break;
                    }
                default:
                    {
                        return false;
                    }
            }
            return SendCommand(fn, ref args);
        }

        private bool SendCommand(uint msg, ref uint[] args)
        {
            uint[] cmdArgs = new uint[SMU_REQ_MAX_ARGS];
            int argsLength = Math.Min(args.Length, cmdArgs.Length);

            for (int i = 0; i < argsLength; ++i)
                cmdArgs[i] = args[i];

            uint tmp = 0;
            if (Ring0.WaitPciBusMutex(10000))
            {
                // Step 1: Wait until the RSP register is non-zero.
                tmp = 0;
                uint retries = SMU_RETRIES_MAX;
                do
                {
                    if (!ReadReg(_rspAddr, ref tmp))
                    {
                        Ring0.ReleasePciBusMutex();
                        return false;
                    }
                }

                while (tmp == 0 && 0 != retries--);

                // Step 1.b: A command is still being processed meaning a new command cannot be issued.
                if (retries == 0 && tmp == 0)
                {
                    Ring0.ReleasePciBusMutex();
                    return false;
                }

                // Step 2: Write zero (0) to the RSP register
                WriteReg(_rspAddr, 0);
                // Step 3: Write the argument(s) into the argument register(s)
                for (int i = 0; i < cmdArgs.Length; ++i)
                    WriteReg(_argsAddr + (uint)(i * 4), cmdArgs[i]);

                // Step 4: Write the message Id into the Message ID register
                WriteReg(_cmdAddr, msg);

                // Step 5: Wait until the Response register is non-zero.
                tmp = 0;
                retries = SMU_RETRIES_MAX;
                do
                {
                    if (!ReadReg(_rspAddr, ref tmp))
                    {
                        Ring0.ReleasePciBusMutex();
                        return false;
                    }
                }

                while (tmp == 0 && retries-- != 0);

                if (retries == 0 && tmp != (uint)Status.OK)
                {
                    Ring0.ReleasePciBusMutex();
                    return false;
                }

                // Step 6: If the Response register contains OK, then SMU has finished processing the message.
                args = new uint[SMU_REQ_MAX_ARGS];
                for (byte i = 0; i < SMU_REQ_MAX_ARGS; i++)
                {
                    if (!ReadReg(_argsAddr + (uint)(i * 4), ref args[i]))
                    {
                        Ring0.ReleasePciBusMutex();
                        return false;
                    }
                }

                ReadReg(_rspAddr, ref tmp);
                Ring0.ReleasePciBusMutex();
            }

            return tmp == (uint)Status.OK;
        }

        private static void WriteReg(uint addr, uint data)
        {
            if (Ring0.WaitPciBusMutex(10))
            {
                if (Ring0.WritePciConfig(0x00, SMU_PCI_ADDR_REG, addr))
                {
                    Ring0.WritePciConfig(0x00, SMU_PCI_DATA_REG, data);
                }
                Ring0.ReleasePciBusMutex();
            }
        }

        private static bool ReadReg(uint addr, ref uint data)
        {
            bool read = false;
            if (Ring0.WaitPciBusMutex(10))
            {
                if (Ring0.WritePciConfig(0x00, SMU_PCI_ADDR_REG, addr))
                {
                    read = Ring0.ReadPciConfig(0x00, SMU_PCI_DATA_REG, out data);
                }
                Ring0.ReleasePciBusMutex();
            }
            return read;
        }

        public struct SmuSensorType
        {
            public string Name;
            public SensorType Type;
            public float Scale;
        }

        private enum Status : uint
        {
            OK = 0x01,
            Failed = 0xFF,
            UnknownCmd = 0xFE,
            CmdRejectedPrereq = 0xFD,
            CmdRejectedBusy = 0xFC
        }

        private enum CpuCodeName
        {
            Undefined,
            Colfax,
            Renoir,
            Picasso,
            Matisse,
            Threadripper,
            CastlePeak,
            RavenRidge,
            RavenRidge2,
            SummitRidge,
            PinnacleRidge,
            Rembrandt,
            Vermeer,
            Vangogh,
            Cezanne,
            Milan,
            Dali,
            Raphael,
            Phoenix,
            HawkPoint,
            GraniteRidge,
            StrixPoint
        }
    }
}