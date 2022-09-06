using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using LineSeries = CapFrameX.Statistics.PlotBuilder.LineSeries;

namespace CapFrameX.PMD
{
    public class PmdDataChartManager
    {
        private List<double> _ePS12VModelMaxYValueBuffer = new List<double>(10);
        private List<double> _pciExpressModelMaxYValueBuffer = new List<double>(10);

        private double _lastMinXAxis;
        private double _lastMaxXAxis;

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

        PlotModel _frametimeModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 60),
        };

        public bool UseDarkMode { get; set; }

        public PlotModel Eps12VModel => _eps12VModel;

        public PlotModel PciExpressModel => _pciExpressModel;

        public PlotModel CpuAnalysisModel => _cpuAnalysisModel;

        public PlotModel GpuAnalysisModel => _gpuAnalysisModel;

        public PlotModel FrametimeModel => _frametimeModel;

        public bool DrawFrametimes { get; set; } = true;

        public bool DrawPmdCpuPower { get; set; } = true;

        public bool DrawPmdGpuPower { get; set; } = true;

        public bool DrawSensorCpuPower { get; set; } = false;

        public bool DrawSensorGpuPower { get; set; } = false;


        public PmdDataChartManager()
        {
            // Metrics
            Eps12VModel.Axes.Add(AxisDefinitions["X_Axis_Time_CPU"]);
            Eps12VModel.Axes.Add(AxisDefinitions["Y_Axis_CPU_W"]);

            PciExpressModel.Axes.Add(AxisDefinitions["X_Axis_Time_GPU"]);
            PciExpressModel.Axes.Add(AxisDefinitions["Y_Axis_GPU_W"]);

            // Analysis
            CpuAnalysisModel.Axes.Add(AxisDefinitions["X_Axis_Time_CPU_Analysis"]);
            CpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_Analysis_CPU_W"]);

            GpuAnalysisModel.Axes.Add(AxisDefinitions["X_Axis_Time_GPU_Analysis"]);
            GpuAnalysisModel.Axes.Add(AxisDefinitions["Y_Axis_Analysis_GPU_W"]);

            FrametimeModel.Axes.Add(AxisDefinitions["X_Axis_Frame_Time"]);
            FrametimeModel.Axes.Add(AxisDefinitions["Y_Axis_Frame_Time"]);


            FrametimeModel.MouseUp += FrametimeModel_MouseUp;
            GpuAnalysisModel.MouseUp += GpuAnalysisModel_MouseUp;
            CpuAnalysisModel.MouseUp += CpuAnalysisModel_MouseUp;


        }


        private void CpuAnalysisModel_MouseUp(object sender, OxyMouseEventArgs e)
        {
            var min = AxisDefinitions["X_Axis_Time_CPU_Analysis"].ActualMinimum;
            var max = AxisDefinitions["X_Axis_Time_CPU_Analysis"].ActualMaximum;

            SynchronizeXAxes(min, max, "CPU");
        }

        private void GpuAnalysisModel_MouseUp(object sender, OxyMouseEventArgs e)
        {
            var min = AxisDefinitions["X_Axis_Time_GPU_Analysis"].ActualMinimum;
            var max = AxisDefinitions["X_Axis_Time_GPU_Analysis"].ActualMaximum;

            SynchronizeXAxes(min, max, "GPU");
        }

        private void FrametimeModel_MouseUp(object sender, OxyMouseEventArgs e)
        {
            var min = AxisDefinitions["X_Axis_Frame_Time"].ActualMinimum;
            var max = AxisDefinitions["X_Axis_Frame_Time"].ActualMaximum;

            SynchronizeXAxes(min, max, "Frametime");
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
                    Color = UseDarkMode ? OxyColors.White : OxyColors.Black,
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
                    Color = UseDarkMode ? OxyColors.White : OxyColors.Black,
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
                        MajorStep = 30,
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
                        MajorStep = 30,
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
                 { "Y_Axis_Frame_Time", new LinearAxis()
                    {
                        Key = "Y_Axis_Frame_Time",
                        Position = AxisPosition.Left,
                        Title = "Frametime [ms]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        MajorStep = 10,
                        Minimum = 0,
                        AbsoluteMinimum = 0,
                        AxisTitleDistance = 15
                    }
                 },
                 { "X_Axis_Frame_Time", new LinearAxis()
                    {
                        Key = "X_Axis_Frame_Time",
                        Position = AxisPosition.Bottom,
                        Title = "Time [s]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
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
                        AxisTitleDistance = 15
                    }
                 },
                 { "Y_Axis_Analysis_CPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_Analysis_CPU_W",
                        Position = AxisPosition.Left,
                        Title = "Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 30,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        AxisTitleDistance = 15
                    }
                },
                { "Y_Axis_Analysis_GPU_W", new LinearAxis()
                    {
                        Key = "Y_Axis_Analysis_GPU_W",
                        Position = AxisPosition.Left,
                        Title = "Power [W]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MajorStep = 30,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        Minimum = 0,
                        AxisTitleDistance = 15
                    }
                }
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

        public void UpdateChartsTheme()
        {
            var gridAndBorderColor = UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);
            var textColor = UseDarkMode ? OxyColors.White : OxyColors.Black;

            Eps12VModel.TextColor = textColor;
            CpuAnalysisModel.TextColor = textColor;
            GpuAnalysisModel.TextColor = textColor;
            PciExpressModel.TextColor = textColor;
            FrametimeModel.TextColor = textColor;

            Eps12VModel.PlotAreaBorderColor = gridAndBorderColor;
            PciExpressModel.PlotAreaBorderColor = gridAndBorderColor;
            CpuAnalysisModel.PlotAreaBorderColor = gridAndBorderColor;
            GpuAnalysisModel.PlotAreaBorderColor = gridAndBorderColor;
            FrametimeModel.PlotAreaBorderColor = gridAndBorderColor;

            AxisDefinitions["Y_Axis_CPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_GPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_CPU"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_GPU"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_CPU_Analysis"].MajorGridlineColor = gridAndBorderColor;            
            AxisDefinitions["Y_Axis_Analysis_CPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_GPU_Analysis"].MajorGridlineColor = gridAndBorderColor;            
            AxisDefinitions["Y_Axis_Analysis_GPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_Frame_Time"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Frame_Time"].MajorGridlineColor = gridAndBorderColor;

            Eps12VModel.InvalidatePlot(false);
            PciExpressModel.InvalidatePlot(false);
            CpuAnalysisModel.InvalidatePlot(false);
            GpuAnalysisModel.InvalidatePlot(false);
            FrametimeModel.InvalidatePlot(false);
        }

        public void UpdateCpuPowerChart(ISession session)
        {
            if (session == null) return;

            CpuAnalysisModel.Series.Clear();

            if (DrawPmdCpuPower)
            {
                var pmdCpuPowerPoints = session.GetPmdCpuPowerPoints();

                if (pmdCpuPowerPoints != null && pmdCpuPowerPoints.Any())
                {
                    var pmdCpuPowerDataPoints = pmdCpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
                    //var xMin = frametimeDataPoints.Min(pnt => pnt.X);
                    //var xMax = frametimeDataPoints.Max(pnt => pnt.X);

                    //AxisDefinitions["X_Axis_Time_CPU_Analysis"].Minimum = xMin;
                    //AxisDefinitions["X_Axis_Time_CPU_Analysis"].Maximum = xMax;

                    var pmdCpuPowerSeries = new LineSeries
                    {
                        Title = "Frametimes",
                        YAxisKey = "Y_Axis_Analysis_CPU_W",
                        StrokeThickness = 1,
                        LegendStrokeThickness = 4,
                        Color = Constants.PmdColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    pmdCpuPowerSeries.Points.AddRange(pmdCpuPowerDataPoints);
                    CpuAnalysisModel.Series.Add(pmdCpuPowerSeries);
                }
            }

            CpuAnalysisModel.InvalidatePlot(true);
        }

        public void UpdateGpuPowerChart(ISession session)
        {
            if (session == null) return;

            GpuAnalysisModel.Series.Clear();

            if (DrawPmdGpuPower)
            {
                var pmdGpuPowerPoints = session.GetPmdGpuPowerPoints();

                if (pmdGpuPowerPoints != null && pmdGpuPowerPoints.Any())
                {
                    var pmdGpuPowerDataPoints = pmdGpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
                    //var xMin = frametimeDataPoints.Min(pnt => pnt.X);
                    //var xMax = frametimeDataPoints.Max(pnt => pnt.X);

                    //AxisDefinitions["X_Axis_Time_GPU_Analysis"].Minimum = xMin;
                    //AxisDefinitions["X_Axis_Time_GPU_Analysis"].Maximum = xMax;

                    var pmdGpuPowerSeries = new LineSeries
                    {
                        Title = "Frametimes",
                        YAxisKey = "Y_Axis_Analysis_GPU_W",
                        StrokeThickness = 1,
                        LegendStrokeThickness = 4,
                        Color = Constants.PmdColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    pmdGpuPowerSeries.Points.AddRange(pmdGpuPowerDataPoints);
                    GpuAnalysisModel.Series.Add(pmdGpuPowerSeries);
                }
            }

            GpuAnalysisModel.InvalidatePlot(true);
        }

        public void UpdateFrametimeChart(ISession session)
        {
            if (session == null) return;

            FrametimeModel.Series.Clear();

            if (DrawFrametimes)
            {
                var frametimePoints = session.GetFrametimePoints();

                if (frametimePoints != null && frametimePoints.Any())
                {
                    var frametimeDataPoints = frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
                    var xMin = frametimePoints.Min(pnt => pnt.X);
                    var xMax = frametimePoints.Max(pnt => pnt.X);

                    AxisDefinitions["X_Axis_Frame_Time"].Minimum = xMin;
                    AxisDefinitions["X_Axis_Frame_Time"].Maximum = xMax;

                    AxisDefinitions["X_Axis_Time_CPU_Analysis"].Minimum = xMin;
                    AxisDefinitions["X_Axis_Time_CPU_Analysis"].Maximum = xMax;

                    AxisDefinitions["X_Axis_Time_GPU_Analysis"].Minimum = xMin;
                    AxisDefinitions["X_Axis_Time_GPU_Analysis"].Maximum = xMax;

                    var frametimeSeries = new LineSeries
                    {
                        Title = "Frametimes",
                        YAxisKey = "Y_Axis_Frame_Time",
                        StrokeThickness = 1,
                        LegendStrokeThickness = 4,
                        Color = Constants.FrametimeColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    frametimeSeries.Points.AddRange(frametimeDataPoints);
                    FrametimeModel.Series.Add(frametimeSeries);                   
                }
            }

            FrametimeModel.InvalidatePlot(true);
            CpuAnalysisModel.InvalidatePlot(true);
            GpuAnalysisModel.InvalidatePlot(true);
        }

        private void SynchronizeXAxes(double min, double max, string axis)
        {
            if (min == _lastMinXAxis && max == _lastMaxXAxis)
                return;

            AxisDefinitions["X_Axis_Time_GPU_Analysis"].Zoom(min, max);
            AxisDefinitions["X_Axis_Time_CPU_Analysis"].Zoom(min, max);
            AxisDefinitions["X_Axis_Frame_Time"].Zoom(min, max);

            CpuAnalysisModel.InvalidatePlot(false);
            GpuAnalysisModel.InvalidatePlot(false);
            FrametimeModel.InvalidatePlot(false);

            _lastMinXAxis = min;
            _lastMaxXAxis = max;
        }

    }
}
