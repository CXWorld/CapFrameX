using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Sensor
{
    /// <summary>
    /// The simulated CPUs must name their cores exactly like the real drivers do on hybrid parts
    /// ("Core #N P" / "E" / "LPE" on Intel, "Core #N P" / "D" on AMD, no suffix on homogeneous parts),
    /// because the overlay and the sensor UI key on those names.
    /// </summary>
    [TestClass]
    public class SimulatedCpuLayoutTest
    {
        [TestMethod]
        public void IntelLpeCpu_PublishesPEAndLpeCores()
        {
            List<string> names = GetCoreClockNames(SimulatedCpuKind.IntelLpeCpu, out string cpuName);

            StringAssert.Contains(cpuName, "Core Ultra 9 285H");
            CollectionAssert.AreEqual(ExpectedCoreNames(("P", 6), ("E", 8), ("LPE", 2)), names);
        }

        [TestMethod]
        public void AmdHybridCpu_PublishesPAndDenseCores()
        {
            List<string> names = GetCoreClockNames(SimulatedCpuKind.AmdHybridCpu, out string cpuName);

            StringAssert.Contains(cpuName, "Ryzen AI 9 HX 370");
            CollectionAssert.AreEqual(ExpectedCoreNames(("P", 4), ("D", 8)), names);
        }

        [TestMethod]
        public void IntelCpu_KeepsPAndECores()
        {
            List<string> names = GetCoreClockNames(SimulatedCpuKind.IntelCpu, out _);

            CollectionAssert.AreEqual(ExpectedCoreNames(("P", 8), ("E", 16)), names);
        }

        [TestMethod]
        public void Amd17Cpu_KeepsPlainCoreNames()
        {
            List<string> names = GetCoreClockNames(SimulatedCpuKind.Amd17Cpu, out _);

            CollectionAssert.AreEqual(ExpectedCoreNames(("", 16)), names);
        }

        [TestMethod]
        public void HybridLayouts_UseTheSameCoreNameAcrossSensorTypes()
        {
            // Every per-core sensor family has to carry the same suffix, otherwise the UI shows one core under two names.
            WithSimulatedCpu(SimulatedCpuKind.IntelLpeCpu, cpu =>
            {
                List<ISensor> core15 = cpu.Sensors.Where(s => s.Name.StartsWith("Core #15 ", StringComparison.Ordinal)).ToList();

                CollectionAssert.AreEquivalent(
                    new[] { SensorType.Clock, SensorType.Clock, SensorType.Load, SensorType.Temperature },
                    core15.Select(s => s.SensorType).ToArray());
                Assert.IsTrue(
                    core15.All(s => s.Name == "Core #15 LPE" || s.Name == "Core #15 LPE (Effective)"),
                    string.Join(", ", core15.Select(s => s.Name)));
            });

            WithSimulatedCpu(SimulatedCpuKind.AmdHybridCpu, cpu =>
            {
                List<ISensor> core5 = cpu.Sensors.Where(s => s.Name.StartsWith("Core #5 ", StringComparison.Ordinal)).ToList();

                CollectionAssert.AreEquivalent(
                    new[] { SensorType.Clock, SensorType.Load },
                    core5.Select(s => s.SensorType).ToArray());
                Assert.IsTrue(core5.All(s => s.Name == "Core #5 D"), string.Join(", ", core5.Select(s => s.Name)));
            });
        }

        [TestMethod]
        public void SimulatedCpuGroup_ReportsTheCoreLayout()
        {
            var computer = new Computer(new SimulationConfiguration { Mode = SimulationMode.Enabled, Cpu = SimulatedCpuKind.IntelLpeCpu })
            {
                IsCpuEnabled = true
            };

            try
            {
                computer.Open();
                string report = computer.GetReport();

                StringAssert.Contains(report, "Layout: 6 P + 8 E + 2 LPE");
            }
            finally
            {
                computer.Close();
            }
        }

        private static void WithSimulatedCpu(SimulatedCpuKind kind, Action<IHardware> assert)
        {
            var computer = new Computer(new SimulationConfiguration { Mode = SimulationMode.Enabled, Cpu = kind })
            {
                IsCpuEnabled = true
            };

            try
            {
                computer.Open();
                IHardware cpu = computer.Hardware.Single(h => h.HardwareType == HardwareType.Cpu);
                assert(cpu);
            }
            finally
            {
                computer.Close();
            }
        }

        private static List<string> GetCoreClockNames(SimulatedCpuKind kind, out string cpuName)
        {
            string name = null;
            List<string> names = null;

            WithSimulatedCpu(kind, cpu =>
            {
                name = cpu.Name;
                names = cpu.Sensors
                    .Where(s => s.SensorType == SensorType.Clock
                        && s.Name.StartsWith("Core #", StringComparison.Ordinal)
                        && !s.Name.EndsWith("(Effective)", StringComparison.Ordinal))
                    .OrderBy(s => s.Index)
                    .Select(s => s.Name)
                    .ToList();
            });

            cpuName = name;
            return names;
        }

        private static List<string> ExpectedCoreNames(params (string Label, int Count)[] layout)
        {
            var names = new List<string>();

            foreach ((string label, int count) in layout)
            {
                for (int i = 0; i < count; i++)
                {
                    int number = names.Count + 1;
                    names.Add(label.Length == 0 ? $"Core #{number}" : $"Core #{number} {label}");
                }
            }

            return names;
        }
    }
}
