using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The Info tab is the startup page, so the portable version's layer registration happens while
    /// it is shown and no navigation refreshes it; the registration message does.
    /// </summary>
    [TestClass]
    public class InfoViewModelVulkanLayerStatusTest
    {
        [TestMethod]
        public void RegistrationMessage_RefreshesTheLayerStatusWithoutTheSensorService()
        {
            // A sensor service that never finishes: the startup path, which also sets the layer
            // status, waits for it, so only the message can replace "Detecting" here.
            var sensorService = new Mock<ISensorService>();
            sensorService.Setup(service => service.SensorServiceCompletionSource).Returns(new TaskCompletionSource<bool>());
            sensorService.Setup(service => service.SensorSnapshotStream)
                .Returns(Observable.Never<(DateTime, Dictionary<ISensorEntry, float>)>());

            var appConfiguration = new Mock<IAppConfiguration>();
            appConfiguration.Setup(config => config.OnValueChanged).Returns(Observable.Never<(string key, object value)>());

            var eventAggregator = new EventAggregator();
            var viewModel = new InfoViewModel(
                sensorService.Object,
                new Mock<ISensorConfig>().Object,
                new Mock<ISystemInfo>().Object,
                appConfiguration.Object,
                new Mock<IRTSSService>().Object,
                new Mock<IAppVersionProvider>().Object,
                eventAggregator,
                new Mock<ILogger<InfoViewModel>>().Object);

            using (var refreshed = new ManualResetEventSlim())
            {
                viewModel.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(InfoViewModel.VulkanLayerStatus))
                        refreshed.Set();
                };
                var detecting = viewModel.VulkanLayerStatus;

                eventAggregator.GetEvent<PubSubEvent<AppMessages.VulkanLayerRegistrationChanged>>()
                    .Publish(new AppMessages.VulkanLayerRegistrationChanged());

                Assert.IsTrue(refreshed.Wait(TimeSpan.FromSeconds(10)), "The layer status was not read again.");
                Assert.AreNotSame(detecting, viewModel.VulkanLayerStatus);
            }
        }
    }
}
