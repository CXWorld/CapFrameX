﻿using CapFrameX.Data.Session.Contracts;
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

        private bool _gpuAxisChanging;
        private bool _cpuAxisChanging;
        private bool _frametimeAxisChanging;

        PlotModel _eps12VModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 20, 60),
        };

        PlotModel _pciExpressModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 20, 60),
        };

        PlotModel _cpuAnalysisModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 20, 60),
        };

        PlotModel _gpuAnalysisModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 20, 60),
        };

        PlotModel _performanceModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 20, 60),
        };

        public bool UseDarkMode { get; set; }

        public PlotModel Eps12VModel => _eps12VModel;

        public PlotModel PciExpressModel => _pciExpressModel;

        public PlotModel CpuAnalysisModel => _cpuAnalysisModel;

        public PlotModel GpuAnalysisModel => _gpuAnalysisModel;

        public PlotModel PerformanceModel => _performanceModel;

        public bool DrawPerformanceChart { get; set; } = true;

        public bool DrawPmdPower { get; set; } = true;

        public bool DrawAvgPmdPower { get; set; } = true;

        public bool DrawSensorPower { get; set; } = false;

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

            PerformanceModel.Axes.Add(AxisDefinitions["X_Axis_Performance"]);
            PerformanceModel.Axes.Add(AxisDefinitions["Y_Axis_Performance"]);

            AxisDefinitions["X_Axis_Time_GPU_Analysis"].AxisChanged += GPU_AxisChanged;
            AxisDefinitions["X_Axis_Time_CPU_Analysis"].AxisChanged += CPU_AxisChanged;
            AxisDefinitions["X_Axis_Performance"].AxisChanged += Performance_AxisChanged;
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
            AxisDefinitions["Y_Axis_GPU_W"].AbsoluteMaximum = y_Axis_GPU_W_Max > 0 ? y_Axis_GPU_W_Max : 300;

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
                 { "Y_Axis_Performance", new LinearAxis()
                    {
                        Key = "Y_Axis_Performance",
                        Position = AxisPosition.Left,
                        Title = "Frametime [ms]",
                        FontSize = 13,
                        MajorGridlineStyle = LineStyle.Solid,
                        MajorGridlineThickness = 1,
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        AbsoluteMinimum = 0,
                        AxisTitleDistance = 15
                    }
                 },
                 { "X_Axis_Performance", new LinearAxis()
                    {
                        Key = "X_Axis_Performance",
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
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        AbsoluteMinimum = 0,
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
                        MinorTickSize = 0,
                        MajorTickSize = 0,
                        AbsoluteMinimum = 0,
                        AxisTitleDistance = 15
                    }
                }
            };

        public void ResetRealTimePlotModels()
        {
            _ePS12VModelMaxYValueBuffer.Clear();
            _pciExpressModelMaxYValueBuffer.Clear();

            AxisDefinitions["Y_Axis_GPU_W"].Maximum = 300;

            AxisDefinitions["Y_Axis_CPU_W"].Maximum = 150;

            Eps12VModel.Series.Clear();
            Eps12VModel.ResetAllAxes();
            Eps12VModel.InvalidatePlot(true);
            PciExpressModel.Series.Clear();
            PciExpressModel.ResetAllAxes();
            PciExpressModel.InvalidatePlot(true);
        }

        public void ResetAnalysisPlotModels()
        {
            CpuAnalysisModel.ResetAllAxes();
            GpuAnalysisModel.ResetAllAxes();
            PerformanceModel.ResetAllAxes();
        }

        public void UpdateChartsTheme()
        {
            var gridAndBorderColor = UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);
            var textColor = UseDarkMode ? OxyColors.White : OxyColors.Black;

            Eps12VModel.TextColor = textColor;
            CpuAnalysisModel.TextColor = textColor;
            GpuAnalysisModel.TextColor = textColor;
            PciExpressModel.TextColor = textColor;
            PerformanceModel.TextColor = textColor;

            Eps12VModel.PlotAreaBorderColor = gridAndBorderColor;
            PciExpressModel.PlotAreaBorderColor = gridAndBorderColor;
            CpuAnalysisModel.PlotAreaBorderColor = gridAndBorderColor;
            GpuAnalysisModel.PlotAreaBorderColor = gridAndBorderColor;
            PerformanceModel.PlotAreaBorderColor = gridAndBorderColor;

            AxisDefinitions["Y_Axis_CPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_GPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_CPU"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_GPU"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_CPU_Analysis"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_Analysis_CPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Time_GPU_Analysis"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_Analysis_GPU_W"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["Y_Axis_Performance"].MajorGridlineColor = gridAndBorderColor;
            AxisDefinitions["X_Axis_Performance"].MajorGridlineColor = gridAndBorderColor;

            Eps12VModel.InvalidatePlot(false);
            PciExpressModel.InvalidatePlot(false);
            CpuAnalysisModel.InvalidatePlot(false);
            GpuAnalysisModel.InvalidatePlot(false);
            PerformanceModel.InvalidatePlot(false);
        }

        public void UpdateCpuPowerChart(ISession session)
        {
            if (session == null) return;

            CpuAnalysisModel.Series.Clear();

            double xAxisMax = double.NegativeInfinity;
            double yAxisMin = double.PositiveInfinity;
            double yAxisMax = double.NegativeInfinity;

            if (DrawPmdPower)
            {
                var pmdCpuPowerPoints = session.GetPmdPowerPoints("CPU");

                if (pmdCpuPowerPoints != null && pmdCpuPowerPoints.Any())
                {
                    var pmdCpuPowerDataPoints = pmdCpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

                    var pmdCpuPowerSeries = new LineSeries
                    {
                        Title = "CPU PMD Power",
                        YAxisKey = "Y_Axis_Analysis_CPU_W",
                        StrokeThickness = 1,
                        LegendStrokeThickness = 4,
                        Color = Constants.PmdColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var xMax = pmdCpuPowerDataPoints.Max(pnt => pnt.X);
                    var yMin = pmdCpuPowerDataPoints.Min(pnt => pnt.Y);
                    var yMax = pmdCpuPowerDataPoints.Max(pnt => pnt.Y);

                    SetAxisValues(xMax, yMin, yMax);

                    pmdCpuPowerSeries.Points.AddRange(pmdCpuPowerDataPoints);
                    CpuAnalysisModel.Series.Add(pmdCpuPowerSeries);


                }
            }

            if (DrawAvgPmdPower)
            {
                var avgPmdCpuPowerPoints = session.GetAveragePmdPowerPoints("CPU");

                if (avgPmdCpuPowerPoints != null && avgPmdCpuPowerPoints.Any())
                {
                    var avgPmdCpuPowerDataPoints = avgPmdCpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

                    var avgPmdCpuPowerSeries = new LineSeries
                    {
                        Title = "CPU Avg PMD Power",
                        YAxisKey = "Y_Axis_Analysis_CPU_W",
                        StrokeThickness = 2,
                        LegendStrokeThickness = 4,
                        Color = Constants.PmdMovingAverageColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var xMax = avgPmdCpuPowerDataPoints.Max(pnt => pnt.X);
                    var yMin = avgPmdCpuPowerDataPoints.Min(pnt => pnt.Y);
                    var yMax = avgPmdCpuPowerDataPoints.Max(pnt => pnt.Y);

                    SetAxisValues(xMax, yMin, yMax);

                    avgPmdCpuPowerSeries.Points.AddRange(avgPmdCpuPowerDataPoints);
                    CpuAnalysisModel.Series.Add(avgPmdCpuPowerSeries);
                }
            }

            if (DrawSensorPower)
            {
                var sensorCpuPowerPoints = session.GetSensorPowerPoints("CPU");

                if (sensorCpuPowerPoints != null && sensorCpuPowerPoints.Any())
                {
                    var sensorCpuPowerDataPoints = sensorCpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

                    var sensorCpuPowerSeries = new LineSeries
                    {
                        Title = "CPU Sensor Power",
                        YAxisKey = "Y_Axis_Analysis_CPU_W",
                        StrokeThickness = 2,
                        LegendStrokeThickness = 4,
                        Color = Constants.SensorColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var xMax = sensorCpuPowerDataPoints.Max(pnt => pnt.X);
                    var yMin = sensorCpuPowerDataPoints.Min(pnt => pnt.Y);
                    var yMax = sensorCpuPowerDataPoints.Max(pnt => pnt.Y);

                    SetAxisValues(xMax, yMin, yMax);

                    sensorCpuPowerSeries.Points.AddRange(sensorCpuPowerDataPoints);
                    CpuAnalysisModel.Series.Add(sensorCpuPowerSeries);
                }
            }

            void SetAxisValues(double xMax, double yMin, double yMax)
            {
                if (xAxisMax < xMax)
                    xAxisMax = xMax;
                if (yAxisMin > yMin)
                    yAxisMin = yMin;
                if (yAxisMax < yMax)
                    yAxisMax = yMax;
            }

            AxisDefinitions["Y_Axis_Analysis_CPU_W"].Maximum = yAxisMax + (yAxisMax - yAxisMin) / 6;
            AxisDefinitions["Y_Axis_Analysis_CPU_W"].Minimum = yAxisMin - (yAxisMax - yAxisMin) / 6;

            AxisDefinitions["X_Axis_Time_CPU_Analysis"].Maximum = xAxisMax;

            CpuAnalysisModel.ResetAllAxes();
            CpuAnalysisModel.InvalidatePlot(true);
        }

        public void UpdateGpuPowerChart(ISession session, bool useTBP)
        {
            if (session == null) return;

            GpuAnalysisModel.Series.Clear();

            double xAxisMax = double.NegativeInfinity;
            double yAxisMin = double.PositiveInfinity;
            double yAxisMax = double.NegativeInfinity;

            if (DrawPmdPower)
            {
                var pmdGpuPowerPoints = session.GetPmdPowerPoints("GPU");

                if (pmdGpuPowerPoints != null && pmdGpuPowerPoints.Any())
                {
                    var pmdGpuPowerDataPoints = pmdGpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

                    var pmdGpuPowerSeries = new LineSeries
                    {
                        Title = "GPU PMD Power",
                        YAxisKey = "Y_Axis_Analysis_GPU_W",
                        StrokeThickness = 1,
                        LegendStrokeThickness = 4,
                        Color = Constants.PmdColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var xMax = pmdGpuPowerDataPoints.Max(pnt => pnt.X);
                    var yMin = pmdGpuPowerDataPoints.Min(pnt => pnt.Y);
                    var yMax = pmdGpuPowerDataPoints.Max(pnt => pnt.Y);

                    SetAxisValues(xMax, yMin, yMax);

                    pmdGpuPowerSeries.Points.AddRange(pmdGpuPowerDataPoints);
                    GpuAnalysisModel.Series.Add(pmdGpuPowerSeries);
                }
            }

            if (DrawAvgPmdPower)
            {
                var avgPmdGpuPowerPoints = session.GetAveragePmdPowerPoints("GPU");

                if (avgPmdGpuPowerPoints != null && avgPmdGpuPowerPoints.Any())
                {
                    var avgPmdGpuPowerDataPoints = avgPmdGpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

                    var avgPmdGpuPowerSeries = new LineSeries
                    {
                        Title = "GPU Avg PMD Power",
                        YAxisKey = "Y_Axis_Analysis_GPU_W",
                        StrokeThickness = 2,
                        LegendStrokeThickness = 4,
                        Color = Constants.PmdMovingAverageColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var xMax = avgPmdGpuPowerDataPoints.Max(pnt => pnt.X);
                    var yMin = avgPmdGpuPowerDataPoints.Min(pnt => pnt.Y);
                    var yMax = avgPmdGpuPowerDataPoints.Max(pnt => pnt.Y);

                    SetAxisValues(xMax, yMin, yMax);

                    avgPmdGpuPowerSeries.Points.AddRange(avgPmdGpuPowerDataPoints);
                    GpuAnalysisModel.Series.Add(avgPmdGpuPowerSeries);
                }
            }

            if (DrawSensorPower)
            {
                var sensorGpuPowerPoints = session.GetSensorPowerPoints("GPU", useTBP);

                if (sensorGpuPowerPoints != null && sensorGpuPowerPoints.Any())
                {
                    var sensorGpuPowerDataPoints = sensorGpuPowerPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

                    var sensorGpuPowerSeries = new LineSeries
                    {
                        Title = "GPU Sensor Power",
                        YAxisKey = "Y_Axis_Analysis_GPU_W",
                        StrokeThickness = 2,
                        LegendStrokeThickness = 4,
                        Color = Constants.SensorColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var xMax = sensorGpuPowerDataPoints.Max(pnt => pnt.X);
                    var yMin = sensorGpuPowerDataPoints.Min(pnt => pnt.Y);
                    var yMax = sensorGpuPowerDataPoints.Max(pnt => pnt.Y);

                    SetAxisValues(xMax, yMin, yMax);


                    sensorGpuPowerSeries.Points.AddRange(sensorGpuPowerDataPoints);
                    GpuAnalysisModel.Series.Add(sensorGpuPowerSeries);
                }
            }

            void SetAxisValues(double xMax, double yMin, double yMax)
            {
                if (xAxisMax < xMax)
                    xAxisMax = xMax;
                if (yAxisMin > yMin)
                    yAxisMin = yMin;
                if (yAxisMax < yMax)
                    yAxisMax = yMax;
            }

            AxisDefinitions["Y_Axis_Analysis_GPU_W"].Maximum = yAxisMax + (yAxisMax - yAxisMin) / 6;
            AxisDefinitions["Y_Axis_Analysis_GPU_W"].Minimum = yAxisMin - (yAxisMax - yAxisMin) / 6;
            AxisDefinitions["X_Axis_Time_GPU_Analysis"].Maximum = xAxisMax;

            GpuAnalysisModel.InvalidatePlot(true);
        }

        public void UpdatePerformanceChart(ISession session, string metric = "Frametimes")
        {
            if (session == null) return;

            PerformanceModel.Series.Clear();

            if (DrawPerformanceChart)
            {
                var frametimePoints = session.GetFrametimePoints();

                if (frametimePoints != null && frametimePoints.Any())
                {

                    var performanceDataPoints = metric == "Frametimes" ? frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y)) : frametimePoints.Select(pnt => new DataPoint(pnt.X, 1000 / pnt.Y));
                    var xMin = frametimePoints.Min(pnt => pnt.X);
                    var xMax = frametimePoints.Max(pnt => pnt.X);

                    AxisDefinitions["X_Axis_Performance"].Minimum = xMin;
                    AxisDefinitions["X_Axis_Performance"].Maximum = xMax;

                    AxisDefinitions["X_Axis_Time_CPU_Analysis"].Minimum = xMin;
                    AxisDefinitions["X_Axis_Time_CPU_Analysis"].Maximum = xMax;

                    AxisDefinitions["X_Axis_Time_GPU_Analysis"].Minimum = xMin;
                    AxisDefinitions["X_Axis_Time_GPU_Analysis"].Maximum = xMax;

                    var performanceSeries = new LineSeries
                    {
                        Title = metric == "Frametimes" ? "Frametimes" : "FPS",
                        YAxisKey = "Y_Axis_Performance",
                        StrokeThickness = 2,
                        LegendStrokeThickness = 4,
                        Color = Constants.FrametimeColor,
                        EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                    };

                    var yAxisMin = performanceDataPoints.Min(pnt => pnt.Y);
                    var yAxisMax = performanceDataPoints.Max(pnt => pnt.Y);

                    AxisDefinitions["Y_Axis_Performance"].Title = metric == "Frametimes" ? "Frametimes [ms]" : "FPS";

                    AxisDefinitions["Y_Axis_Performance"].Maximum = yAxisMax + (yAxisMax - yAxisMin) / 6;
                    AxisDefinitions["Y_Axis_Performance"].Minimum = yAxisMin - (yAxisMax - yAxisMin) / 6;

                    performanceSeries.Points.AddRange(performanceDataPoints);
                    PerformanceModel.Series.Add(performanceSeries);
                }
            }

            PerformanceModel.InvalidatePlot(true);
            CpuAnalysisModel.InvalidatePlot(false);
            GpuAnalysisModel.InvalidatePlot(false);
        }


        private void GPU_AxisChanged(object sender, AxisChangedEventArgs e)
        {
            if (_frametimeAxisChanging || _cpuAxisChanging || !GpuAnalysisModel.Series.Any()) return;

            _gpuAxisChanging = true;

            var min = AxisDefinitions["X_Axis_Time_GPU_Analysis"].ActualMinimum;
            var max = AxisDefinitions["X_Axis_Time_GPU_Analysis"].ActualMaximum;

            AxisDefinitions["X_Axis_Time_CPU_Analysis"].Zoom(min, max);
            AxisDefinitions["X_Axis_Performance"].Zoom(min, max);

            CpuAnalysisModel.InvalidatePlot(false);
            PerformanceModel.InvalidatePlot(false);

            _gpuAxisChanging = false;
        }

        private void CPU_AxisChanged(object sender, AxisChangedEventArgs e)
        {
            if (_gpuAxisChanging || _frametimeAxisChanging || !CpuAnalysisModel.Series.Any()) return;

            _cpuAxisChanging = true;

            var min = AxisDefinitions["X_Axis_Time_CPU_Analysis"].ActualMinimum;
            var max = AxisDefinitions["X_Axis_Time_CPU_Analysis"].ActualMaximum;

            AxisDefinitions["X_Axis_Time_GPU_Analysis"].Zoom(min, max);
            AxisDefinitions["X_Axis_Performance"].Zoom(min, max);

            GpuAnalysisModel.InvalidatePlot(false);
            PerformanceModel.InvalidatePlot(false);

            _cpuAxisChanging = false;
        }

        private void Performance_AxisChanged(object sender, AxisChangedEventArgs e)
        {
            if (!DrawPerformanceChart || _gpuAxisChanging || _cpuAxisChanging) return;

            _frametimeAxisChanging = true;

            var min = AxisDefinitions["X_Axis_Performance"].ActualMinimum;
            var max = AxisDefinitions["X_Axis_Performance"].ActualMaximum;

            AxisDefinitions["X_Axis_Time_CPU_Analysis"].Zoom(min, max);
            AxisDefinitions["X_Axis_Time_GPU_Analysis"].Zoom(min, max);

            CpuAnalysisModel.InvalidatePlot(false);
            GpuAnalysisModel.InvalidatePlot(false);

            _frametimeAxisChanging = false;
        }
    }
}
