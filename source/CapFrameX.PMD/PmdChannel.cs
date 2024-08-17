using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.PMD
{
    public struct PmdChannel
    {
        public string Name;
        public PmdChannelType PmdChannelType;
        public PmdMeasurand Measurand;
        public float Value;
        public long TimeStamp;
    }

    public struct PmdChannelArrayPosition
    {
        public int Index;
        public string Name;
        public PmdChannelType PmdChannelType;
        public PmdMeasurand Measurand;
    }

    public static class PmdChannelExtensions
    {
        // GPU
        public static int[] GPUCurrentIndexGroup { get; private set; }

        public static int[] GPUVoltageIndexGroup { get; private set; }

        public static int[] GPUPowerIndexGroup { get; private set; }


        // PCIeSlot
        public static int[] PCIeSlotCurrentIndexGroup { get; private set; }

        public static int[] PCIeSlotVoltageIndexGroup { get; private set; }

        public static int[] PCIeSlotPowerIndexGroup { get; private set; }

        // PCIe
        public static int[] PCIeCurrentIndexGroup { get; private set; }

        public static int[] PCIeVoltageIndexGroup { get; private set; }

        public static int[] PCIePowerIndexGroup { get; private set; }

        // EPS
        public static int[] EPSCurrentIndexGroup { get; private set; }

        public static int[] EPSVoltageIndexGroup { get; private set; }

        public static int[] EPSPowerIndexGroup { get; private set; }

        // ATX
        public static int[] ATXCurrentIndexGroup { get; private set; }

        public static int[] ATXVoltageIndexGroup { get; private set; }

        public static int[] ATXPowerIndexGroup { get; private set; }

        // System
        public static int[] SystemCurrentIndexGroup { get; private set; }

        public static int[] SystemVoltageIndexGroup { get; private set; }

        public static int[] SystemPowerIndexGroup { get; private set; }


        public static PmdChannelArrayPosition[] PmdChannelIndexMapping { get; private set; }

        public static Dictionary<int, int[]> PowerDependcyIndices { get; private set; }

        public static void Initialize()
        {
            PowerDependcyIndices
                = new Dictionary<int, int[]>
                {
                    { 2, new int[]{0, 1 }},

                    { 5, new int[]{3, 4 }},

                    { 16, new int[]{6, 11 }},
                    { 17, new int[]{7, 12 }},
                    { 18, new int[]{8, 13 }},
                    { 19, new int[]{9, 14 }},
                    { 20, new int[]{10, 15 }},

                    { 27, new int[]{21, 24 }},
                    { 28, new int[]{22, 25 }},
                    { 29, new int[]{23, 26 }},

                    { 32, new int[]{30, 31 }},

                    { 35, new int[]{33, 34 }},

                    { 38, new int[]{36, 37 }},

                    { 41, new int[]{39, 40 }}
                };

            PmdChannelIndexMapping
                = new PmdChannelArrayPosition[]
                {
                    new PmdChannelArrayPosition(){ Index= 0, Name= "PCIe_Slot_12V_Voltage", PmdChannelType= PmdChannelType.PCIeSlot, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 1, Name= "PCIe_Slot_12V_Current", PmdChannelType= PmdChannelType.PCIeSlot, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 2, Name= "PCIe_Slot_12V_Power", PmdChannelType= PmdChannelType.PCIeSlot, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 3, Name= "PCIe_Slot_33V_Voltage", PmdChannelType= PmdChannelType.PCIeSlot, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 4, Name= "PCIe_Slot_33V_Current", PmdChannelType= PmdChannelType.PCIeSlot, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 5, Name= "PCIe_Slot_33V_Power", PmdChannelType= PmdChannelType.PCIeSlot, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 6, Name= "PCIe_12V_Voltage1", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 7, Name= "PCIe_12V_Voltage2", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 8, Name= "PCIe_12V_Voltage3", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 9, Name= "PCIe_12V_Voltage4", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 10, Name= "PCIe_12V_Voltage5", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Voltage},

                    new PmdChannelArrayPosition(){ Index= 11, Name= "PCIe_12V_Current1", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 12, Name= "PCIe_12V_Current2", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 13, Name= "PCIe_12V_Current3", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 14, Name= "PCIe_12V_Current4", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 15, Name= "PCIe_12V_Current5", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Current},

                    new PmdChannelArrayPosition(){ Index= 16, Name= "PCIe_12V_Power1", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Power},
                    new PmdChannelArrayPosition(){ Index= 17, Name= "PCIe_12V_Power2", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Power},
                    new PmdChannelArrayPosition(){ Index= 18, Name= "PCIe_12V_Power3", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Power},
                    new PmdChannelArrayPosition(){ Index= 19, Name= "PCIe_12V_Power4", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Power},
                    new PmdChannelArrayPosition(){ Index= 20, Name= "PCIe_12V_Power5", PmdChannelType= PmdChannelType.PCIe, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 21, Name= "EPS_Voltage1", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 22, Name= "EPS_Voltage2", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 23, Name= "EPS_Voltage3", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Voltage},

                    new PmdChannelArrayPosition(){ Index= 24, Name= "EPS_Current1", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 25, Name= "EPS_Current2", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 26, Name= "EPS_Current3", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Current},

                    new PmdChannelArrayPosition(){ Index= 27, Name= "EPS_Power1", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Power},
                    new PmdChannelArrayPosition(){ Index= 28, Name= "EPS_Power2", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Power},
                    new PmdChannelArrayPosition(){ Index= 29, Name= "EPS_Power3", PmdChannelType= PmdChannelType.EPS, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 30, Name= "ATX_12V_Voltage", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 31, Name= "ATX_12V_Current", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 32, Name= "ATX_12V_Power", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 33, Name= "ATX_5V_Voltage", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 34, Name= "ATX_5V_Current", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 35, Name= "ATX_5V_Power", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 36, Name= "ATX_33V_Voltage", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 37, Name= "ATX_33V_Current", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 38, Name= "ATX_33V_Power", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Power},

                    new PmdChannelArrayPosition(){ Index= 39, Name= "ATX_STB_Voltage", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Voltage},
                    new PmdChannelArrayPosition(){ Index= 40, Name= "ATX_STB_Current", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Current},
                    new PmdChannelArrayPosition(){ Index= 41, Name= "ATX_STB_Power", PmdChannelType= PmdChannelType.ATX, Measurand = PmdMeasurand.Power},
                };

            ATXPowerIndexGroup
              = PmdChannelIndexMapping
               .Where(ch => ch.PmdChannelType == PmdChannelType.ATX && ch.Measurand == PmdMeasurand.Power)
               .Select(ch => ch.Index)
               .ToArray();

            ATXVoltageIndexGroup
                = PmdChannelIndexMapping
                .Where(ch => ch.PmdChannelType == PmdChannelType.ATX && ch.Measurand == PmdMeasurand.Voltage)
                .Select(ch => ch.Index)
                .ToArray();

            ATXCurrentIndexGroup
                = PmdChannelIndexMapping
                .Where(ch => ch.PmdChannelType == PmdChannelType.ATX && ch.Measurand == PmdMeasurand.Current)
                .Select(ch => ch.Index)
                .ToArray();

            // PCIeSlot
            PCIeSlotCurrentIndexGroup
                = PmdChannelIndexMapping
                .Where(ch => ch.PmdChannelType == PmdChannelType.PCIeSlot && ch.Measurand == PmdMeasurand.Current)
                .Select(ch => ch.Index)
                .ToArray();

            PCIeSlotVoltageIndexGroup
                = PmdChannelIndexMapping
                .Where(ch => ch.PmdChannelType == PmdChannelType.PCIeSlot && ch.Measurand == PmdMeasurand.Voltage)
                .Select(ch => ch.Index)
                .ToArray();

            PCIeSlotPowerIndexGroup
                = PmdChannelIndexMapping
                .Where(ch => ch.PmdChannelType == PmdChannelType.PCIeSlot && ch.Measurand == PmdMeasurand.Power)
                .Select(ch => ch.Index)
                .ToArray();

            // PCIe
            PCIeCurrentIndexGroup
               = PmdChannelIndexMapping
               .Where(ch => ch.PmdChannelType == PmdChannelType.PCIe && ch.Measurand == PmdMeasurand.Current)
               .Select(ch => ch.Index)
               .ToArray();

            PCIeVoltageIndexGroup
               = PmdChannelIndexMapping
               .Where(ch => ch.PmdChannelType == PmdChannelType.PCIe && ch.Measurand == PmdMeasurand.Voltage)
               .Select(ch => ch.Index)
               .ToArray();

            PCIePowerIndexGroup
              = PmdChannelIndexMapping
               .Where(ch => ch.PmdChannelType == PmdChannelType.PCIe && ch.Measurand == PmdMeasurand.Power)
               .Select(ch => ch.Index)
               .ToArray();

            // EPS
            EPSCurrentIndexGroup
               = PmdChannelIndexMapping
               .Where(ch => ch.PmdChannelType == PmdChannelType.EPS && ch.Measurand == PmdMeasurand.Current)
               .Select(ch => ch.Index)
               .ToArray();

            EPSVoltageIndexGroup
              = PmdChannelIndexMapping
               .Where(ch => ch.PmdChannelType == PmdChannelType.EPS && ch.Measurand == PmdMeasurand.Voltage)
               .Select(ch => ch.Index)
               .ToArray();

            EPSPowerIndexGroup
                = PmdChannelIndexMapping
                .Where(ch => ch.PmdChannelType == PmdChannelType.EPS && ch.Measurand == PmdMeasurand.Power)
                .Select(ch => ch.Index)
                .ToArray();

            // GPU
            GPUCurrentIndexGroup
                = PCIeSlotCurrentIndexGroup.Concat(PCIeCurrentIndexGroup).ToArray();

            GPUVoltageIndexGroup
                 = PCIeSlotVoltageIndexGroup.Concat(PCIeVoltageIndexGroup).ToArray();

            GPUPowerIndexGroup
                = PCIeSlotPowerIndexGroup.Concat(PCIePowerIndexGroup).ToArray();

            // System
            SystemCurrentIndexGroup
               = PCIeCurrentIndexGroup.Concat(EPSCurrentIndexGroup)
               .Concat(ATXPowerIndexGroup).ToArray();

            SystemVoltageIndexGroup
                 = PCIeVoltageIndexGroup.Concat(EPSVoltageIndexGroup)
                 .Concat(ATXVoltageIndexGroup).ToArray();

            SystemPowerIndexGroup
                = PCIePowerIndexGroup.Concat(EPSPowerIndexGroup)
                .Concat(ATXPowerIndexGroup).ToArray();
        }
    }
}
