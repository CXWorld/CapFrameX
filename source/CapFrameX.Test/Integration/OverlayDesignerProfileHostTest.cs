using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.View.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerProfileHostTest
    {
        [TestMethod]
        [DataRow(BaseTheme.Dark, 1120, 780)]
        [DataRow(BaseTheme.Light, 1120, 780)]
        [DataRow(BaseTheme.Dark, 1500, 920)]
        [DataRow(BaseTheme.Light, 1500, 920)]
        public void ProfileToolbarAndEditorRemainUsableAtWindowMinimumSize(BaseTheme theme, int width, int height)
        {
            using var fixture = new HostFixture(theme: theme, width: width, height: height);
            var host = fixture.Host;
            host.ConfigureDesignService(Moq.Mock.Of<CapFrameX.Contracts.Overlay.IOverlayDesignService>());
            fixture.Arrange();
            var toolbar = (WrapPanel)host.FindName("ProfileToolbar");
            foreach (FrameworkElement command in toolbar.Children)
            {
                Rect bounds = command.TransformToAncestor(host).TransformBounds(new Rect(command.RenderSize));
                Assert.IsTrue(bounds.Left >= 0 && bounds.Right <= width,
                    $"The {command.Name} profile command is clipped at {width} pixels.");
                Assert.IsTrue(command.ActualHeight > 0);
            }
            Rect editor = host.Designer.TransformToAncestor(host).TransformBounds(new Rect(host.Designer.RenderSize));
            Assert.IsTrue(editor.Right <= width && editor.Bottom <= height,
                "The saved-design toolbar must not push the editor outside the minimum window.");
            Assert.IsTrue(host.Designer.ActualHeight >= host.Designer.MinHeight);
        }

        [TestMethod]
        public void StarterCatalogIsAvailableImmediatelyAndBindingASavedGroupStartsClean()
        {
            using var fixture = new HostFixture();
            Assert.AreEqual(12, fixture.ReopenStore().Profiles.Count);
            var starter = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Vertical Thread Strip");
            fixture.Host.Designer.Sources = Enumerable.Range(1, 32).Select(index => new OsdTelemetrySource
            {
                Key = "cpu/thread/" + index, Name = "CPU Thread " + index, Category = "Processor", Unit = "%",
                DeviceKey = "cpu0", DeviceName = "Fixture CPU", GroupKey = "cpu0/cpu-loads", GroupName = "CPU thread loads", GroupOrder = index
            }).ToArray();
            ((ComboBox)fixture.Host.FindName("ProfileSelector")).SelectedValue = starter.Id;
            Assert.AreEqual(starter.Id, fixture.Host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(32, fixture.Host.Designer.Document.Tiles.Single(tile => tile.Kind == "group").GroupMembers.Count);
            Assert.IsFalse(fixture.Host.HasUnsavedChanges);
            Assert.IsFalse(((Button)fixture.Host.Designer.FindName("UndoButton")).IsEnabled,
                "Initial source binding is the saved baseline, not an undoable user edit.");
            var saved = OsdDesignDocument.FromJson(fixture.ReopenStore().Get(starter.Id).DesignJson);
            Assert.AreEqual(32, saved.Tiles.Single(tile => tile.Kind == "group").GroupMembers.Count);
            Assert.IsFalse(OverlayDesignStarterCatalog.IsUnmodifiedStarter(fixture.ReopenStore().Get(starter.Id)),
                "Once resolved, the profile must retain its exact hardware bindings.");
        }

        [TestMethod]
        public void ImmediateEditsCanBeSavedThroughTheHostWithoutInitializingNativePreview()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.IsFalse(host.Designer.IsLoaded, "This test must work without a native rendering window.");
            Assert.IsTrue(host.Designer.HostManagesFiles);

            host.Designer.Document.Width += 40;
            double expectedWidth = host.Designer.Document.Width;
            Assert.IsTrue(host.HasUnsavedChanges,
                "Closing immediately after an edit must detect it before the 250 ms preview debounce.");
            Assert.IsTrue(fixture.Button("SaveProfileButton").IsEnabled);
            fixture.Click("SaveProfileButton");

            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.IsFalse(host.IsProfileOperationPending);
            var saved = fixture.ReopenStore();
            Assert.AreEqual(expectedWidth, OsdDesignDocument.FromJson(saved.Get(saved.ActiveProfileId).DesignJson).Width);
            Assert.AreEqual(host.ProfileSession.ActiveProfile.Name, host.Designer.Document.Name);
        }

        [TestMethod]
        public void RuntimeActivationChangesRefreshTheOpenDesignerFromThePublishingThread()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            var service = new DesignService();
            host.ConfigureDesignService(service);
            var status = (TextBlock)host.FindName("ProfileStatus");
            service.Activate(host.ProfileSession.ActiveProfile.Id);
            StringAssert.Contains(status.Text, "Tile overlay active");
            Task.Run(service.Deactivate).GetAwaiter().GetResult();
            DrainDispatcher();
            Assert.IsFalse(status.Text.Contains("Tile overlay active"));
            Assert.IsFalse(host.HasUnsavedChanges, "Runtime selection changes must not mutate the working design.");
        }

        [TestMethod]
        public void RtssDisablesActivationInTheOpenDesignerWithoutDisablingEditing()
        {
            using var fixture = new HostFixture();
            var service = new DesignService { CanActivate = false };
            fixture.Host.ConfigureDesignService(service);
            Assert.IsFalse(fixture.Button("UseDesignButton").IsEnabled);
            StringAssert.Contains(((TextBlock)fixture.Host.FindName("ProfileStatus")).Text, "RTSS");
            Assert.IsTrue(fixture.Host.Designer.IsEnabled);
            fixture.Click("UseDesignButton");
            Assert.IsNull(service.ActiveProfileId, "Routed activation requests must also respect RTSS.");
            Assert.IsFalse(fixture.Host.HasUnsavedChanges);

            service.CanActivate = true;
            service.RefreshProfiles();
            Assert.IsTrue(fixture.Button("UseDesignButton").IsEnabled);
            Assert.IsFalse(((TextBlock)fixture.Host.FindName("ProfileStatus")).Text.Contains("RTSS"));
        }

        [TestMethod]
        public void RuntimeStatusSubscriptionsFollowHostLifetimeWithoutDuplicatesOrStaleCallbacks()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            var first = new DesignService();
            var second = new DesignService();
            host.ConfigureDesignService(first);
            host.ConfigureDesignService(first);
            Assert.AreEqual(1, first.Subscribers);

            Task.Run(() => first.Activate(host.ProfileSession.ActiveProfile.Id)).GetAwaiter().GetResult();
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.AreEqual(0, first.Subscribers);
            int reads = first.EnabledReads;
            DrainDispatcher();
            Assert.AreEqual(reads, first.EnabledReads, "A callback queued before unloading must not update the detached host.");

            host.ConfigureDesignService(second);
            Assert.AreEqual(0, second.Subscribers);
            second.Activate(host.ProfileSession.ActiveProfile.Id);
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.AreEqual(1, second.Subscribers);
            StringAssert.Contains(((TextBlock)host.FindName("ProfileStatus")).Text, "Tile overlay active");
            host.ConfigureDesignService(first);
            Assert.AreEqual(0, second.Subscribers);
            Assert.AreEqual(1, first.Subscribers);
            host.ConfigureDesignService(null);
            Assert.AreEqual(0, first.Subscribers);
            Assert.AreEqual(Visibility.Collapsed, fixture.Button("UseDesignButton").Visibility);
        }

        [TestMethod]
        public void InvalidDocumentDisablesSavingAndRecoveryPreservesTheLastSavedProfile()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            double originalWidth = host.Designer.Document.Width;
            string originalJson = host.ProfileSession.ActiveProfile.DesignJson;
            host.Designer.Document.Width = -1;

            Assert.IsTrue(host.HasUnsavedChanges);
            Assert.IsTrue(host.ProfileSession.HasValidationErrors);
            Assert.IsFalse(fixture.Button("SaveProfileButton").IsEnabled);
            Assert.IsFalse(fixture.Button("DuplicateProfileButton").IsEnabled);
            Assert.IsFalse(fixture.Button("ExportProfileButton").IsEnabled);
            // Routed/programmatic save requests must also be guarded, even if the button is disabled.
            fixture.Click("SaveProfileButton");
            Assert.AreEqual(originalJson, fixture.ReopenStore().Get(host.ProfileSession.ActiveProfile.Id).DesignJson);
            Assert.IsTrue(host.HasUnsavedChanges);
            Assert.IsFalse(host.IsProfileOperationPending);

            host.Designer.Document.Width = originalWidth + 40;
            Assert.IsFalse(host.ProfileSession.HasValidationErrors);
            Assert.IsTrue(fixture.Button("SaveProfileButton").IsEnabled);
            fixture.Click("SaveProfileButton");
            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)host.FindName("ProfileError")).Visibility);
        }

        [TestMethod]
        public void NumericBindingErrorsMarkTheHostDirtyEvenWhenTheDocumentValueHasNotChanged()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            var tabs = (TabControl)host.Designer.FindName("InspectorTabs");
            tabs.SelectedItem = tabs.Items.Cast<TabItem>().Single(tab => Equals(tab.Header, "Layout"));
            fixture.Arrange();
            var widthInput = Descendants<TextBox>(tabs).Single(input =>
                BindingOperations.GetBinding(input, TextBox.TextProperty)?.Path?.Path == "Document.Width");
            double originalWidth = host.Designer.Document.Width;

            widthInput.Text = "invalid number";
            widthInput.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Assert.IsTrue(Validation.GetHasError(widthInput));
            Assert.AreEqual(originalWidth, host.Designer.Document.Width);
            Assert.IsTrue(host.HasUnsavedChanges, "A conversion error must not disappear from the close guard.");
            Assert.IsTrue(host.ProfileSession.HasValidationErrors);
            Assert.IsFalse(fixture.Button("SaveProfileButton").IsEnabled);

            widthInput.Text = originalWidth.ToString(System.Globalization.CultureInfo.CurrentCulture);
            widthInput.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Assert.IsFalse(Validation.GetHasError(widthInput));
            Assert.IsFalse(host.ProfileSession.HasValidationErrors);
            Assert.IsFalse(host.HasUnsavedChanges, "Correcting the field to its saved value must clear the dirty state.");
        }

        [TestMethod]
        public void SelectingASavedProfileClearsEditorUndoAndRedoAcrossProfileBoundaries()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            string originalId = host.ProfileSession.ActiveProfile.Id;
            var next = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Second");
            host.Designer.Document.Width += 40;
            Assert.IsTrue(host.Designer.TryCommitEdits());
            Assert.IsTrue(((Button)host.Designer.FindName("UndoButton")).IsEnabled);
            fixture.Click("SaveProfileButton");

            var selector = (ComboBox)host.FindName("ProfileSelector");
            selector.SelectedValue = next.Id;
            Assert.AreEqual(next.Id, host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(next.Id, fixture.ReopenStore().ActiveProfileId);
            Assert.AreEqual(next.Name, host.Designer.Document.Name);
            Assert.IsFalse(((Button)host.Designer.FindName("UndoButton")).IsEnabled);
            Assert.IsFalse(((Button)host.Designer.FindName("RedoButton")).IsEnabled);
            string selectedJson = host.Designer.GetTemplateJson();
            ((Button)host.Designer.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(selectedJson, host.Designer.GetTemplateJson(), "Undo must not restore the previous profile.");
            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.IsFalse(host.IsProfileOperationPending);
        }

        [TestMethod]
        public void OpeningARequestedProfileRefreshesTheLibraryWithoutRestoringDeletedProfiles()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var external = fixture.ReopenStore();
            string deletedId = external.Profiles.Single(profile => profile.Name == "Second").Id;
            external.Delete(deletedId);
            var requested = external.Create("Added from another editor", OsdDesignDocument.CreatePreset("Minimal").ToJson());

            Assert.IsTrue(fixture.Host.OpenProfileAsync(requested.Id).GetAwaiter().GetResult());
            Assert.AreEqual(requested.Id, fixture.Host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(requested.Name, fixture.Host.Designer.Document.Name);
            Assert.AreEqual(requested.Id, fixture.ReopenStore().ActiveProfileId);
            Assert.IsFalse(((ComboBox)fixture.Host.FindName("ProfileSelector")).Items.Cast<OverlayDesignProfile>()
                .Any(profile => profile.Id == deletedId));
            Assert.IsFalse(fixture.Host.HasUnsavedChanges);
        }

        [TestMethod]
        public void OpeningTheCurrentProfilePreservesUnsavedEditsAndReloadsCleanExternalChanges()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            string id = host.ProfileSession.ActiveProfile.Id;
            host.Designer.Document.Width += 40;
            string working = host.Designer.GetTemplateJson();
            Assert.IsTrue(host.OpenProfileAsync(id).GetAwaiter().GetResult());
            Assert.AreEqual(working, host.Designer.GetTemplateJson());
            Assert.IsTrue(host.HasUnsavedChanges);
            fixture.Click("SaveProfileButton");

            var external = fixture.ReopenStore();
            var changed = OsdDesignDocument.FromJson(external.Get(id).DesignJson);
            changed.Width += 80;
            external.Save(id, changed.ToJson());
            Assert.IsTrue(host.OpenProfileAsync(id).GetAwaiter().GetResult());
            Assert.AreEqual(changed.Width, host.Designer.Document.Width);
            Assert.IsFalse(host.HasUnsavedChanges);
        }

        [TestMethod]
        [DataRow(OverlayDesignPromptResult.Cancel)]
        [DataRow(OverlayDesignPromptResult.Confirm)]
        [DataRow(OverlayDesignPromptResult.Secondary)]
        public void OpeningARequestedProfileHonorsSaveDiscardAndCancel(OverlayDesignPromptResult decision)
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            var original = host.ProfileSession.ActiveProfile;
            var requested = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Second");
            host.Designer.Document.Width += 40;
            string working = host.Designer.GetTemplateJson();

            bool opened = OpenWithDecision(fixture, requested.Id, decision);

            Assert.AreEqual(decision != OverlayDesignPromptResult.Cancel, opened);
            Assert.AreEqual(opened ? requested.Id : original.Id, host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(opened ? requested.Id : original.Id, ((ComboBox)host.FindName("ProfileSelector")).SelectedValue);
            Assert.AreEqual(decision == OverlayDesignPromptResult.Confirm ? working : original.DesignJson,
                fixture.ReopenStore().Get(original.Id).DesignJson);
            Assert.AreEqual(decision == OverlayDesignPromptResult.Cancel, host.HasUnsavedChanges);
            if (!opened)
            {
                Assert.AreEqual(working, host.Designer.GetTemplateJson());
                Assert.AreEqual(original.Id, fixture.ReopenStore().ActiveProfileId);
            }
            Assert.IsFalse(host.IsProfileOperationPending);
        }

        [TestMethod]
        public void SavingBeforeOpeningKeepsOtherExternallyCreatedAndDeletedProfiles()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            string originalId = host.ProfileSession.ActiveProfile.Id;
            host.Designer.Document.Width += 40;
            string working = host.Designer.GetTemplateJson();
            var external = fixture.ReopenStore();
            string removedId = external.Profiles.Single(profile => profile.Name == "Second").Id;
            external.Delete(removedId);
            var requested = external.Create("New external profile", OsdDesignDocument.CreatePreset("Minimal").ToJson());

            Assert.IsTrue(OpenWithDecision(fixture, requested.Id, OverlayDesignPromptResult.Confirm));
            var saved = fixture.ReopenStore();
            Assert.AreEqual(working, saved.Get(originalId).DesignJson);
            Assert.AreEqual(requested.Id, saved.ActiveProfileId);
            Assert.IsFalse(saved.Profiles.Any(profile => profile.Id == removedId));
        }

        [TestMethod]
        public void DeletingTheRequestedProfileDuringThePromptPreservesTheWorkingDocument()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            string originalId = host.ProfileSession.ActiveProfile.Id;
            var requested = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Second");
            host.Designer.Document.Width += 40;
            string working = host.Designer.GetTemplateJson();

            Assert.IsFalse(OpenWithDecision(fixture, requested.Id, OverlayDesignPromptResult.Secondary,
                () => fixture.ReopenStore().Delete(requested.Id)));
            Assert.AreEqual(originalId, host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(working, host.Designer.GetTemplateJson());
            Assert.IsTrue(host.HasUnsavedChanges);
            Assert.IsFalse(fixture.ReopenStore().Profiles.Any(profile => profile.Id == requested.Id));
            StringAssert.Contains(((TextBlock)host.FindName("ProfileError")).Text, "no longer exists");
        }

        [TestMethod]
        public void SavingBeforeOpeningCannotOverwriteAProfileChangedExternally()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            string originalId = host.ProfileSession.ActiveProfile.Id;
            var requested = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Second");
            host.Designer.Document.Width += 40;
            string working = host.Designer.GetTemplateJson();
            var external = fixture.ReopenStore();
            var changed = OsdDesignDocument.FromJson(external.Get(originalId).DesignJson);
            changed.Width += 80;
            external.Save(originalId, changed.ToJson());

            Assert.IsFalse(OpenWithDecision(fixture, requested.Id, OverlayDesignPromptResult.Confirm));
            Assert.AreEqual(originalId, host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(working, host.Designer.GetTemplateJson());
            Assert.IsTrue(host.HasUnsavedChanges);
            Assert.AreEqual(changed.ToJson(), fixture.ReopenStore().Get(originalId).DesignJson);
            StringAssert.Contains(((TextBlock)host.FindName("ProfileError")).Text, "changed or deleted");
        }

        [TestMethod]
        public void DiscardingAnInvalidDocumentCanOpenTheRequestedSavedProfile()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            var original = host.ProfileSession.ActiveProfile;
            var requested = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Second");
            host.Designer.Document.Width = -1;
            Assert.IsTrue(host.ProfileSession.HasValidationErrors);

            Assert.IsTrue(OpenWithDecision(fixture, requested.Id, OverlayDesignPromptResult.Secondary));
            Assert.AreEqual(requested.Id, host.ProfileSession.ActiveProfile.Id);
            Assert.IsFalse(host.ProfileSession.HasValidationErrors);
            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.AreEqual(original.DesignJson, fixture.ReopenStore().Get(original.Id).DesignJson);
        }

        [TestMethod]
        public void RequestingAMissingProfileDoesNotPromptOrReplaceWorkingEdits()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            string originalId = host.ProfileSession.ActiveProfile.Id;
            host.Designer.Document.Width += 40;
            string working = host.Designer.GetTemplateJson();

            Assert.IsFalse(host.OpenProfileAsync(Guid.NewGuid().ToString("N")).GetAwaiter().GetResult());
            Assert.AreEqual(originalId, host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(working, host.Designer.GetTemplateJson());
            Assert.IsTrue(host.HasUnsavedChanges);
            StringAssert.Contains(((TextBlock)host.FindName("ProfileError")).Text, "no longer exists");
        }

        [TestMethod]
        public void UndoingAnInvalidEditUpdatesTheHostValidationAndCloseGuardImmediately()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            string saved = host.Designer.GetTemplateJson();
            host.Designer.Document.Width = -1;
            Assert.IsTrue(host.ProfileSession.HasValidationErrors);

            // Ctrl+Z routes to this same handler even when no valid undo entry could
            // be recorded for the invalid edit.
            ((Button)host.Designer.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(saved, host.Designer.GetTemplateJson());
            Assert.IsFalse(host.ProfileSession.HasValidationErrors,
                "The host must receive the recovery edit even when preview validation initially failed.");
            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.IsTrue(fixture.Button("DuplicateProfileButton").IsEnabled);
            Assert.IsTrue(fixture.Button("ExportProfileButton").IsEnabled);
        }

        [TestMethod]
        public void CleanCloseAndReopeningPreserveTheActiveDesignWithoutADialog()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            var next = fixture.ReopenStore().Profiles.Last();
            ((ComboBox)host.FindName("ProfileSelector")).SelectedValue = next.Id;
            Assert.IsTrue(host.TryCloseAsync().GetAwaiter().GetResult());
            Assert.IsFalse(host.HasUnsavedChanges);
            Assert.IsFalse(host.IsProfileOperationPending);
            Assert.AreEqual(next.Id, fixture.ReopenStore().ActiveProfileId);

            var reopened = new OverlayDesignerControl();
            try
            {
                reopened.ConfigureProfiles(fixture.Folder);
                reopened.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.AreEqual(next.Id, reopened.ProfileSession.ActiveProfile.Id);
                Assert.AreEqual(host.Designer.GetTemplateJson(), reopened.Designer.GetTemplateJson());
                Assert.IsFalse(reopened.HasUnsavedChanges);
            }
            finally
            {
                reopened.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                reopened.Designer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
        }

        [TestMethod]
        public void DeferredOwnerCloseFreezesEditsAndFileCommandsButKeepsTheDialogHostInteractive()
        {
            using var fixture = new HostFixture();
            var host = fixture.Host;
            string saved = host.ProfileSession.ActiveProfile.DesignJson;
            host.SetHostClosing(true);
            Assert.IsFalse(host.Designer.IsEnabled);
            Assert.IsFalse(((WrapPanel)host.FindName("ProfileToolbar")).IsEnabled);
            Assert.IsTrue(((DialogHost)host.FindName("ProfileDialogHost")).IsEnabled,
                "Save/discard/cancel prompts must remain interactive while editor input is frozen.");
            Assert.IsTrue(host.TryCloseAsync().GetAwaiter().GetResult());
            Assert.IsFalse(host.Designer.IsEnabled, "Approval must not reopen an edit gap before the owner actually closes.");

            // Routed save requests must honor the same guard as disabled mouse controls.
            host.Designer.Document.Width += 40;
            fixture.Click("SaveProfileButton");
            Assert.AreEqual(saved, fixture.ReopenStore().Get(host.ProfileSession.ActiveProfile.Id).DesignJson);
            host.SetHostClosing(false);
            Assert.IsTrue(host.Designer.IsEnabled);
            Assert.IsTrue(fixture.Button("SaveProfileButton").IsEnabled);
            fixture.Click("SaveProfileButton");
            Assert.IsFalse(host.HasUnsavedChanges);
        }

        [TestMethod]
        public void RejectedProfileLoadCannotCopyThePreviousDocumentIntoTheNewProfile()
        {
            using var fixture = new HostFixture(twoProfiles: true);
            var host = fixture.Host;
            string previousDocument = host.Designer.GetTemplateJson();
            var next = fixture.ReopenStore().Profiles.Single(profile => profile.Name == "Second");
            // Force the editor's actual template-acceptance failure/rollback path without
            // requiring a native device failure in the test runner.
            EventHandler reject = (sender, args) =>
            {
                if (host.Designer.Document.Name == next.Name)
                    throw new FormatException("Test template rejection.");
            };
            host.Designer.TemplateChanged += reject;
            ((ComboBox)host.FindName("ProfileSelector")).SelectedValue = next.Id;
            host.Designer.TemplateChanged -= reject;

            Assert.AreEqual(next.Id, host.ProfileSession.ActiveProfile.Id);
            Assert.AreEqual(previousDocument, host.Designer.GetTemplateJson());
            Assert.IsFalse(host.Designer.IsEnabled);
            Assert.IsFalse(((WrapPanel)host.FindName("ProfileToolbar")).IsEnabled);
            Assert.AreEqual(Visibility.Visible, ((TextBlock)host.FindName("ProfileError")).Visibility);
            fixture.Click("SaveProfileButton");
            Assert.AreEqual(next.DesignJson, fixture.ReopenStore().Get(next.Id).DesignJson,
                "The previous accepted preview must never replace the newly selected saved profile.");
            host.SetHostClosing(true);
            host.SetHostClosing(false);
            Assert.IsFalse(host.Designer.IsEnabled, "Aborting shutdown must not clear a failed-load guard.");
            Assert.IsTrue(host.TryCloseAsync().GetAwaiter().GetResult(), "A rejected profile must still allow closing safely.");
            Assert.AreEqual(next.DesignJson, fixture.ReopenStore().Get(next.Id).DesignJson);
        }

        [TestMethod]
        public void NamePromptRejectsBlankAndDuplicateNamesBeforeConfirming()
        {
            var prompt = new OverlayDesignPrompt("Rename", "Name", "Save", "Existing",
                name => string.Equals(name, "Existing", StringComparison.OrdinalIgnoreCase) ? "Already exists" : null);
            var input = (TextBox)prompt.FindName("NameInput");
            var confirm = (Button)prompt.FindName("ConfirmButton");
            // Outside an open DialogHost its close command cannot execute. Isolate the
            // name-validation state here; an active dialog is required for command routing.
            confirm.Command = null;
            Assert.IsFalse(confirm.IsEnabled);
            input.Text = "   ";
            Assert.IsFalse(confirm.IsEnabled);
            input.Text = "  New design  ";
            Assert.IsTrue(confirm.IsEnabled);
            Assert.AreEqual("New design", prompt.EnteredName);
        }

        [TestMethod]
        public void UnreadableProfileLibraryPreservesItsBytesAndDisablesUntrackedEditing()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CapFrameX-DesignerHost-" + Guid.NewGuid().ToString("N"));
            string libraryFolder = Path.Combine(folder, "OverlayDesigns");
            Directory.CreateDirectory(libraryFolder);
            string path = Path.Combine(libraryFolder, "profiles.json");
            const string corruptJson = "This is a damaged library that must be preserved.";
            File.WriteAllText(path, corruptJson);
            try
            {
                var host = new OverlayDesignerControl();
                host.ConfigureProfiles(folder);
                Assert.IsNull(host.ProfileSession);
                Assert.IsFalse(host.Designer.IsEnabled,
                    "Without a working profile session the editor must not accept edits that its close guard cannot track.");
                Assert.AreEqual(Visibility.Visible, ((TextBlock)host.FindName("ProfileError")).Visibility);
                Assert.AreEqual(corruptJson, File.ReadAllText(path));
                host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                host.Designer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T found) yield return found;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        private static void DrainDispatcher()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        private static bool OpenWithDecision(HostFixture fixture, string profileId,
            OverlayDesignPromptResult decision, Action whilePromptOpen = null)
        {
            var dialog = (DialogHost)fixture.Host.FindName("ProfileDialogHost");
            bool prompted = false;
            DialogOpenedEventHandler respond = (_, args) =>
            {
                prompted = true;
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    whilePromptOpen?.Invoke();
                    args.Session.Close(decision);
                }));
            };
            dialog.DialogOpened += respond;
            dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                var operation = fixture.Host.OpenProfileAsync(profileId);
                var frame = new DispatcherFrame();
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timeout.Tick += (_, _) => frame.Continue = false;
                _ = operation.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                    TaskScheduler.FromCurrentSynchronizationContext());
                timeout.Start();
                if (!operation.IsCompleted) Dispatcher.PushFrame(frame);
                timeout.Stop();
                Assert.IsTrue(operation.IsCompleted, "The profile prompt did not complete.");
                Assert.IsTrue(prompted, "An unsaved document must use the existing Save/Discard/Cancel prompt.");
                return operation.GetAwaiter().GetResult();
            }
            finally
            {
                dialog.DialogOpened -= respond;
                dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        private sealed class DesignService : IOverlayDesignService
        {
            private EventHandler _changed;
            public int Subscribers { get; private set; }
            public int EnabledReads { get; private set; }
            public IReadOnlyList<OverlayDesignInfo> Profiles => Array.Empty<OverlayDesignInfo>();
            public string ActiveProfileId { get; private set; }
            public bool IsEnabled { get { EnabledReads++; return ActiveProfileId != null; } }
            public bool CanActivate { get; set; } = true;
            public string Status => ActiveProfileId == null ? "Classic" : "Active";
            public event EventHandler Changed
            {
                add { _changed += value; Subscribers++; }
                remove { _changed -= value; Subscribers--; }
            }
            public void RefreshProfiles() => _changed?.Invoke(this, EventArgs.Empty);
            public void Activate(string profileId) { ActiveProfileId = profileId; RefreshProfiles(); }
            public void Deactivate() { ActiveProfileId = null; RefreshProfiles(); }
            public void Delete(string profileId) { if (ActiveProfileId == profileId) Deactivate(); }
        }

        private sealed class HostFixture : IDisposable
        {
            public string Folder { get; } = Path.Combine(Path.GetTempPath(), "CapFrameX-DesignerHost-" + Guid.NewGuid().ToString("N"));
            public OverlayDesignerControl Host { get; }
            private readonly Border _surface;
            private readonly Size _size;

            public HostFixture(bool twoProfiles = false, BaseTheme theme = BaseTheme.Dark, int width = 1280, int height = 840)
            {
                _size = new Size(width, height);
                Directory.CreateDirectory(Folder);
                if (twoProfiles)
                {
                    var store = ReopenStore();
                    var first = store.Create("First", OsdDesignDocument.CreatePreset("Benchmark").ToJson());
                    store.Create("Second", OsdDesignDocument.CreatePreset("Minimal").ToJson());
                    store.SetActive(first.Id);
                }
                _surface = new Border();
                _surface.Resources.MergedDictionaries.Add(new CustomColorTheme
                {
                    BaseTheme = theme,
                    PrimaryColor = Color.FromRgb(2, 113, 249),
                    SecondaryColor = Colors.Lime
                });
                _surface.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
                });
                Host = new OverlayDesignerControl();
                Host.ConfigureProfiles(Folder);
                Assert.IsNotNull(Host.ProfileSession,
                    "Profiles must be initialized before showing the editor or awaiting hardware discovery, so early edits cannot be lost.");
                _surface.Child = Host;
                Arrange();
                // Exercise the production Loaded handler without creating an Application,
                // showing a window or loading a native graphics device in the test runner.
                Host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.IsNotNull(Host.ProfileSession, "The host must initialize its supplied configuration library.");
            }

            public OverlayDesignProfileStore ReopenStore()
                => new OverlayDesignProfileStore(Folder, json => OsdDesignDocument.FromJson(json).ToJson());

            public Button Button(string name) => (Button)Host.FindName(name);
            public void Click(string name) => Button(name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            public void Arrange()
            {
                _surface.Measure(_size);
                _surface.Arrange(new Rect(new Point(), _size));
                _surface.UpdateLayout();
            }

            public void Dispose()
            {
                Host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Host.Designer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                _surface.Child = null;
                Directory.Delete(Folder, true);
            }
        }
    }
}
