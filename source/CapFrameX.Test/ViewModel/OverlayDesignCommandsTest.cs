using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hardware.Controller;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignCommandsTest
    {
        [TestMethod]
        public void BrowsingDesignsDoesNotActivateUntilRequestedAndPreservesRendererVisibility()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            var model = Create(designs, configuration);
            model.SelectedDesign = designs.Profiles[1];
            Assert.IsNull(designs.ActiveProfileId);
            model.UseDesignCommand.Execute();
            Assert.AreEqual("second", designs.ActiveProfileId);
            Assert.IsTrue(model.HasActiveDesign);
            Assert.IsTrue(configuration.Object.EnableHookOverlay);
            Assert.IsFalse(configuration.Object.EnableHookFreeOverlay);
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            model.UseClassicOverlayCommand.Execute();
            Assert.IsFalse(model.HasActiveDesign);
            Assert.AreEqual(2, model.SavedDesigns.Count, "Returning to classic must retain the saved designs.");
        }

        [TestMethod]
        public void RefreshAfterDeletedSelectionSelectsAnExistingDesign()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            model.SelectedDesign = designs.Profiles[1];
            designs.Profiles = new[] { designs.Profiles[0] };
            model.RefreshDesignsCommand.Execute();
            Assert.AreEqual("first", model.SelectedDesign.Id);
            designs.Profiles = Array.Empty<OverlayDesignInfo>();
            model.RefreshDesignsCommand.Execute();
            Assert.IsNull(model.SelectedDesign);
            Assert.IsFalse(model.UseDesignCommand.CanExecute());
        }

        [TestMethod]
        public void FailedActivationIsVisibleWithoutChangingActiveDesign()
        {
            var designs = new DesignService { RefuseActivation = true };
            var model = Create(designs, Configuration());
            model.UseDesignCommand.Execute();
            Assert.IsFalse(model.HasActiveDesign);
            StringAssert.Contains(model.DesignStatus, "Unavailable design");
        }

        [TestMethod]
        public void NewerProfileLibraryShowsErrorWithoutChangingTheActiveDesign()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            model.UseDesignCommand.Execute();
            Assert.AreEqual("first", designs.ActiveProfileId);
            model.SelectedDesign = designs.Profiles[1];
            designs.ActivationError = new NotSupportedException("This profile library was saved by a newer version of CapFrameX.");
            model.UseDesignCommand.Execute();
            Assert.AreEqual("first", designs.ActiveProfileId);
            Assert.AreEqual("second", model.SelectedDesign.Id);
            StringAssert.Contains(model.DesignStatus, "newer version");
        }

        [TestMethod]
        public void OpenDesignerUsesApplicationEventWithoutStartingAnotherProcess()
        {
            var events = new EventAggregator();
            int requests = 0;
            events.GetEvent<PubSubEvent<ViewMessages.OpenOverlayDesigner>>().Subscribe(_ => requests++);
            var model = Create(new DesignService(), Configuration(), events);
            model.OpenDesignerCommand.Execute();
            Assert.AreEqual(1, requests);
        }

        [TestMethod]
        public void RtssDisablesActivationButKeepsBrowsingAndEditingAvailable()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            configuration.Object.EnableHookOverlay = false;
            var model = Create(designs, configuration, new EventAggregator());
            Assert.IsFalse(model.CanUseDesign);
            Assert.IsFalse(model.UseDesignCommand.CanExecute());
            model.UseDesignCommand.Execute();
            Assert.IsNull(designs.ActiveProfileId, "Programmatic command execution must also respect RTSS.");
            Assert.AreEqual(2, model.SavedDesigns.Count);
            Assert.IsTrue(model.OpenDesignerCommand.CanExecute());

            model.OverlayModeHookFree = true;
            Assert.IsTrue(model.CanUseDesign);
            Assert.IsTrue(model.UseDesignCommand.CanExecute());
            Assert.IsNull(designs.ActiveProfileId, "Selecting a native renderer must not activate a design implicitly.");
        }

        [TestMethod]
        public void ServiceEligibilityChangesRefreshTheActivationCommand()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            int changes = 0;
            model.UseDesignCommand.CanExecuteChanged += (_, _) => changes++;
            designs.CanActivate = false;
            designs.RefreshProfiles();
            Assert.IsFalse(model.UseDesignCommand.CanExecute());
            Assert.IsTrue(changes > 0);
        }

        [TestMethod]
        public void RtssHidesTheTileTabAndReturnsItsSelectionToTheRowEditor()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            var model = Create(designs, configuration);
            model.SelectedOverlayTabIndex = 1;
            Assert.IsTrue(model.IsTileOverlayAvailable);
            model.OverlayModeHookFree = true;
            Assert.AreEqual(1, model.SelectedOverlayTabIndex);

            model.OverlayModeRtss = true;
            Assert.IsFalse(model.IsTileOverlayAvailable);
            Assert.AreEqual(0, model.SelectedOverlayTabIndex);
            model.SelectedOverlayTabIndex = 1;
            Assert.AreEqual(0, model.SelectedOverlayTabIndex, "A hidden tab cannot be selected programmatically.");
            Assert.IsTrue(model.OverlayModeRtss, "Selecting a tab must never change the renderer.");

            model.OverlayModeHook = true;
            Assert.IsTrue(model.IsTileOverlayAvailable);
            Assert.AreEqual(0, model.SelectedOverlayTabIndex, "Returning to a native renderer must leave the row editor selected.");
            Assert.IsNull(designs.ActiveProfileId);
            model.SelectedOverlayTabIndex = 2;
            model.OverlayModeRtss = true;
            Assert.AreEqual(2, model.SelectedOverlayTabIndex, "The always-visible OSD options must remain selected.");
        }

        [TestMethod]
        public void ProductionTabBindingsCollapseTileContentAndKeepRtssInTheRowOptions()
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "CapFrameX.sln"))) root = root.Parent;
            Assert.IsNotNull(root);
            var document = XDocument.Load(Path.Combine(root.FullName, "source", "CapFrameX.View", "OverlayView.xaml"));
            var source = document.Descendants(wpf + "TabControl")
                .Single(element => (string)element.Attribute(xaml + "Name") == "OverlayTabs");
            var sourceTile = source.Elements(wpf + "TabItem")
                .Single(element => (string)element.Attribute(xaml + "Name") == "TileOverlayTab");
            Assert.IsFalse(sourceTile.Descendants(wpf + "RadioButton").Any(element =>
                ((string)element.Attribute("IsChecked") ?? string.Empty).Contains("OverlayModeRtss")),
                "RTSS must not be offered inside the tile overlay tab.");
            Assert.IsTrue(document.Descendants(wpf + "RadioButton").Any(element =>
                ((string)element.Attribute("IsChecked") ?? string.Empty).Contains("OverlayModeRtss")),
                "The existing renderer options must retain RTSS.");

            // Exercise the actual production tab bindings in WPF without instantiating the
            // unrelated app services and hundreds of entry-editor controls in OverlayView.
            var fragment = new XElement(wpf + "TabControl", new XAttribute("xmlns", wpf.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "x", xaml.NamespaceName),
                new XAttribute("SelectedIndex", source.Attribute("SelectedIndex").Value),
                new XElement(wpf + "TabControl.Resources", new XElement(wpf + "BooleanToVisibilityConverter",
                    new XAttribute(xaml + "Key", "TrueToVisibleConverter"))));
            foreach (var sourceTab in source.Elements(wpf + "TabItem"))
            {
                var tab = new XElement(wpf + "TabItem", new XAttribute("Header", sourceTab.Attribute("Header").Value
                    .Replace("{loc:Tr ", string.Empty).TrimEnd('}')),
                    new XElement(wpf + "TextBlock", new XAttribute("Text", "Overlay content")));
                if (sourceTab.Attribute(xaml + "Name") is XAttribute name) tab.Add(new XAttribute(name));
                if (sourceTab.Attribute("Visibility") is XAttribute visibility) tab.Add(new XAttribute(visibility));
                fragment.Add(tab);
            }
            var tabs = (TabControl)XamlReader.Parse(fragment.ToString());
            var model = Create(new DesignService(), Configuration());
            tabs.DataContext = model;
            tabs.Measure(new Size(640, 180));
            tabs.Arrange(new Rect(0, 0, 640, 180));
            tabs.UpdateLayout();
            var rowTab = (TabItem)tabs.Items[0];
            var tileTab = (TabItem)tabs.Items[1];
            Assert.AreEqual(Visibility.Visible, tileTab.Visibility);
            tabs.SelectedItem = tileTab;
            Assert.AreEqual(1, model.SelectedOverlayTabIndex);

            model.OverlayModeRtss = true;
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.AreEqual(Visibility.Collapsed, tileTab.Visibility);
            Assert.AreSame(rowTab, tabs.SelectedItem);
            Assert.IsFalse(tileTab.IsSelected);

            model.OverlayModeHookFree = true;
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.AreEqual(Visibility.Visible, tileTab.Visibility);
            Assert.AreSame(rowTab, tabs.SelectedItem);
            Assert.IsFalse(model.HasActiveDesign);
            tabs.DataContext = null;
        }

        [TestMethod]
        public void ContextActionsAddressTheClickedProfileInsteadOfThePreviousSelection()
        {
            var events = new EventAggregator();
            ViewMessages.OpenOverlayDesigner opened = null;
            events.GetEvent<PubSubEvent<ViewMessages.OpenOverlayDesigner>>().Subscribe(message => opened = message);
            var designs = new DesignService();
            var model = Create(designs, Configuration(), events);
            var first = designs.Profiles[0];
            var second = designs.Profiles[1];

            model.SelectedDesign = first;
            model.EditSavedDesignCommand.Execute(second);
            Assert.AreEqual(second.Id, opened.ProfileId);
            Assert.AreEqual(second.Id, model.SelectedDesign.Id);
            Assert.IsNull(designs.ActiveProfileId, "Opening an editor must not activate the profile.");

            model.SelectedDesign = second;
            model.ShowSavedDesignCommand.Execute(first);
            Assert.AreEqual(first.Id, designs.ActiveProfileId);
            Assert.AreEqual(first.Id, model.SelectedDesign.Id);
        }

        [TestMethod]
        public void DeletingRequiresConfirmationAndCancellationDoesNotChangeProfiles()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            var target = designs.Profiles[1];
            model.DeleteSavedDesignCommand.Execute(target);
            Assert.IsTrue(model.IsDeleteDesignDialogOpen);
            StringAssert.Contains(model.DeleteDesignMessage, target.Name);
            Assert.AreEqual(2, designs.Profiles.Count);
            Assert.AreEqual(0, designs.DeletedIds.Count);

            model.CancelDeleteDesignCommand.Execute();
            Assert.IsFalse(model.IsDeleteDesignDialogOpen);
            Assert.IsFalse(model.ConfirmDeleteDesignCommand.CanExecute());
            model.ConfirmDeleteDesignCommand.Execute();
            Assert.AreEqual(0, designs.DeletedIds.Count);
            Assert.AreEqual(2, designs.Profiles.Count);
        }

        [TestMethod]
        public void ConfirmationKeepsItsOriginalTargetAfterSelectionAndLibraryObjectsChange()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            designs.Activate("second");
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);
            Assert.IsTrue(model.IsDeletingActiveDesign);
            designs.Profiles = new[] { new OverlayDesignInfo("first", "First design"), new OverlayDesignInfo("second", "Second design") };
            designs.RefreshProfiles();
            model.SelectedDesign = designs.Profiles[0];
            model.ConfirmDeleteDesignCommand.Execute();
            CollectionAssert.AreEqual(new[] { "second" }, designs.DeletedIds);
            Assert.IsFalse(model.IsDeleteDesignDialogOpen);
            Assert.IsFalse(model.HasActiveDesign);
            Assert.AreEqual("first", model.SelectedDesign.Id);
        }

        [TestMethod]
        public void LastProfileAndStaleContextTargetsCannotBeDeletedOrActivated()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration(), new EventAggregator());
            var removed = designs.Profiles[1];
            designs.Profiles = new[] { designs.Profiles[0] };
            designs.RefreshProfiles();
            Assert.IsFalse(model.DeleteSavedDesignCommand.CanExecute(designs.Profiles[0]));
            Assert.IsFalse(model.DeleteSavedDesignCommand.CanExecute(removed));
            Assert.IsFalse(model.ShowSavedDesignCommand.CanExecute(removed));
            Assert.IsFalse(model.EditSavedDesignCommand.CanExecute(removed));
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[0]);
            model.ShowSavedDesignCommand.Execute(removed);
            Assert.IsFalse(model.IsDeleteDesignDialogOpen);
            Assert.IsNull(designs.ActiveProfileId);
        }

        [TestMethod]
        public void FailedConfirmedDeletionReportsTheErrorAndRetainsTheLibrary()
        {
            var designs = new DesignService { DeleteError = new IOException("Library is locked") };
            var model = Create(designs, Configuration());
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);
            model.ConfirmDeleteDesignCommand.Execute();
            Assert.IsFalse(model.IsDeleteDesignDialogOpen);
            Assert.AreEqual(2, designs.Profiles.Count);
            StringAssert.Contains(model.DesignStatus, "Library is locked");
        }

        [TestMethod]
        public void LeavingTheTileTabCancelsPendingDeletionAndRtssBlocksContextActivation()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            model.SelectedOverlayTabIndex = 1;
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);
            model.OverlayModeRtss = true;
            Assert.IsFalse(model.IsDeleteDesignDialogOpen);
            Assert.IsFalse(model.ShowSavedDesignCommand.CanExecute(designs.Profiles[1]));
            Assert.IsFalse(model.DeleteSavedDesignCommand.CanExecute(designs.Profiles[1]));
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);
            Assert.IsFalse(model.IsDeleteDesignDialogOpen);
            model.ConfirmDeleteDesignCommand.Execute();
            Assert.AreEqual(0, designs.DeletedIds.Count);
        }

        private static Mock<IAppConfiguration> Configuration()
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            config.Object.EnableHookOverlay = true;
            config.SetupGet(c => c.OnValueChanged).Returns(Observable.Never<(string key, object value)>());
            return config;
        }

        private static OverlayViewModel Create(DesignService designs, Mock<IAppConfiguration> config,
            IEventAggregator events = null)
        {
            var provider = new Mock<IOverlayEntryProvider>();
            provider.Setup(p => p.GetOverlayEntries(false)).ReturnsAsync(Array.Empty<IOverlayEntry>());
            provider.Setup(p => p.SwitchConfigurationTo(It.IsAny<int>())).Returns(Task.CompletedTask);
            return new OverlayViewModel(Mock.Of<IOverlayService>(), provider.Object, config.Object,
                Mock.Of<IPathService>(), Mock.Of<ISensorService>(), Mock.Of<IRTSSService>(),
                Mock.Of<IThreadAffinityController>(), Mock.Of<IOnlineMetricService>(),
                new OverlayTemplateService(Mock.Of<ISensorService>()), null, null, designs, events);
        }

        private sealed class DesignService : IOverlayDesignService
        {
            public IReadOnlyList<OverlayDesignInfo> Profiles { get; set; } = new[]
            {
                new OverlayDesignInfo("first", "First design"), new OverlayDesignInfo("second", "Second design")
            };
            public string ActiveProfileId { get; private set; }
            public bool IsEnabled => ActiveProfileId != null;
            public bool CanActivate { get; set; } = true;
            public string Status => IsEnabled ? "Active: " + ActiveProfileId : "Classic overlay";
            public bool RefuseActivation { get; set; }
            public Exception ActivationError { get; set; }
            public Exception DeleteError { get; set; }
            public List<string> DeletedIds { get; } = new List<string>();
            public event EventHandler Changed;
            public void RefreshProfiles() => Changed?.Invoke(this, EventArgs.Empty);
            public void Activate(string profileId)
            {
                if (ActivationError != null) throw ActivationError;
                if (RefuseActivation)
                    throw new InvalidOperationException("Unavailable design");
                ActiveProfileId = profileId;
                RefreshProfiles();
            }
            public void Deactivate()
            {
                ActiveProfileId = null;
                RefreshProfiles();
            }
            public void Delete(string profileId)
            {
                if (DeleteError != null) throw DeleteError;
                DeletedIds.Add(profileId);
                Profiles = Profiles.Where(profile => profile.Id != profileId).ToArray();
                if (ActiveProfileId == profileId) ActiveProfileId = null;
                RefreshProfiles();
            }
        }
    }
}
