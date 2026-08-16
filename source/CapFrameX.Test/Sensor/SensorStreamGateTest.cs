using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Sensor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SensorStreamGateTest
    {
        [TestMethod]
        public void WhileActive_Inactive_DoesNotSubscribeToHardwareSource()
        {
            var activity = new Subject<bool>();
            int subscriptionCount = 0;
            var values = new List<int>();
            var source = Observable.Create<int>(_ =>
            {
                subscriptionCount++;
                return Disposable.Empty;
            });

            using var subscription = SensorStreamGate.WhileActive(
                activity,
                () => source,
                () => -1)
                .Subscribe(values.Add);

            activity.OnNext(false);
            activity.OnNext(false);

            Assert.AreEqual(0, subscriptionCount);
            CollectionAssert.AreEqual(new[] { -1 }, values);
        }

        [TestMethod]
        public void WhileActive_StateChanges_SubscribeAndDisposeHardwareSource()
        {
            var activity = new Subject<bool>();
            var hardwareValues = new Subject<int>();
            int subscriptionCount = 0;
            int disposalCount = 0;
            var values = new List<int>();
            var source = Observable.Create<int>(observer =>
            {
                subscriptionCount++;
                var sourceSubscription = hardwareValues.Subscribe(observer);
                return Disposable.Create(() =>
                {
                    sourceSubscription.Dispose();
                    disposalCount++;
                });
            });

            using var subscription = SensorStreamGate.WhileActive(
                activity,
                () => source,
                () => -1)
                .Subscribe(values.Add);

            activity.OnNext(false);
            activity.OnNext(true);
            hardwareValues.OnNext(42);
            activity.OnNext(false);
            hardwareValues.OnNext(99);
            activity.OnNext(true);
            hardwareValues.OnNext(7);

            Assert.AreEqual(2, subscriptionCount);
            Assert.AreEqual(1, disposalCount);
            CollectionAssert.AreEqual(new[] { -1, 42, -1, 7 }, values);
        }
    }
}
