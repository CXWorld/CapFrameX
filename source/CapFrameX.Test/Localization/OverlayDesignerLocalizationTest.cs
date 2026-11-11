using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Localization;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.View.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Localization
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerLocalizationTest
    {
        private static readonly string[] Languages = { "en", "de", "es", "ru", "ko" };
        private string _originalUiLanguage;
        private string _originalEditorLanguage;

        [TestInitialize]
        public void Initialize()
        {
            _originalUiLanguage = CxLang.Instance.UiLanguage;
            _originalEditorLanguage = OsdLocalization.Instance.Language;
            CxLang.Instance.SetUiLanguage("en");
            OsdLocalization.SetLanguage("en");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CxLang.Instance.SetUiLanguage(_originalUiLanguage);
            OsdLocalization.SetLanguage(_originalEditorLanguage);
            DrainBindings();
        }

        [TestMethod]
        public void EmbeddedEditorAndTemplateCatalogsCoverAllLanguagesAndPlaceholders()
        {
            foreach (string family in new[] { "Editor", "Templates" })
            {
                var english = ReadCatalog(family, "en");
                Assert.IsTrue(english.Count > 0);
                foreach (string language in Languages)
                {
                    var translated = ReadCatalog(family, language);
                    CollectionAssert.AreEquivalent(english.Properties().Select(p => p.Name).ToArray(),
                        translated.Properties().Select(p => p.Name).ToArray(), family + "." + language);
                    foreach (var entry in english.Properties())
                    {
                        string value = (string)translated[entry.Name];
                        Assert.IsFalse(string.IsNullOrWhiteSpace(value), language + ": " + entry.Name);
                        CollectionAssert.AreEquivalent(Placeholders((string)entry.Value), Placeholders(value),
                            language + ": " + entry.Name);
                    }
                }
            }
        }

        [TestMethod]
        public void EveryGalleryTemplateHasTranslatedMetadataWithoutChangingItsDocument()
        {
            var original = OsdDesignPresets.All.ToDictionary(preset => preset.Id, preset => new
            {
                preset.Name,
                preset.Category,
                preset.Description,
                Json = preset.CreateDocument().ToJson()
            });
            Assert.AreEqual(68, original.Count);

            foreach (string language in Languages)
            {
                OsdLocalization.SetLanguage(language);
                foreach (var preset in OsdDesignPresets.All)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(preset.DisplayName), preset.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(preset.DisplayCategory), preset.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(preset.DisplayDescription), preset.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(preset.DisplaySummary), preset.Id);
                    if (language != "en")
                        Assert.AreNotEqual(original[preset.Id].Description, preset.DisplayDescription,
                            language + ": untranslated template " + preset.Id);
                    Assert.AreEqual(original[preset.Id].Name, preset.Name);
                    Assert.AreEqual(original[preset.Id].Category, preset.Category);
                    Assert.AreEqual(original[preset.Id].Description, preset.Description);
                    Assert.AreEqual(original[preset.Id].Json, preset.CreateDocument().ToJson(),
                        "Translating gallery metadata must not rewrite default names, labels, or overlay text.");
                    Assert.AreSame(preset, OsdDesignPresets.Find(preset.Id));
                    Assert.AreSame(preset, OsdDesignPresets.Find(preset.Name));
                }
            }
        }

        [TestMethod]
        public void InspectorSourceSearchNamesItsPurposeUntilSomethingIsTyped()
        {
            var editor = new OsdEditorControl();
            var surface = Surface(editor);
            try
            {
                var document = new OsdDesignDocument { Name = "Search", Width = 640 };
                document.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS" });
                editor.SetTemplateJson(document.ToJson());
                Arrange(surface);
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var search = (TextBox)editor.FindName("InspectorSourceSearch");
                var hint = (TextBlock)editor.FindName("InspectorSourceSearchHint");
                DrainBindings();

                // The field filters the source picker; empty, it must not read as a missing tile source.
                Assert.AreEqual(Visibility.Visible, hint.Visibility);
                Assert.IsFalse(string.IsNullOrWhiteSpace(hint.Text));
                Assert.AreEqual(search.ToolTip, hint.Text);
                search.SetCurrentValue(TextBox.TextProperty, "gpu");
                DrainBindings();
                Assert.AreEqual(Visibility.Collapsed, hint.Visibility, "Typed text replaces the hint.");
                search.SetCurrentValue(TextBox.TextProperty, "");
                DrainBindings();
                Assert.AreEqual(Visibility.Visible, hint.Visibility);
            }
            finally
            {
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                surface.Child = null;
            }
        }

        [TestMethod]
        public void SwitchingLanguagePreservesPendingTextDocumentAndSourceFilter()
        {
            var editor = new OsdEditorControl();
            var surface = Surface(editor);
            try
            {
                var document = new OsdDesignDocument { Name = "Save", Width = 640 };
                document.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "Templates", Key = "fps", Unit = "FPS" });
                document.Tiles.Add(new OsdDesignTile { Kind = "text", Label = "Sources", Text = "Ready" });
                editor.SetTemplateJson(document.ToJson());
                editor.Sources = new[]
                {
                    new OsdTelemetrySource { Key = "fps", Name = "Templates", Category = "Performance", DeviceName = "General", Unit = "FPS" },
                    new OsdTelemetrySource { Key = "cpu", Name = "My CPU", Category = "Processor", DeviceName = "My hardware", Unit = "%" }
                };
                var sourceTab = (TabItem)editor.FindName("SourcesTab");
                sourceTab.IsSelected = true;
                Arrange(surface);
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var category = (ComboBox)editor.FindName("SourceCategoryFilter");
                category.SelectedItem = "Performance";
                var sourceList = (ListBox)editor.FindName("SourceList");
                Assert.AreEqual(1, sourceList.Items.Count);
                var label = LogicalDescendants<TextBox>((TabControl)editor.FindName("InspectorTabs"))
                    .Single(field => field.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path == "SelectedTile.Label");
                label.SetCurrentValue(TextBox.TextProperty, "Mein eigener Text · Мой текст · 내 텍스트");
                DrainBindings();
                Assert.IsTrue(editor.HasPendingTextEdits);
                string json = editor.GetTemplateJson();
                int changed = 0;
                editor.DocumentEdited += (_, _) => changed++;

                foreach (string language in new[] { "de", "es", "ru", "ko", "en" })
                {
                    OsdLocalization.SetLanguage(language);
                    DrainBindings();
                    Arrange(surface);
                    Assert.AreEqual("Performance", category.SelectedItem, "A display translation must not change the filter identity.");
                    Assert.AreEqual(1, sourceList.Items.Count);
                    var source = (OsdTelemetrySource)sourceList.Items[0];
                    Assert.AreEqual("fps", source.Key);
                    Assert.AreEqual("Templates", source.Name, "User/source names that match UI words are not UI labels.");
                    Assert.AreEqual("General", source.DeviceName);
                    Assert.AreEqual("Mein eigener Text · Мой текст · 내 텍스트", label.Text);
                    Assert.IsTrue(editor.HasPendingTextEdits);
                    Assert.AreEqual(json, editor.GetTemplateJson());
                    Assert.AreEqual(0, changed, "Language changes must not edit the document or add undo steps.");
                }
            }
            finally
            {
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                surface.Child = null;
            }
        }

        [TestMethod]
        public void HostLanguageChangesUpdateOpenControlsWithoutRenamingOrSavingProfiles()
        {
            string folder = NewFolder();
            var host = new OverlayDesignerControl();
            var surface = Surface(host);
            try
            {
                var store = new OverlayDesignProfileStore(folder, OverlayRuntimeDesign.Canonicalize);
                var document = OsdDesignDocument.CreatePreset("Minimal");
                document.Name = "Save";
                document.Tiles[0].Label = "Templates";
                var profile = store.Create("Save", document.ToJson());
                store.SetActive(profile.Id);
                host.ConfigureProfiles(folder);
                Arrange(surface);
                host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                host.Designer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                DrainBindings();
                string path = Path.Combine(folder, "OverlayDesigns", "profiles.json");
                string bytes = File.ReadAllText(path);
                string json = host.Designer.GetTemplateJson();
                var save = (Button)host.FindName("SaveProfileButton");
                string english = save.Content.ToString();

                foreach (string language in new[] { "de", "es", "ru", "ko" })
                {
                    CxLang.Instance.SetUiLanguage(language);
                    DrainBindings();
                    Assert.AreEqual(language, OsdLocalization.Instance.Language);
                    Assert.AreNotEqual(english, save.Content.ToString());
                    Assert.AreEqual("Save", host.ProfileSession.ActiveProfile.Name);
                    Assert.AreEqual(json, host.Designer.GetTemplateJson());
                    Assert.AreEqual(bytes, File.ReadAllText(path));
                    Assert.IsFalse(host.HasUnsavedChanges);
                }
            }
            finally
            {
                host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                host.Designer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                surface.Child = null;
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void ActiveDesignStatusTracksLanguageWithoutChangingRuntimeSelectionOrFiles()
        {
            string folder = NewFolder();
            try
            {
                var configuration = new Mock<IAppConfiguration>();
                configuration.SetupAllProperties();
                configuration.Object.EnableHookOverlay = true;
                configuration.SetupGet(c => c.OnValueChanged).Returns(Observable.Never<(string key, object value)>());
                var store = new OverlayDesignProfileStore(folder, OverlayRuntimeDesign.Canonicalize);
                var profile = store.Create("Save", OsdDesignDocument.CreatePreset("Minimal").ToJson());
                using var service = new OverlayDesignService(folder, configuration.Object);
                service.Activate(profile.Id);
                string englishStatus = service.Status;
                string path = Path.Combine(folder, "OverlayDesigns", "profiles.json");
                string bytes = File.ReadAllText(path);
                int changes = 0;
                service.Changed += (_, _) => changes++;
                foreach (string language in new[] { "de", "es", "ru", "ko" })
                {
                    CxLang.Instance.SetUiLanguage(language);
                    Assert.AreNotEqual(englishStatus, service.Status);
                    StringAssert.Contains(service.Status, "Save");
                    Assert.AreEqual(profile.Id, service.ActiveProfileId);
                    Assert.AreEqual(profile.Id, configuration.Object.LastOverlayDesignProfileId);
                    Assert.AreEqual(bytes, File.ReadAllText(path));
                }
                Assert.IsTrue(changes >= 4, "Open status views need notifications when the UI language changes.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void InvalidDesignErrorsAreTranslatedWithoutChangingTheRejectedInput()
        {
            var document = OsdDesignDocument.CreatePreset("Minimal");
            document.Name = "";
            var error = Assert.ThrowsExactly<FormatException>(() => document.ToJson());
            string english = error.Message;
            foreach (string language in new[] { "de", "es", "ru", "ko" })
            {
                OsdLocalization.SetLanguage(language);
                string translated = Assert.ThrowsExactly<FormatException>(() => document.ToJson()).Message;
                Assert.AreNotEqual(english, translated);
                Assert.IsFalse(string.IsNullOrWhiteSpace(translated));
                Assert.AreEqual(translated, OverlayDesignText.ExceptionMessage(error),
                    "The host must retranslate existing errors from the reusable editor too.");
                Assert.AreEqual("", document.Name);
            }
        }

        [TestMethod]
        [DataRow("en")]
        [DataRow("de")]
        [DataRow("es")]
        [DataRow("ru")]
        [DataRow("ko")]
        public void TranslatedSidebarTabsFitAtTheMinimumEditorWidth(string language)
        {
            OsdLocalization.SetLanguage(language);
            var editor = new OsdEditorControl();
            var surface = Surface(editor);
            try
            {
                surface.Measure(new Size(1080, 560));
                surface.Arrange(new Rect(0, 0, 1080, 560));
                surface.UpdateLayout();
                DrainBindings();
                foreach (var tabs in VisualDescendants<TabControl>(editor))
                {
                    foreach (TabItem tab in tabs.Items)
                    {
                        Rect bounds = tab.TransformToAncestor(tabs).TransformBounds(new Rect(tab.RenderSize));
                        Assert.IsTrue(tab.ActualWidth > 0 && tab.ActualHeight > 0, language + ": " + tab.Header);
                        Assert.IsTrue(bounds.Left >= -0.5 && bounds.Right <= tabs.ActualWidth + 0.5,
                            language + ": clipped tab " + tab.Header);
                        var header = VisualDescendants<ContentPresenter>(tab)
                            .FirstOrDefault(presenter => Equals(presenter.Content, tab.Header));
                        Assert.IsNotNull(header, language + ": " + tab.Header);
                        Assert.IsTrue(header.ActualWidth + header.Margin.Left + header.Margin.Right + 0.5 >= header.DesiredSize.Width,
                            language + ": truncated tab label " + tab.Header);
                    }
                }
            }
            finally
            {
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                surface.Child = null;
            }
        }

        private static JObject ReadCatalog(string family, string language)
        {
            var assembly = typeof(OsdLocalization).Assembly;
            string resource = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith("." + family + "." + language + ".json", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(resource);
            using var reader = new StreamReader(stream);
            return JObject.Parse(reader.ReadToEnd(), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }

        private static string[] Placeholders(string text) => Regex.Matches(text, @"\{\d+(?:[,:][^}]*)?\}")
            .Select(match => match.Value).Distinct().OrderBy(value => value).ToArray();

        private static string NewFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CapFrameX-EditorLocalization-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static Border Surface(UIElement child)
        {
            var surface = new Border();
            TransitionAssist.SetDisableTransitions(surface, true);
            surface.Resources.MergedDictionaries.Add(new CustomColorTheme
            {
                BaseTheme = BaseTheme.Dark,
                PrimaryColor = System.Windows.Media.Color.FromRgb(2, 113, 249),
                SecondaryColor = System.Windows.Media.Colors.Lime
            });
            surface.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            });
            surface.Child = child;
            return surface;
        }

        private static void Arrange(FrameworkElement element)
        {
            element.Measure(new Size(1280, 900));
            element.Arrange(new Rect(0, 0, 1280, 900));
            element.UpdateLayout();
        }

        private static void DrainBindings() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

        private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (object item in LogicalTreeHelper.GetChildren(parent))
            {
                if (item is not DependencyObject child) continue;
                if (child is T match) yield return match;
                foreach (var descendant in LogicalDescendants<T>(child)) yield return descendant;
            }
        }

        private static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match) yield return match;
                foreach (var descendant in VisualDescendants<T>(child)) yield return descendant;
            }
        }
    }
}
