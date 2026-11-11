using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hardware.Controller;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Test.Mocks;
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
        public void ProductionProfileListSelectsPassivelyAndShowButtonAppliesItsCurrentTarget()
        {
            var designs = new DesignService();
            designs.Activate("first");
            var model = Create(designs, Configuration());
            var controls = CreateProfileControls(model);
            Assert.AreSame(designs.Profiles[0], controls.Profiles.SelectedItem);
            Assert.AreEqual(1, designs.ActivationCalls);

            controls.Profiles.SetCurrentValue(Selector.SelectedItemProperty, designs.Profiles[1]);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));

            Assert.AreEqual("first", designs.ActiveProfileId, "Selecting a list item must remain passive.");
            Assert.AreEqual(1, designs.ActivationCalls);
            Assert.AreSame(designs.Profiles[1], controls.Show.CommandParameter);
            Assert.IsTrue(controls.Show.IsEnabled);
            controls.Show.Command.Execute(controls.Show.CommandParameter);
            Assert.AreEqual("second", designs.ActiveProfileId);
            Assert.AreEqual(2, designs.ActivationCalls);

            Assert.IsTrue(controls.Show.IsEnabled, "The active profile may be explicitly shown again.");
            controls.Show.Command.Execute(controls.Show.CommandParameter);
            Assert.AreEqual(3, designs.ActivationCalls);

            controls.Profiles.SetCurrentValue(Selector.SelectedItemProperty, designs.Profiles[0]);
            Assert.AreSame(designs.Profiles[0], controls.Show.CommandParameter);
            controls.Show.Command.Execute(controls.Show.CommandParameter);
            Assert.AreEqual("first", designs.ActiveProfileId, "The button must use the current list selection.");
            Assert.AreEqual(4, designs.ActivationCalls);
            controls.View.DataContext = null;
        }

        [TestMethod]
        public void ShownDesignFollowsTheActiveProfileAndRendererNotTheListSelection()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            Assert.IsNull(model.ShownDesignId, "The row overlay shows no tile profile.");

            var raised = new List<string>();
            model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            designs.Activate("first");
            model.SelectedDesign = designs.Profiles[1];
            Assert.AreEqual("first", model.ShownDesignId, "Browsing the list does not change what the overlay shows.");
            CollectionAssert.Contains(raised, nameof(OverlayViewModel.ShownDesignId));

            model.OverlayModeHookFree = true;
            Assert.AreEqual("first", model.ShownDesignId);
            // RTSS draws the row overlay, whatever tile profile is active.
            model.OverlayModeRtss = true;
            Assert.IsNull(model.ShownDesignId);

            model.OverlayModeHook = true;
            Assert.AreEqual("first", model.ShownDesignId);
            designs.Deactivate();
            Assert.IsNull(model.ShownDesignId);
        }

        [TestMethod]
        public void ProfileListMarksTheShownProfileWithItsBadge()
        {
            var designs = new DesignService();
            designs.Activate("second");
            var model = Create(designs, Configuration());
            var (view, list) = CreateProfileList(model);
            Visibility Badge(int index)
            {
                var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
                var presenter = FindChild<ContentPresenter>(container);
                return ((FrameworkElement)list.ItemTemplate.FindName("ShownBadge", presenter)).Visibility;
            }
            Assert.AreEqual(Visibility.Collapsed, Badge(0));
            Assert.AreEqual(Visibility.Visible, Badge(1));

            list.SetCurrentValue(Selector.SelectedItemProperty, designs.Profiles[0]);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.AreEqual(Visibility.Collapsed, Badge(0), "The selection is not what the overlay shows.");
            Assert.AreEqual(Visibility.Visible, Badge(1));

            designs.Activate("first");
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.AreEqual(Visibility.Visible, Badge(0));
            Assert.AreEqual(Visibility.Collapsed, Badge(1));
            designs.Deactivate();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.AreEqual(Visibility.Collapsed, Badge(0), "The row overlay has no profile badge.");
            view.DataContext = null;
        }

        [TestMethod]
        public void ProfileListBrowsingRefreshAndContextTargetsDoNotActivateProfiles()
        {
            var designs = new DesignService();
            designs.Activate("first");
            var events = new EventAggregator();
            ViewMessages.OpenOverlayDesigner opened = null;
            events.GetEvent<PubSubEvent<ViewMessages.OpenOverlayDesigner>>().Subscribe(message => opened = message);
            var model = Create(designs, Configuration(), events);
            var controls = CreateProfileControls(model);

            controls.Profiles.SetCurrentValue(Selector.SelectedItemProperty, designs.Profiles[1]);
            model.EditSavedDesignCommand.Execute(designs.Profiles[1]);
            Assert.AreEqual("second", opened.ProfileId);
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);
            Assert.IsTrue(model.IsDeleteDesignDialogOpen);
            model.CancelDeleteDesignCommand.Execute();

            designs.Profiles = new[]
            {
                new OverlayDesignInfo("first", "Renamed first design"),
                new OverlayDesignInfo("second", "Renamed second design")
            };
            model.RefreshDesignsCommand.Execute();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));

            Assert.AreEqual("second", model.SelectedDesign.Id, "Refreshing must retain the browsed selection.");
            Assert.AreSame(designs.Profiles[1], controls.Profiles.SelectedItem);
            Assert.AreSame(designs.Profiles[1], controls.Show.CommandParameter);
            Assert.AreEqual("first", designs.ActiveProfileId);
            Assert.AreEqual(1, designs.ActivationCalls, "Browsing, context targets and library refresh must never apply a profile.");
            controls.View.DataContext = null;
        }

        [TestMethod]
        public void ShowSelectedProfileButtonRejectsMissingUnavailableAndModalTargets()
        {
            var designs = new DesignService();
            designs.Activate("first");
            var model = Create(designs, Configuration());
            var controls = CreateProfileControls(model);
            void AssertBlocked()
            {
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
                Assert.IsFalse(controls.Show.IsEnabled);
                Assert.IsFalse(controls.Show.Command.CanExecute(controls.Show.CommandParameter));
                controls.Show.Command.Execute(controls.Show.CommandParameter);
                Assert.AreEqual(1, designs.ActivationCalls);
                Assert.AreEqual("first", designs.ActiveProfileId);
            }

            controls.Profiles.SetCurrentValue(Selector.SelectedItemProperty, null);
            Assert.IsNull(controls.Show.CommandParameter);
            AssertBlocked();

            var stale = new OverlayDesignInfo("removed", "Removed design");
            Assert.IsFalse(controls.Show.Command.CanExecute(stale));
            controls.Show.Command.Execute(stale);
            Assert.AreEqual(1, designs.ActivationCalls);

            controls.Profiles.SetCurrentValue(Selector.SelectedItemProperty, designs.Profiles[1]);
            designs.CanActivate = false;
            designs.RefreshProfiles();
            AssertBlocked();

            designs.CanActivate = true;
            designs.RefreshProfiles();
            Assert.IsTrue(controls.Show.IsEnabled);
            model.OverlayModeRtss = true;
            AssertBlocked();

            model.OverlayModeHook = true;
            Assert.IsTrue(controls.Show.IsEnabled, "Returning to a compatible renderer must enable the button again.");
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);
            AssertBlocked();
            Assert.IsTrue(model.IsDeleteDesignDialogOpen);
            model.CancelDeleteDesignCommand.Execute();
            Assert.IsTrue(controls.Show.IsEnabled);
            controls.View.DataContext = null;
        }

        [TestMethod]
        public void FailedShowSelectedProfileKeepsTheActiveProfileAndAllowsRetryingTheSelection()
        {
            var designs = new DesignService();
            designs.Activate("first");
            var model = Create(designs, Configuration());
            var controls = CreateProfileControls(model);
            designs.ActivationError = new IOException("Profile library is locked");

            controls.Profiles.SetCurrentValue(Selector.SelectedItemProperty, designs.Profiles[1]);
            controls.Show.Command.Execute(controls.Show.CommandParameter);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));

            Assert.AreEqual("first", designs.ActiveProfileId);
            Assert.AreSame(designs.Profiles[1], controls.Profiles.SelectedItem);
            Assert.AreSame(designs.Profiles[1], controls.Show.CommandParameter);
            Assert.AreEqual(2, designs.ActivationCalls);
            StringAssert.Contains(model.DesignStatus, "Profile library is locked");
            Assert.IsTrue(controls.Show.IsEnabled);

            designs.ActivationError = null;
            controls.Show.Command.Execute(controls.Show.CommandParameter);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.AreEqual("second", designs.ActiveProfileId);
            Assert.AreSame(designs.Profiles[1], controls.Profiles.SelectedItem);
            Assert.AreEqual(3, designs.ActivationCalls);
            Assert.AreEqual(designs.Status, model.DesignStatus);
            controls.View.DataContext = null;
        }

        [TestMethod]
        public void ModeToggleUsesTheRememberedProfileDespiteBrowsingAndKeepsTheRenderer()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            configuration.Object.LastOverlayDesignProfileId = "second";
            var model = Create(designs, configuration);
            Assert.AreSame(designs.Profiles[1], model.SelectedDesign);
            Assert.IsFalse(model.IsTileOverlayEnabled);
            Assert.AreEqual(0, designs.ActivationCalls);
            model.SelectedDesign = designs.Profiles[0];

            model.IsTileOverlayEnabled = true;

            Assert.IsTrue(model.IsTileOverlayEnabled);
            Assert.AreEqual("second", designs.ActiveProfileId);
            Assert.AreEqual(1, model.SelectedOverlayTabIndex);
            Assert.IsTrue(model.IsTileOverlayTabVisible);
            Assert.IsTrue(configuration.Object.EnableHookOverlay);
            Assert.IsFalse(configuration.Object.EnableHookFreeOverlay);
            Assert.IsFalse(configuration.Object.IsOverlayActive);

            model.SelectedDesign = designs.Profiles[0];
            model.IsTileOverlayEnabled = true;
            Assert.AreEqual("second", designs.ActiveProfileId,
                "Writing the existing mode must not activate a profile that is only being browsed.");

            model.IsTileOverlayEnabled = false;
            Assert.IsFalse(model.IsTileOverlayEnabled);
            Assert.IsFalse(model.IsTileOverlayTabVisible);
            Assert.AreEqual("first", model.SelectedDesign.Id);
            Assert.AreEqual(2, model.SavedDesigns.Count);
            Assert.AreEqual(0, model.SelectedOverlayTabIndex);

            model.IsTileOverlayEnabled = true;
            Assert.AreEqual("second", designs.ActiveProfileId, "Returning to tile mode must reuse the remembered profile.");
            Assert.AreSame(designs.Profiles[1], model.SelectedDesign);
            Assert.AreEqual(2, designs.ActivationCalls);
        }

        [TestMethod]
        public void MissingRememberedProfileFallsBackToTheFirstAvailableProfile()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            configuration.Object.LastOverlayDesignProfileId = "removed";
            var model = Create(designs, configuration);

            Assert.AreSame(designs.Profiles[0], model.SelectedDesign);
            Assert.AreEqual(0, designs.ActivationCalls, "Restoring the initial selection must not enable tile mode.");
            model.SelectedDesign = designs.Profiles[1];
            model.IsTileOverlayEnabled = true;
            Assert.AreEqual("first", designs.ActiveProfileId);
            Assert.AreSame(designs.Profiles[0], model.SelectedDesign);
            Assert.AreEqual(1, designs.ActivationCalls);
        }

        [TestMethod]
        public void MissingActiveProfileDoesNotActivateTheRememberedProfileUntilTileModeIsReenabled()
        {
            var designs = new DesignService();
            designs.Activate("unavailable");
            var configuration = Configuration();
            configuration.Object.LastOverlayDesignProfileId = "second";
            var model = Create(designs, configuration);

            Assert.IsTrue(model.HasActiveDesign);
            Assert.AreEqual("unavailable", designs.ActiveProfileId);
            model.SelectedDesign = designs.Profiles[0];
            model.RefreshDesignsCommand.Execute();
            model.IsTileOverlayEnabled = true;
            Assert.AreEqual("unavailable", designs.ActiveProfileId, "Refreshing and browsing must not silently replace the active profile.");
            Assert.AreEqual(1, designs.ActivationCalls);

            model.IsTileOverlayEnabled = false;
            Assert.AreEqual(1, designs.ActivationCalls);
            model.IsTileOverlayEnabled = true;
            Assert.AreEqual("second", designs.ActiveProfileId);
            Assert.AreSame(designs.Profiles[1], model.SelectedDesign);
            Assert.AreEqual(2, designs.ActivationCalls);
        }

        [TestMethod]
        public void InitiallyActiveTileDesignSelectsItsVisibleEditor()
        {
            var designs = new DesignService();
            designs.Activate("second");
            var configuration = Configuration();
            configuration.Object.LastOverlayDesignProfileId = "first";
            var model = Create(designs, configuration);

            Assert.IsTrue(model.IsTileOverlayEnabled);
            Assert.IsTrue(model.IsTileOverlayTabVisible);
            Assert.AreEqual(1, model.SelectedOverlayTabIndex);
            Assert.AreEqual("second", model.SelectedDesign.Id);
            Assert.AreEqual("second", designs.ActiveProfileId);
        }

        [TestMethod]
        public void ModeToggleCanReturnToRowsWhenTileActivationBecomesUnavailable()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            model.SelectedDesign = null;
            Assert.IsTrue(model.CanToggleOverlayMode, "Tile mode can use its initial profile without a passive list selection.");
            model.SelectedDesign = designs.Profiles[0];
            Assert.IsTrue(model.CanToggleOverlayMode);
            model.IsTileOverlayEnabled = true;

            designs.CanActivate = false;
            designs.RefreshProfiles();
            model.OverlayModeRtss = true;
            Assert.IsFalse(model.CanUseDesign);
            Assert.IsTrue(model.CanToggleOverlayMode);

            model.IsTileOverlayEnabled = false;
            Assert.IsFalse(model.IsTileOverlayEnabled);
            Assert.IsFalse(model.CanToggleOverlayMode);
            Assert.IsTrue(model.OverlayModeRtss);
        }

        [TestMethod]
        public void ModeToggleCannotChangeTheOverlayDuringDeletionConfirmation()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            model.SelectedOverlayTabIndex = 1;
            model.IsTileOverlayEnabled = true;
            model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]);

            Assert.IsFalse(model.CanToggleOverlayMode);
            model.IsTileOverlayEnabled = false;
            Assert.AreEqual("first", designs.ActiveProfileId);
            Assert.IsTrue(model.IsDeleteDesignDialogOpen);
            model.CancelDeleteDesignCommand.Execute();
            Assert.IsTrue(model.CanToggleOverlayMode);
        }

        [TestMethod]
        public void FailedModeToggleRestoresTheActualStateThroughATwoWayWpfBinding()
        {
            var designs = new DesignService { RefuseActivation = true };
            var model = Create(designs, Configuration());
            var toggle = new ToggleButton();
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(model.IsTileOverlayEnabled))
            {
                Source = model,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Assert.AreEqual(false, toggle.IsChecked);

            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));

            Assert.IsFalse(model.IsTileOverlayEnabled);
            Assert.AreEqual(false, toggle.IsChecked, "A failed activation must not leave the UI in tile mode.");
            StringAssert.Contains(model.DesignStatus, "Unavailable design");
            Assert.IsTrue(model.ShowDesignRecoveryActions);

            designs.RefuseActivation = false;
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            Assert.IsTrue(model.IsTileOverlayEnabled);
            Assert.AreEqual(true, toggle.IsChecked);
            Assert.IsFalse(model.ShowDesignRecoveryActions);
            designs.Deactivate();
            Assert.AreEqual(false, toggle.IsChecked, "Service changes must also refresh the global toggle.");
            BindingOperations.ClearAllBindings(toggle);
        }

        [TestMethod]
        public void ModeTogglePropertiesRefreshForSelectionServiceRendererAndDialogChanges()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            var properties = new HashSet<string>();
            model.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
            void AssertNotifications(Action change)
            {
                properties.Clear();
                change();
                Assert.IsTrue(properties.Contains(nameof(model.IsTileOverlayEnabled)));
                Assert.IsTrue(properties.Contains(nameof(model.CanToggleOverlayMode)));
            }

            AssertNotifications(() => model.SelectedDesign = designs.Profiles[1]);
            AssertNotifications(() => designs.Activate("first"));
            AssertNotifications(() => model.OverlayModeHookFree = true);
            AssertNotifications(() => model.DeleteSavedDesignCommand.Execute(designs.Profiles[1]));
            AssertNotifications(() => model.CancelDeleteDesignCommand.Execute());
        }

        [TestMethod]
        public void RefreshAfterDeletedSelectionSelectsAnExistingDesign()
        {
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            Assert.IsFalse(model.ShowDesignRecoveryActions);
            model.SelectedDesign = designs.Profiles[1];
            designs.Profiles = new[] { designs.Profiles[0] };
            model.RefreshDesignsCommand.Execute();
            Assert.AreEqual("first", model.SelectedDesign.Id);
            designs.Profiles = Array.Empty<OverlayDesignInfo>();
            model.RefreshDesignsCommand.Execute();
            Assert.IsNull(model.SelectedDesign);
            Assert.IsFalse(model.UseDesignCommand.CanExecute());
            Assert.IsFalse(model.CanToggleOverlayMode);
            Assert.IsTrue(model.ShowDesignRecoveryActions, "An empty library must leave a visible way to create or reload profiles.");

            designs.Profiles = new[] { new OverlayDesignInfo("restored", "Restored design") };
            model.RefreshDesignsCommand.Execute();
            Assert.AreEqual("restored", model.SelectedDesign.Id);
            Assert.IsFalse(model.ShowDesignRecoveryActions);
        }

        [TestMethod]
        public void FailedActivationIsVisibleWithoutChangingActiveDesign()
        {
            var designs = new DesignService { RefuseActivation = true };
            var model = Create(designs, Configuration());
            model.UseDesignCommand.Execute();
            Assert.IsFalse(model.HasActiveDesign);
            StringAssert.Contains(model.DesignStatus, "Unavailable design");
            Assert.IsTrue(model.ShowDesignRecoveryActions, "A broken profile must not make the editor inaccessible.");
            model.OverlayModeRtss = true;
            Assert.IsFalse(model.ShowDesignRecoveryActions, "RTSS cannot use tile designs.");
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
            Assert.IsFalse(model.ShowDesignRecoveryActions, "The active tile editor already provides the recovery actions.");
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
        public void RendererOffDisablesTheOverlayTogglesAndDesignsUntilARendererIsSelected()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            var model = Create(designs, configuration, new EventAggregator());

            model.OverlayModeOff = true;

            Assert.AreEqual(EOverlayRenderer.Off, configuration.Object.OverlayRenderer);
            Assert.IsTrue(model.OverlayModeOff);
            Assert.IsFalse(model.OverlayModeRtss, "Off clears the flags like RTSS but is a renderer of its own.");
            Assert.IsFalse(model.OverlayModeHook);
            Assert.IsFalse(model.OverlayModeHookFree);
            Assert.IsFalse(model.CanSwitchOverlay);
            Assert.AreEqual(CxLang.T("OverlayView_OverlayRendererIsOff"), model.OverlayToggleToolTip);
            Assert.IsFalse(model.IsTileOverlayAvailable);
            Assert.IsFalse(model.CanUseDesign);
            model.IsOverlayActive = true;
            Assert.IsFalse(configuration.Object.IsOverlayActive, "The hotkey and every toggle refuse to show an overlay.");

            model.OverlayModeHook = true;
            Assert.IsFalse(model.OverlayModeOff);
            Assert.IsTrue(model.CanSwitchOverlay);
            Assert.IsTrue(model.CanUseDesign);
            Assert.AreEqual(CxLang.T("OverlayView_ShowOrHideTheOverlay"), model.OverlayToggleToolTip);
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
        public void RtssHidesTheTileEditorUntilACompatibleRendererIsSelected()
        {
            var designs = new DesignService();
            var configuration = Configuration();
            var model = Create(designs, configuration);
            model.IsTileOverlayEnabled = true;
            Assert.IsTrue(model.IsTileOverlayAvailable);
            model.OverlayModeHookFree = true;
            Assert.AreEqual(1, model.SelectedOverlayTabIndex);

            model.OverlayModeRtss = true;
            Assert.IsFalse(model.IsTileOverlayAvailable);
            Assert.IsFalse(model.IsTileOverlayTabVisible);
            Assert.AreEqual(0, model.SelectedOverlayTabIndex);
            model.SelectedOverlayTabIndex = 1;
            Assert.AreEqual(0, model.SelectedOverlayTabIndex, "A hidden tab cannot be selected programmatically.");
            Assert.IsTrue(model.OverlayModeRtss, "Selecting a tab must never change the renderer.");

            model.OverlayModeHook = true;
            Assert.IsTrue(model.IsTileOverlayAvailable);
            Assert.IsTrue(model.IsTileOverlayTabVisible);
            Assert.AreEqual(1, model.SelectedOverlayTabIndex,
                "This service retains its active design across renderer changes, so its tile editor must return.");
            Assert.AreEqual("first", designs.ActiveProfileId);
            model.SelectedOverlayTabIndex = 0;
            Assert.AreEqual(1, model.SelectedOverlayTabIndex, "The hidden row editor cannot be selected either.");
            model.SelectedOverlayTabIndex = 2;
            model.OverlayModeRtss = true;
            Assert.AreEqual(2, model.SelectedOverlayTabIndex, "The always-visible OSD options must remain selected.");
        }

        [TestMethod]
        public void ProductionTabBindingsFollowTheActiveModeAndPreserveTheOptionsSelection()
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "CapFrameX.sln"))) root = root.Parent;
            Assert.IsNotNull(root);
            var document = XDocument.Load(Path.Combine(root.FullName, "source", "CapFrameX.View", "OverlayView.xaml"));
            XNamespace mvvm = document.Root.GetNamespaceOfPrefix("mvvm");
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
                new XAttribute(XNamespace.Xmlns + "mvvm", mvvm.NamespaceName),
                new XAttribute("SelectedIndex", source.Attribute("SelectedIndex").Value),
                new XElement(wpf + "TabControl.Resources", document.Descendants(mvvm + "BoolToVisibilityConverter")
                    .Where(element => (string)element.Attribute(xaml + "Key") == "TrueToVisibleConverter"
                        || (string)element.Attribute(xaml + "Key") == "FalseToVisibleConverter")));
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
            var designs = new DesignService();
            var model = Create(designs, Configuration());
            tabs.DataContext = model;
            tabs.Measure(new Size(640, 180));
            tabs.Arrange(new Rect(0, 0, 640, 180));
            tabs.UpdateLayout();
            var rowTab = (TabItem)tabs.Items[0];
            var tileTab = (TabItem)tabs.Items[1];
            var optionsTab = (TabItem)tabs.Items[2];
            void AssertTabs(bool tileVisible, TabItem selected)
            {
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
                tabs.UpdateLayout();
                Assert.AreEqual(tileVisible ? Visibility.Collapsed : Visibility.Visible, rowTab.Visibility);
                Assert.AreEqual(tileVisible ? Visibility.Visible : Visibility.Collapsed, tileTab.Visibility);
                Assert.AreEqual(Visibility.Visible, optionsTab.Visibility);
                Assert.AreSame(selected, tabs.SelectedItem);
                Assert.AreEqual(tabs.Items.IndexOf(selected), model.SelectedOverlayTabIndex);
            }

            AssertTabs(false, rowTab);
            model.SelectedOverlayTabIndex = 1;
            AssertTabs(false, rowTab);

            designs.RefuseActivation = true;
            model.IsTileOverlayEnabled = true;
            AssertTabs(false, rowTab);
            StringAssert.Contains(model.DesignStatus, "Unavailable design");

            designs.RefuseActivation = false;
            model.IsTileOverlayEnabled = true;
            AssertTabs(true, tileTab);
            model.SelectedOverlayTabIndex = 0;
            AssertTabs(true, tileTab);

            tabs.SelectedItem = optionsTab;
            model.IsTileOverlayEnabled = false;
            AssertTabs(false, optionsTab);
            model.IsTileOverlayEnabled = true;
            AssertTabs(true, optionsTab);

            model.SelectedOverlayTabIndex = 1;
            designs.Deactivate();
            AssertTabs(false, rowTab);
            designs.Activate("second");
            AssertTabs(true, tileTab);

            model.OverlayModeRtss = true;
            AssertTabs(false, rowTab);
            model.OverlayModeHookFree = true;
            AssertTabs(true, tileTab);

            model.OverlayModeRtss = true;
            designs.Deactivate();
            model.OverlayModeHookFree = true;
            AssertTabs(false, rowTab);
            Assert.IsFalse(model.HasActiveDesign);

            tabs.SelectedItem = optionsTab;
            model.OverlayModeRtss = true;
            AssertTabs(false, optionsTab);
            model.OverlayModeHookFree = true;
            AssertTabs(false, optionsTab);
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
            model.IsTileOverlayEnabled = true;
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

        private static (StackPanel View, ListBox Profiles, Button Show) CreateProfileControls(OverlayViewModel model)
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "CapFrameX.sln"))) root = root.Parent;
            Assert.IsNotNull(root);
            var document = XDocument.Load(Path.Combine(root.FullName, "source", "CapFrameX.View", "OverlayView.xaml"));
            var sourceList = document.Descendants(wpf + "ListBox")
                .Single(element => (string)element.Attribute(xaml + "Name") == "SavedTileProfiles");
            var sourceButton = document.Descendants(wpf + "Button")
                .Single(element => (string)element.Attribute(xaml + "Name") == "ShowSelectedTileProfileButton");
            var fragment = new XElement(wpf + "StackPanel", new XAttribute("xmlns", wpf.NamespaceName),
                new XElement(wpf + "Button", new XAttribute(sourceButton.Attribute("Command")),
                    new XAttribute(sourceButton.Attribute("CommandParameter"))),
                new XElement(wpf + "ListBox", new XAttribute(sourceList.Attribute("ItemsSource")),
                    new XAttribute(sourceList.Attribute("SelectedItem"))));
            var view = (StackPanel)XamlReader.Parse(fragment.ToString());
            view.DataContext = model;
            // Complete item, selection and command bindings before simulating user input.
            view.Measure(new Size(440, 240));
            view.Arrange(new Rect(0, 0, 440, 240));
            view.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            return (view, (ListBox)view.Children[1], (Button)view.Children[0]);
        }

        // The production profile list with its item template, its converter and its namespaces.
        private static (Border View, ListBox Profiles) CreateProfileList(OverlayViewModel model)
        {
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "CapFrameX.sln"))) root = root.Parent;
            Assert.IsNotNull(root);
            var document = XDocument.Load(Path.Combine(root.FullName, "source", "CapFrameX.View", "OverlayView.xaml"));
            var sourceList = document.Descendants(wpf + "ListBox")
                .Single(element => (string)element.Attribute(xaml + "Name") == "SavedTileProfiles");
            var list = new XElement(wpf + "ListBox", sourceList.Attribute("ItemsSource"), sourceList.Attribute("SelectedItem"),
                sourceList.Element(wpf + "ListBox.ItemTemplate"));
            var fragment = new XElement(wpf + "Border",
                document.Root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration),
                new XElement(wpf + "Border.Resources",
                    new XElement(XName.Get("EqualValuesConverter", "clr-namespace:CapFrameX.MVVM.Converter;assembly=CapFrameX.MVVM"),
                        new XAttribute(xaml + "Key", "EqualValuesConverter"))),
                list);
            var view = (Border)XamlReader.Parse(fragment.ToString());
            view.DataContext = model;
            view.Measure(new Size(440, 240));
            view.Arrange(new Rect(0, 0, 440, 240));
            view.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            return (view, (ListBox)view.Child);
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var nested = FindChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private static Mock<IAppConfiguration> Configuration()
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            config.WireOverlayRenderer(EOverlayRenderer.InGame);
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
            public int ActivationCalls { get; private set; }
            public List<string> DeletedIds { get; } = new List<string>();
            public event EventHandler Changed;
            public void RefreshProfiles() => Changed?.Invoke(this, EventArgs.Empty);
            public void Activate(string profileId)
            {
                ActivationCalls++;
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
