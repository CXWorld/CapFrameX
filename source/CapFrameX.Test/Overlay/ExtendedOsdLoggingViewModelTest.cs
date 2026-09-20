using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CapFrameX.ViewModel;
using CapFrameX.ViewModel.SubModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class ExtendedOsdLoggingViewModelTest
    {
        private string _testDirectory;
        private Dictionary<string, string> _userValues;
        private Dictionary<string, string> _processValues;
        private Action _beforeWrite;
        private Action _notifyEnvironmentChanged;
        private int _writes;

        [TestInitialize]
        public void Initialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(),
                $"CapFrameX.OsdLoggingViewModelTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);
            _userValues = new Dictionary<string, string>();
            _processValues = new Dictionary<string, string>();
            _beforeWrite = () => { };
            _notifyEnvironmentChanged = () => { };
            _writes = 0;
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_testDirectory, true);
        }

        [TestMethod]
        public void NewSettings_DefaultToFalseWithoutWritingDiagnosticFlags()
        {
            var viewModel = new ExtendedOsdLoggingViewModel(CreateController());

            Assert.IsFalse(viewModel.IsEnabled);
            Assert.IsFalse(viewModel.IsUpdating);
            Assert.IsFalse(viewModel.HasError);
            viewModel.IsEnabled = false;
            Assert.AreEqual(0, _writes);
            Assert.AreEqual(0, Directory.GetFiles(_testDirectory).Length);
        }

        [TestMethod]
        public Task SlowEnvironmentNotification_KeepsDispatcherResponsiveAndPreventsOverlappingWrites()
        {
            return RunOnDispatcherAsync(async () =>
            {
                int uiThread = Environment.CurrentManagedThreadId;
                var notificationThreads = new ConcurrentBag<int>();
                var notificationStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var releaseNotification = new ManualResetEventSlim();
                _notifyEnvironmentChanged = () =>
                {
                    notificationStarted.TrySetResult(Environment.CurrentManagedThreadId);
                    if (!releaseNotification.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The test did not release the environment notification.");
                };
                var viewModel = new ExtendedOsdLoggingViewModel(CreateController());
                viewModel.PropertyChanged += (_, _) => notificationThreads.Add(Environment.CurrentManagedThreadId);
                Task completed = WhenUpdateCompletes(viewModel);

                try
                {
                    viewModel.IsEnabled = true;
                    Assert.IsTrue(viewModel.IsEnabled, "The check mark should update immediately.");
                    Assert.IsTrue(viewModel.IsUpdating);
                    Assert.AreNotEqual(uiThread,
                        await notificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)));

                    int heartbeatThread = await Dispatcher.CurrentDispatcher.InvokeAsync(
                        () => Environment.CurrentManagedThreadId, DispatcherPriority.Input).Task;
                    Assert.AreEqual(uiThread, heartbeatThread);
                    Assert.IsFalse(completed.IsCompleted,
                        "WPF must process input while the Windows broadcast is still pending.");

                    viewModel.IsEnabled = false;
                    Assert.IsTrue(viewModel.IsEnabled, "Another update must wait until saving has completed.");
                    Assert.AreEqual(4, _writes);
                }
                finally
                {
                    releaseNotification.Set();
                    await completed.WaitAsync(TimeSpan.FromSeconds(5));
                }

                Assert.IsFalse(viewModel.IsUpdating);
                Assert.IsFalse(viewModel.HasError);
                foreach (int thread in notificationThreads)
                    Assert.AreEqual(uiThread, thread, "All binding notifications must run on the UI thread.");
            });
        }

        [TestMethod]
        public Task FailedUpdate_RestoresCheckboxAndAllowsRetry()
        {
            return RunOnDispatcherAsync(async () =>
            {
                bool failNextWrite = true;
                _beforeWrite = () =>
                {
                    if (failNextWrite)
                    {
                        failNextWrite = false;
                        throw new IOException("Simulated settings write failure");
                    }
                };
                var viewModel = new ExtendedOsdLoggingViewModel(CreateController());
                Task failedUpdate = WhenUpdateCompletes(viewModel);
                viewModel.IsEnabled = true;
                await failedUpdate.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.IsFalse(viewModel.IsEnabled);
                Assert.IsFalse(viewModel.IsUpdating);
                Assert.IsTrue(viewModel.HasError);
                StringAssert.Contains(viewModel.Error, "Simulated settings write failure");
                Assert.AreEqual(0, _userValues.Count);

                Task retry = WhenUpdateCompletes(viewModel);
                viewModel.IsEnabled = true;
                await retry.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.IsTrue(viewModel.IsEnabled);
                Assert.IsFalse(viewModel.HasError);
                Assert.IsTrue(CreateController().IsEnabled());
            });
        }

        [TestMethod]
        public Task Disable_PersistsFalseAfterRestartWithStaleEnvironment()
        {
            return RunOnDispatcherAsync(async () =>
            {
                CreateController().SetEnabled(true);
                var viewModel = new ExtendedOsdLoggingViewModel(CreateController());
                Assert.IsTrue(viewModel.IsEnabled);

                Task completed = WhenUpdateCompletes(viewModel);
                viewModel.IsEnabled = false;
                await completed.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.IsFalse(viewModel.IsEnabled);
                Assert.IsFalse(viewModel.HasError);

                // A still-running launcher can pass its old values into the restarted process.
                foreach (string name in _userValues.Keys)
                    _processValues[name] = "1";
                var restartedController = CreateController();
                Assert.IsFalse(new ExtendedOsdLoggingViewModel(restartedController).IsEnabled);
                restartedController.ApplyProcessSettings();
                foreach (var pair in _userValues)
                {
                    Assert.AreEqual("0", pair.Value);
                    Assert.AreEqual("0", _processValues[pair.Key]);
                }
            });
        }

        private ExtendedOsdLoggingController CreateController()
        {
            return new ExtendedOsdLoggingController(Path.Combine(_testDirectory, "OsdDebug.json"),
                name => _userValues.TryGetValue(name, out string value) ? value : null,
                (name, value) =>
                {
                    _beforeWrite();
                    SetValue(_userValues, name, value);
                    _writes++;
                },
                name => _processValues.TryGetValue(name, out string value) ? value : null,
                (name, value) => SetValue(_processValues, name, value),
                () => _notifyEnvironmentChanged());
        }

        private static void SetValue(Dictionary<string, string> values, string name, string value)
        {
            if (value == null)
                values.Remove(name);
            else
                values[name] = value;
        }

        private static Task WhenUpdateCompletes(ExtendedOsdLoggingViewModel viewModel)
        {
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            PropertyChangedEventHandler handler = null;
            handler = (_, args) =>
            {
                if (args.PropertyName == nameof(viewModel.IsUpdating) && !viewModel.IsUpdating)
                {
                    viewModel.PropertyChanged -= handler;
                    completed.TrySetResult(true);
                }
            };
            viewModel.PropertyChanged += handler;
            return completed.Task;
        }

        private static Task RunOnDispatcherAsync(Func<Task> test)
        {
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await test();
                        completed.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        completed.TrySetException(ex);
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                }));
                Dispatcher.Run();
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
}
