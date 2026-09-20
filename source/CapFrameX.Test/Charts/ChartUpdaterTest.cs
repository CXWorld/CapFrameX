using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using LiveCharts.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Charts
{
    [TestClass]
    public class ChartUpdaterTest
    {
        [TestMethod]
        public void Run_DoesNotStartDispatcherTimer_WhenChartIsNotVisible()
        {
            Exception testException = null;
            var testThread = new Thread(() =>
            {
                try
                {
                    var chart = new CartesianChart
                    {
                        Visibility = Visibility.Collapsed
                    };

                    var updater = chart.Model.Updater;
                    updater.Run();

                    Assert.IsFalse(updater.IsUpdating);

                    var timerProperty = updater.GetType().GetProperty("Timer");
                    Assert.IsNotNull(timerProperty);

                    var timer = (DispatcherTimer)timerProperty.GetValue(updater);
                    Assert.IsFalse(timer.IsEnabled);

                    chart.IsMocked = true;
                    updater.Run();

                    Assert.IsTrue(updater.IsUpdating);
                    Assert.IsTrue(timer.IsEnabled);

                    chart.IsMocked = false;
                    updater.Run();

                    Assert.IsFalse(updater.IsUpdating);
                    Assert.IsFalse(timer.IsEnabled);
                }
                catch (Exception exception)
                {
                    testException = exception;
                }
            });

            testThread.SetApartmentState(ApartmentState.STA);
            testThread.Start();

            Assert.IsTrue(testThread.Join(TimeSpan.FromSeconds(5)), "The WPF test thread did not finish.");

            if (testException != null)
            {
                ExceptionDispatchInfo.Capture(testException).Throw();
            }
        }
    }
}
