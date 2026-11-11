using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Cpu;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using HardwareSensor = LibreHardwareMonitor.Hardware.Sensor;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class CoreTypeClockSensorTest
    {
        private ISettings _settings;
        private TestHardware _hardware;
        private int _sourceIndex;

        [TestInitialize]
        public void Initialize()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(item => item.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string name, string defaultValue) => defaultValue);
            _settings = settings.Object;
            _hardware = new TestHardware(_settings);
            _sourceIndex = 0;
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Create_IntelHybrid_SeparatesCoreTypesAndIncludesEverySmtThread(bool effective)
        {
            var sources = new (CpuCoreType, ISensor)[]
            {
                (CpuCoreType.LowPowerEfficiencyCore, Source(1800)),
                (CpuCoreType.PerformanceCore, Source(4300)),
                (CpuCoreType.EfficiencyCore, Source(3500)),
                (CpuCoreType.PerformanceCore, Source(5100)),
                (CpuCoreType.LowPowerEfficiencyCore, Source(2200)),
                (CpuCoreType.EfficiencyCore, Source(3200))
            };
            int index = 40;

            CoreTypeClockSensor[] clocks = CoreTypeClockSensor.Create(
                true, _hardware, _settings, sources, effective, ref index);
            foreach (CoreTypeClockSensor clock in clocks)
                clock.Update();

            string suffix = effective ? " Effective" : "";
            CollectionAssert.AreEqual(
                new[] { "CPU P Max" + suffix, "CPU E Max" + suffix, "CPU LPE Max" + suffix },
                clocks.Select(clock => clock.Sensor.Name).ToArray());
            CollectionAssert.AreEqual(
                new float?[] { 5100, 3500, 2200 },
                clocks.Select(clock => clock.Sensor.Value).ToArray());
            Assert.IsTrue(clocks.All(clock => clock.Sensor.SensorType == SensorType.Clock));
            Assert.IsTrue(clocks.All(clock => !clock.Sensor.IsPresentationDefault));
        }

        [TestMethod]
        public void Create_AmdHybrid_SupportsPerformanceDenseAndLowPowerCores()
        {
            var sources = new (CpuCoreType, ISensor)[]
            {
                (CpuCoreType.LowPowerCore, Source(2400)),
                (CpuCoreType.DenseCore, Source(3400)),
                (CpuCoreType.PerformanceCore, Source(5100)),
                (CpuCoreType.DenseCore, Source(3700))
            };
            int index = 20;

            CoreTypeClockSensor[] clocks = CoreTypeClockSensor.Create(
                true, _hardware, _settings, sources, false, ref index);
            foreach (CoreTypeClockSensor clock in clocks)
                clock.Update();

            CollectionAssert.AreEqual(
                new[] { "CPU P Max", "CPU D Max", "CPU LP Max" },
                clocks.Select(clock => clock.Sensor.Name).ToArray());
            CollectionAssert.AreEqual(
                new float?[] { 5100, 3700, 2400 },
                clocks.Select(clock => clock.Sensor.Value).ToArray());
        }

        [TestMethod]
        public void Create_RawAndEffectiveSensors_HaveDistinctIdentifiersAndStablePresentationOrder()
        {
            var sources = new (CpuCoreType, ISensor)[]
            {
                (CpuCoreType.EfficiencyCore, Source(3500)),
                (CpuCoreType.PerformanceCore, Source(5100))
            };
            int index = 40;

            CoreTypeClockSensor[] raw = CoreTypeClockSensor.Create(
                true, _hardware, _settings, sources, false, ref index);
            CoreTypeClockSensor[] effective = CoreTypeClockSensor.Create(
                true, _hardware, _settings, sources, true, ref index);
            HardwareSensor[] sensors = raw.Concat(effective).Select(clock => clock.Sensor).ToArray();

            Assert.AreEqual(44, index);
            CollectionAssert.AreEqual(new[] { 40, 41, 42, 43 }, sensors.Select(sensor => sensor.Index).ToArray());
            Assert.AreEqual(sensors.Length, sensors.Select(sensor => sensor.Identifier.ToString()).Distinct().Count());
            CollectionAssert.AreEqual(
                new[] { "0_2_1_1_0", "0_2_1_2_0", "0_2_1_1_1", "0_2_1_2_1" },
                sensors.Select(sensor => sensor.PresentationSortKey).ToArray());
        }

        [TestMethod]
        public void Update_UsesCurrentValuesAndClearsMissingSamplesInsteadOfRetainingPeaks()
        {
            HardwareSensor first = Source(5400);
            HardwareSensor second = Source(4900);
            int index = 10;
            CoreTypeClockSensor clock = CoreTypeClockSensor.Create(
                true, _hardware, _settings,
                new (CpuCoreType, ISensor)[]
                {
                    (CpuCoreType.PerformanceCore, first),
                    (CpuCoreType.PerformanceCore, second)
                }, false, ref index).Single();

            clock.Update();
            Assert.AreEqual(5400f, clock.Sensor.Value);

            first.Value = 1200;
            second.Value = 2300;
            clock.Update();
            Assert.AreEqual(5400f, first.Max, "The source retains a historical peak to distinguish it from its current value.");
            Assert.AreEqual(2300f, clock.Sensor.Value);

            first.Value = null;
            second.Value = 0;
            clock.Update();
            Assert.AreEqual(0f, clock.Sensor.Value, "A sleeping core's zero clock is a valid current reading.");

            second.Value = null;
            clock.Update();
            Assert.IsNull(clock.Sensor.Value);
        }

        [TestMethod]
        public void Update_IgnoresNonfiniteReadingsAndReturnsNullWhenNoneAreValid()
        {
            HardwareSensor valid = Source(3400);
            int index = 10;
            CoreTypeClockSensor clock = CoreTypeClockSensor.Create(
                true, _hardware, _settings,
                new (CpuCoreType, ISensor)[]
                {
                    (CpuCoreType.EfficiencyCore, Source(float.NaN)),
                    (CpuCoreType.EfficiencyCore, Source(float.PositiveInfinity)),
                    (CpuCoreType.EfficiencyCore, Source(float.NegativeInfinity)),
                    (CpuCoreType.EfficiencyCore, valid)
                }, false, ref index).Single();

            clock.Update();
            Assert.AreEqual(3400f, clock.Sensor.Value);

            valid.Value = null;
            clock.Update();
            Assert.IsNull(clock.Sensor.Value);
        }

        [TestMethod]
        public void Create_UnsupportedCoreTypesAndMissingSensors_AreIgnored()
        {
            int index = 10;
            CoreTypeClockSensor[] clocks = CoreTypeClockSensor.Create(
                true, _hardware, _settings,
                new (CpuCoreType, ISensor)[]
                {
                    (CpuCoreType.Standard, Source(6000)),
                    ((CpuCoreType)99, Source(6000)),
                    (CpuCoreType.EfficiencyCore, null),
                    (CpuCoreType.PerformanceCore, Source(4500))
                }, false, ref index);

            Assert.AreEqual(1, clocks.Length);
            clocks[0].Update();
            Assert.AreEqual("CPU P Max", clocks[0].Sensor.Name);
            Assert.AreEqual(4500f, clocks[0].Sensor.Value);
            Assert.AreEqual(11, index);
        }

        [TestMethod]
        public void Create_NonhybridOrMissingSources_DoesNotAllocateSensorIndices()
        {
            int index = 10;
            var sources = new (CpuCoreType, ISensor)[]
            {
                (CpuCoreType.PerformanceCore, Source(4500))
            };

            Assert.AreEqual(0, CoreTypeClockSensor.Create(
                false, _hardware, _settings, sources, false, ref index).Length);
            Assert.AreEqual(0, CoreTypeClockSensor.Create(
                true, _hardware, _settings, Array.Empty<(CpuCoreType, ISensor)>(), false, ref index).Length);
            Assert.AreEqual(0, CoreTypeClockSensor.Create(
                true, _hardware, _settings, null, false, ref index).Length);
            Assert.AreEqual(10, index);
        }

        [TestMethod]
        public void IntelPolling_EffectiveTypeMaximumAloneEnablesSamplingAndRegistersAllSensors()
        {
            var sensorConfig = new Mock<ISensorConfig>();
            var cpu = (IntelCpu)RuntimeHelpers.GetUninitializedObject(typeof(IntelCpu));
            HardwareSensor average = Source(null);
            HardwareSensor maximum = Source(null);
            HardwareSensor thread = Source(null);
            int index = 40;
            CoreTypeClockSensor[] clocks = CoreTypeClockSensor.Create(
                true, _hardware, _settings,
                new (CpuCoreType, ISensor)[]
                {
                    (CpuCoreType.PerformanceCore, Source(null)),
                    (CpuCoreType.EfficiencyCore, Source(null))
                }, true, ref index);

            SetField(cpu, "_sensorConfig", sensorConfig.Object);
            SetField(cpu, "_coreEffectiveAvg", average);
            SetField(cpu, "_coreEffectiveMax", maximum);
            SetField(cpu, "_coreTypeMaxEffectiveClocks", clocks);
            SetField(cpu, "_threadEffectiveClocks", new[] { new[] { thread } });

            AssertPollingDemand(
                () => InvokePolling(cpu, "ShouldEvaluateEffectiveClocks"),
                sensorConfig, average, clocks[0].Sensor,
                new ISensor[] { average, maximum, clocks[0].Sensor, clocks[1].Sensor, thread });
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AmdPolling_TypeMaximumAloneEnablesSamplingAndRegistersAllSensors(bool effective)
        {
            var sensorConfig = new Mock<ISensorConfig>();
            var cpu = (Amd17Cpu)RuntimeHelpers.GetUninitializedObject(typeof(Amd17Cpu));
            Type processorType = typeof(Amd17Cpu).GetNestedType("Processor", BindingFlags.NonPublic);
            Assert.IsNotNull(processorType);
            object processor = RuntimeHelpers.GetUninitializedObject(processorType);
            HardwareSensor average = Source(null);
            HardwareSensor maximum = Source(null);
            int index = 40;
            CoreTypeClockSensor[] clocks = CoreTypeClockSensor.Create(
                true, _hardware, _settings,
                new (CpuCoreType, ISensor)[]
                {
                    (CpuCoreType.PerformanceCore, Source(null)),
                    (CpuCoreType.DenseCore, Source(null))
                }, effective, ref index);

            SetField(cpu, "_sensorConfig", sensorConfig.Object);
            SetField(processor, "_cpu", cpu);
            SetField(processor, effective ? "_avgClockEffcetive" : "_avgClock", average);
            SetField(processor, effective ? "_maxEffectiveClock" : "_maxClock", maximum);
            SetField(processor, effective ? "_coreTypeMaxEffectiveClocks" : "_coreTypeMaxClocks", clocks);
            Type nodesType = processorType.GetProperty("Nodes").PropertyType;
            SetField(processor, "<Nodes>k__BackingField", Activator.CreateInstance(nodesType));

            AssertPollingDemand(
                () => InvokePolling(processor, effective ? "ShouldEvaluateEffectiveClocks" : "ShouldEvaluateCoreClocks"),
                sensorConfig, average, clocks[0].Sensor,
                new ISensor[] { average, maximum, clocks[0].Sensor, clocks[1].Sensor });
        }

        private static void AssertPollingDemand(
            Func<bool> shouldPoll,
            Mock<ISensorConfig> sensorConfig,
            ISensor earlierSensor,
            ISensor typeMaximum,
            ISensor[] expectedSensors)
        {
            string selectedIdentifier = null;
            var evaluatedIdentifiers = new List<string>();
            sensorConfig.Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns((string identifier) =>
                {
                    evaluatedIdentifiers.Add(identifier);
                    return identifier == selectedIdentifier;
                });

            Assert.IsFalse(shouldPoll(), "No selected clock sensor should leave polling disabled.");

            foreach (ISensor selected in new[] { typeMaximum, earlierSensor })
            {
                evaluatedIdentifiers.Clear();
                selectedIdentifier = selected.Identifier.ToString();

                Assert.IsTrue(shouldPoll(), "Selecting only this sensor must enable polling: " + selectedIdentifier);
                CollectionAssert.AreEquivalent(
                    expectedSensors.Select(sensor => sensor.Identifier.ToString()).ToArray(),
                    evaluatedIdentifiers,
                    "Every identifier must be registered even after an earlier sensor requests polling.");
            }
        }

        private static bool InvokePolling(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing polling method: " + name);
            return (bool)method.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
            }

            throw new InvalidOperationException("Missing field: " + name);
        }

        private HardwareSensor Source(float? value)
        {
            return new HardwareSensor("Core", _sourceIndex++, false, SensorType.Clock,
                _hardware, null, _settings, disableHistory: false) { Value = value };
        }

        private sealed class TestHardware : LibreHardwareMonitor.Hardware.Hardware
        {
            public TestHardware(ISettings settings)
                : base("Core Type Clock Test", new Identifier("core-type-clock-test"), settings)
            { }

            public override HardwareType HardwareType => HardwareType.Cpu;

            public override void Update()
            { }
        }
    }
}
