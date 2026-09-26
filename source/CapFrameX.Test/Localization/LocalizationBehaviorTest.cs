using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.MVVM.Converter;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using CapFrameX.PMD;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace CapFrameX.Test.Localization
{
    [STATestClass]
    [DoNotParallelize]
    public class LocalizationBehaviorTest
    {
        private string _uiLanguage;
        private string _overlayLanguage;

        [TestInitialize]
        public void Initialize()
        {
            _uiLanguage = CxLang.Instance.UiLanguage;
            _overlayLanguage = CxLang.Instance.OverlayLanguage;
            CxLang.Instance.SetUiLanguage("en");
            CxLang.Instance.SetOverlayLanguage("en");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CxLang.Instance.SetUiLanguage(_uiLanguage);
            CxLang.Instance.SetOverlayLanguage(_overlayLanguage);
            DrainBindings();
        }

        [TestMethod]
        public void ExistingTemplateInstances_RefreshWhenOnlyTheUiLanguageChanges()
        {
            var template = (DataTemplate)XamlReader.Parse("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:loc="clr-namespace:CapFrameX.MVVM.Localization;assembly=CapFrameX.MVVM"
                    xmlns:conv="clr-namespace:CapFrameX.MVVM.Converter;assembly=CapFrameX.MVVM">
                    <DataTemplate.Resources>
                        <conv:EnumDescriptionConverter x:Key="EnumDescriptionConverter" />
                    </DataTemplate.Resources>
                    <TextBlock Text="{loc:LanguageBinding {Binding Converter={StaticResource EnumDescriptionConverter}}}" />
                </DataTemplate>
                """);
            var first = (TextBlock)template.LoadContent();
            var second = (TextBlock)template.LoadContent();
            first.DataContext = EOverlayEntryType.CPU;
            second.DataContext = EOverlayEntryType.GPU;
            DrainBindings();
            string english = first.Text;

            CxLang.Instance.SetUiLanguage("ru");
            DrainBindings();

            Assert.AreEqual(CxLang.TranslateEnum(EOverlayEntryType.CPU), first.Text);
            Assert.AreEqual(CxLang.TranslateEnum(EOverlayEntryType.GPU), second.Text);
            Assert.AreNotEqual(english, first.Text);
            Assert.AreEqual(EOverlayEntryType.CPU, first.DataContext);
            BindingOperations.ClearAllBindings(first);
            BindingOperations.ClearAllBindings(second);
        }

        [TestMethod]
        public void GridColumnBinding_UsesOverlayLanguageAndPreservesItsDataSource()
        {
            var grid = (DataGrid)XamlReader.Parse("""
                <DataGrid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:loc="clr-namespace:CapFrameX.MVVM.Localization;assembly=CapFrameX.MVVM"
                    xmlns:conv="clr-namespace:CapFrameX.MVVM.Converter;assembly=CapFrameX.MVVM">
                    <DataGrid.Resources>
                        <conv:OverlayLabelConverter x:Key="OverlayLabelConverter" />
                    </DataGrid.Resources>
                    <DataGrid.Columns>
                        <DataGridTextColumn Binding="{loc:LanguageBinding {Binding GroupName, Converter={StaticResource OverlayLabelConverter}}, Overlay=True}" />
                    </DataGrid.Columns>
                </DataGrid>
                """);
            using var entry = new OverlayEntryWrapper("CpuPower") { GroupName = "CPU power" };
            var text = new TextBlock { DataContext = entry };
            BindingOperations.SetBinding(text, TextBlock.TextProperty, ((DataGridTextColumn)grid.Columns[0]).Binding);
            DrainBindings();
            Assert.AreEqual("CPU power", text.Text);
            CxLang.Instance.SetUiLanguage("ru");
            DrainBindings();
            Assert.AreEqual("CPU power", text.Text);
            CxLang.Instance.SetOverlayLanguage("ru");
            DrainBindings();
            Assert.AreEqual(CxLang.Instance.TranslateOverlay("CPU power"), text.Text);
            Assert.AreNotEqual("CPU power", text.Text);
            Assert.AreEqual("CPU power", entry.GroupName);
            BindingOperations.ClearAllBindings(text);
        }

        [TestMethod]
        public void SensorLabels_FollowUiLanguageIndependentlyOfOverlay()
        {
            var converter = new UiLabelConverter();
            string Convert() => (string)converter.Convert("CPU power", typeof(string), null, CultureInfo.InvariantCulture);
            CxLang.Instance.SetOverlayLanguage("ru");
            Assert.AreEqual("CPU power", Convert());
            CxLang.Instance.SetUiLanguage("ru");
            CxLang.Instance.SetOverlayLanguage("en");
            Assert.AreEqual(CxLang.Instance.TranslateUiText("CPU power"), Convert());
            Assert.AreNotEqual("CPU power", Convert());
        }

        [TestMethod]
        public void RefreshLocalization_DoesNotDirtyOrRewriteOverlayProfile()
        {
            using var entry = new OverlayEntryWrapper("CpuPower")
            {
                Description = "CPU power", GroupName = "CPU power", GroupNameFormat = "<C1>{0}<C>"
            };
            string persisted = JsonConvert.SerializeObject(entry);
            int changes = 0;
            entry.PropertyChangedAction = () => changes++;
            CxLang.Instance.SetOverlayLanguage("ru");
            entry.RefreshLocalization();
            Assert.AreEqual(0, changes);
            Assert.AreEqual(persisted, JsonConvert.SerializeObject(entry));
            Assert.AreNotEqual("<C1>CPU power<C>", entry.FormattedGroupName);
            entry.GroupName = "Custom group";
            Assert.AreEqual(1, changes, "Actual profile edits must still enable Save.");
        }

        [TestMethod]
        public void NativeOverlay_TranslatesGroupsAndPreservesRuntimePlaceholder()
        {
            CxLang.Instance.SetOverlayLanguage("ru");
            using var entry = new OverlayEntryWrapper("CpuPower")
            {
                Description = "CPU power", GroupName = "CPU power", ShowOnOverlay = true, Value = 12.5
            };
            var rendered = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();
            Assert.AreEqual(CxLang.Instance.TranslateOverlay("CPU power"), rendered.Group);
            Assert.AreNotEqual(entry.GroupName, rendered.Group);
            Assert.AreEqual(12.5, rendered.Value);
            entry.GroupName = "<APP>";
            Assert.AreEqual("<APP>", OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single().Group);
        }

        [DataTestMethod]
        [DataRow(1251, true)]
        [DataRow(65001, true)]
        [DataRow(1252, false)]
        public void RtssOutput_TranslatesOnlyWhenWindowsCanRepresentTheText(int codePage, bool supportsRussian)
        {
            CxLang.Instance.SetOverlayLanguage("ru");
            using var entry = new OverlayEntryWrapper("CaptureServiceStatus")
            {
                GroupName = "CPU power", GroupNameFormat = "<C1>{0}<C>",
                Value = "Ready to capture...", ValueFormat = "<S2><C3>{0}<C><S>"
            };
            string expectedGroup = supportsRussian ? CxLang.Instance.TranslateOverlay(entry.GroupName) : entry.GroupName;
            string expectedStatus = supportsRussian ? CxLang.Instance.TranslateOverlay((string)entry.Value) : (string)entry.Value;
            Assert.AreEqual("<C1>" + expectedGroup + "<C>", RtssTextFormatter.FormatGroupName(entry, codePage));
            Assert.AreEqual("<S2><C3>" + expectedStatus + "<C><S>", RtssTextFormatter.FormatValue(entry, codePage));
            Assert.AreEqual("<S2><C3>Ready to capture...<C><S>", entry.FormattedValue,
                "API and MCP formatted values must stay neutral.");
        }

        [TestMethod]
        public void RtssUnits_LeaveNumbersFormatTagsAndHardwareNamesIntact()
        {
            CxLang.Instance.SetOverlayLanguage("ru");
            using var entry = new OverlayEntryWrapper("CpuPower")
            {
                Value = 12.34, ValueFormat = "<S2><C3>{0,6:F1}<C><S><S0>W<C><S>"
            };
            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>" + CxLang.Instance.TranslateOverlay("W") + "<C><S>",
                RtssTextFormatter.FormatValue(entry, 1251));
            Assert.AreEqual(entry.FormattedValue, RtssTextFormatter.FormatValue(entry, 1252));
            entry.Value = "CPU Power Example 123";
            entry.ValueFormat = "<C3>{0}<C>";
            Assert.AreEqual(entry.FormattedValue, RtssTextFormatter.FormatValue(entry, 1251));
        }

        [TestMethod]
        public void PmdPerformanceAxis_PreservesMetricWhenLanguagesChange()
        {
            var manager = new PmdAnalysisChartManager();
            var session = new Session
            {
                Runs = new List<ISessionRun>
                {
                    new SessionRun
                    {
                        CaptureData = new SessionCaptureData(2)
                        {
                            TimeInSeconds = new[] { 0d, .01 }, MsBetweenPresents = new[] { 10d, 20d }
                        }
                    }
                }
            };
            manager.UpdatePerformanceChart(session, "FPS");
            CxLang.Instance.SetUiLanguage("ru");
            CxLang.Instance.SetOverlayLanguage("ru");
            Assert.AreEqual("FPS", manager.AxisDefinitions["Y_Axis_Performance"].Title);
            Assert.AreEqual("FPS", manager.PerformanceModel.Series.Single().Title);
            Assert.AreEqual(100d, ((OxyPlot.Series.LineSeries)manager.PerformanceModel.Series.Single()).Points[0].Y);
            manager.UpdatePerformanceChart(session);
            Assert.AreEqual(CxLang.T("PmdChart_FrametimeMs"), manager.AxisDefinitions["Y_Axis_Performance"].Title);
            Assert.AreEqual(10d, ((OxyPlot.Series.LineSeries)manager.PerformanceModel.Series.Single()).Points[0].Y);
            CxLang.Instance.SetUiLanguage("en");
            Assert.AreEqual(CxLang.T("PmdChart_FrametimeMs"), manager.AxisDefinitions["Y_Axis_Performance"].Title);
        }

        private static void DrainBindings()
            => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
