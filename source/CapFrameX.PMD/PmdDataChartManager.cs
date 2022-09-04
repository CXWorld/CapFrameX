using CapFrameX.Data.Session.Contracts;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace CapFrameX.PMD
{
    public class PmdDataChartManager
    {
        private List<double> _ePS12VModelMaxYValueBuffer = new List<double>(10);
        private List<double> _pciExpressModelMaxYValueBuffer = new List<double>(10);       

        PlotModel _eps12VModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 60),
        };

        PlotModel _pciExpressModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 60),
        };

        PlotModel _cpuAnalysisModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 60),
        };

        PlotModel _gpuAnalysisModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 60),
        };

        public bool UseDarkMode { get; set; }

        public PlotModel Eps12VModel => _eps12VModel;

        public PlotModel PciExpressModel => _pciExpressModel;

        public PlotModel CpuAnalysisModel => _cpuAnalysisModel;

        public PlotModel GpuAnalysisModel => _gpuAnalysisModel;

        public PmdDataChartManager()
        {
            // Metrics
            Eps12VModel.Axes.Add(AxisDefinitions["X_Axis_Time_CPU"]);
            Eps12VModel.Axes.Add(AxisDefinitions["Y_Axis_CPU_W"]);

            PciExpressModel.Axes.Add(AxisDefinitions["X_Axis_Time_GPU"]);
            PciExpressModel.Axes.Add(AxisDefinitions["Y_Axis_GPU_W"]);

            // Analysis
            CpuAnalysisModel.Axes.Add(AxisDefinitions["X_Axis_Time_CPU_Analysis"]);
            CpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_Frame_Time_CPU"]);
            CpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_PMD_CPU_W"]);
            CpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_Sensor_CPU_W"]);

            GpuAnalysisModel.Axes.Add(AxisDefinitions["X_Axis_Time_GPU_Analysis"]);
            GpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_Frame_Time_GPU"]);
            GpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_PMD_GPU_W"]);
            GpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_Sensor_GPU_W"]);
        }

        public void DrawEps12VChart(IEnumerable<DataPoint> powerDrawPoints)
        {
            if (!powerDrawPoints.Any()) return;

            // Set maximum y-axis
            if (_ePS12VModelMaxYValueBuffer.Count == 10) _ePS12VModelMaxYValueBuffer.RemoveAt(0);
            _ePS12VModelMaxYValueBuffer.Add((int)Math.Ceiling(1.05 * powerDrawPoints.Max(pnt => pnt.Y) / 20.0) * 20);
            var y_Axis_CPU_W_Max = _ePS12VModelMaxYValueBuffer.Max();
            AxisDefinitions["Y_Axis_CPU_W"].Maximum = y_Axis_CPU_W_Max;
            AxisDefinitions["Y_Axis_CPU_W"].AbsoluteMaximum = y_Axis_CPU_W_Max;

            Eps12VModel.Series.Clear();
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                var eps12VPowerSeries = new LineSeries
                {
                    Title = "CPU (Sum EPS 12V)",
                    StrokeThickness = 1,
                    Color = OxyColors.Black,
                    EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                };

                eps12VPowerSeries.Points.AddRange(powerDrawPoints);
                Eps12VModel.Series.Add(eps12VPowerSeries);
                Eps12VModel.InvalidatePlot(true);
            });
        }

        public void DrawPciExpressChart(IEnumerable<DataPoint> powerDrawPoints)
        {
            if (!powerDrawPoints.Any()) return;

            // Set maximum y-axis
            if (_pciExpressModelMaxYValueBuffer.Count == 10) _pciExpressModelMaxYValueBuffer.RemoveAt(0);
            _pciExpressModelMaxYValueBuffer.Add((int)Math.Ceiling(1.05 * powerDrawPoints.Max(pnt => pnt.Y) / 20.0) * 20);
            var y_Axis_GPU_W_Max = _pciExpressModelMaxYValueBuffer.Max();
            AxisDefinitions["Y_Axis_GPU_W"].Maximum = y_Axis_GPU_W_Max;
            AxisDefinitions["Y_Axis_GPU_W"].AbsoluteMaximum = y_Axis_GPU_W_Max;

            PciExpressModel.Series.Clear();
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                var pciExpressPowerSeries = new LineSeries
                {
                    Title = "GPU (Sum PCI Express)",
                    StrokeThickness = 1,
                    Color = OxyColors.Black,
                    EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                };

                pciExpressPowerSeries.Points.AddRange(powerDrawPoints);
                PciExpressModel.Series.Add(pciExpressPowerSeries);
                PciExpressModel.InvalidatePlot(true);
            });
        }

        public Dictionary<string, LinearAxis> AxisDefinitions { get; }
            = new Dictionary<string, LinearAxis>() {
                // Metrics tab
                { "Y_Axis_CPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_CPU_W",
                        Position = AxisPosition.Left,
                        Title = "CPU Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,                   
                        MajorStep = 25,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 150,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 150,
                        AxisTitleDistance = 15
                    }
                },
                { "Y_Axis_GPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_GPU_W",
                        Position = AxisPosition.Left,
                        Title = "GPU Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 25,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 300,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 300,
                        AxisTitleDistance = 15
                    }
                },
                { "X_Axis_Time_CPU", new LinearAxis()
                    {
                        Key = "X_Axis_Time_CPU",
                        Position = AxisPosition.Bottom,
                        Title = "Time [s]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 10,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 10,
                        AxisTitleDistance = 15
                    }
                },
                { "X_Axis_Time_GPU", new LinearAxis()
                    {
                        Key = "X_Axis_Time_GPU",
                        Position = AxisPosition.Bottom,
                        Title = "Time [s]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 10,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 10,
                        AxisTitleDistance = 15
                    }
                 },
                 // Analysis tab
                 { "Y_Axis_Frame_Time_CPU", new LinearAxis()
                    {
                        Key = "Y_Axis_Frame_Time_CPU",
                        Position = AxisPosition.Right,
                        Title = "Frametime [ms]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
						MajorStep = 10,
                        Minimum = 0,
                        Maximum = 50,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 50,
                        AxisTitleDistance = 15
                    }
                 },
                 { "Y_Axis_Frame_Time_GPU", new LinearAxis()
                    {
                        Key = "Y_Axis_Frame_Time_GPU",
                        Position = AxisPosition.Right,
                        Title = "Frametime [ms]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
						MajorStep = 10,
                        Minimum = 0,
                        Maximum = 50,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 50,
                        AxisTitleDistance = 15
                    }
                 },
                 { "X_Axis_Time_CPU_Analysis", new LinearAxis()
                    {
                        Key = "X_Axis_Time_CPU_Analysis",
                        Position = AxisPosition.Bottom,
                        Title = "Time [s]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 60,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 60,
                        AxisTitleDistance = 15
                    }
                 },
                 { "X_Axis_Time_GPU_Analysis", new LinearAxis()
                    {
                        Key = "X_Axis_Time_GPU_Analysis",
                        Position = AxisPosition.Bottom,
                        Title = "Time [s]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 60,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 60,
                        AxisTitleDistance = 15
                    }
                 },
                 // PMD data
                 { "Y_Axis_PMD_CPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_PMD_CPU_W",
                        Position = AxisPosition.Left,
                        Title = "CPU Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 25,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 150,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 150,
                        AxisTitleDistance = 15
                    }
                },
                { "Y_Axis_PMD_GPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_PMD_GPU_W",
                        Position = AxisPosition.Left,
                        Title = "GPU Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 25,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 300,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 300,
                        AxisTitleDistance = 15
                    }
                },
                // Sensor readings data
                { "Y_Axis_Sensor_CPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_Sensor_CPU_W",
                        Position = AxisPosition.Left,
                        Title = "CPU Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 25,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 150,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 150,
                        AxisTitleDistance = 15
                    }
                },
                { "Y_Axis_Sensor_GPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_Sensor_GPU_W",
                        Position = AxisPosition.Left,
                        Title = "GPU Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 25,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        Maximum = 300,
                        AbsoluteMinimum = 0,
                        AbsoluteMaximum = 300,
                        AxisTitleDistance = 15
                    }
                },
            };

        public void ResetAllPLotModels()
        {
            _ePS12VModelMaxYValueBuffer.Clear();
            _pciExpressModelMaxYValueBuffer.Clear();

            AxisDefinitions["Y_Axis_GPU_W"].Maximum = 300;
            AxisDefinitions["Y_Axis_GPU_W"].AbsoluteMaximum = 300;

            AxisDefinitions["Y_Axis_CPU_W"].Maximum = 150;
            AxisDefinitions["Y_Axis_CPU_W"].AbsoluteMaximum = 150;

            Eps12VModel.Series.Clear();
            Eps12VModel.ResetAllAxes();
            Eps12VModel.InvalidatePlot(true);
            PciExpressModel.Series.Clear();
            PciExpressModel.ResetAllAxes();
            PciExpressModel.InvalidatePlot(true);
        }

        public void UpdateCharts()
        {
            var gridAndBorderColor = UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);
            var textColor = UseDarkMode ? OxyColors.White : OxyColors.Black;

            Eps12VModel.TextColor = textColor;
            Eps12VModel.PlotAreaBorderColor = gridAndBorderColor;

            PciExpressModel.TextColor = textColor;
            PciExpressModel.PlotAreaBorderColor = gridAndBorderColor;

            AxisDefinitions["Y_Axis_CPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_GPU_W"].MajorGridlineColor = gridAndBorderColor;

            AxisDefinitions["X_Axis_Time_CPU"].MajorGridlineColor = gridAndBorderColor;           
            AxisDefinitions["X_Axis_Time_GPU"].MajorGridlineColor = gridAndBorderColor;

            Eps12VModel.InvalidatePlot(false);
            PciExpressModel.InvalidatePlot(false);
        }

        public void UpdatePowerFramtimesChart(ISession session)
        {
            if (session == null) return;


        }
    }
}
