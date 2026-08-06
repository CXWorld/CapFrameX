using OxyPlot;
using OxyPlot.Annotations;
using System.Linq;

namespace CapFrameX.Statistics.PlotBuilder
{
    /// <summary>
    /// Draws one series a second time on top of all others. Annotations on
    /// <see cref="AnnotationLayer.AboveSeries"/> are rendered after every series, so this brings a
    /// curve to the front without reordering <see cref="PlotModel.Series"/> - that collection also
    /// defines the order of the legend entries, and moving a series inside it makes the legend
    /// jump and decouples it from the record list.
    /// </summary>
    public class SeriesHighlightAnnotation : Annotation
    {
        public SeriesHighlightAnnotation()
        {
            Layer = AnnotationLayer.AboveSeries;
        }

        /// <summary>
        /// The series to repeat on top. It keeps its own position in the series collection and is
        /// therefore drawn twice; with opaque colors both passes are identical.
        /// </summary>
        public OxyPlot.Series.Series Series { get; set; }

        public override void Render(IRenderContext rc)
        {
            var series = Series;

            // The series collection is rebuilt behind our back (tab switches, range slider, color
            // changes). Rendering a series that is no longer part of the model would leave a ghost
            // curve on the plot, so the highlight simply goes quiet instead.
            if (series == null || PlotModel == null || !PlotModel.Series.Contains(series))
                return;

            series.Render(rc);
        }

        /// <summary>
        /// Makes <paramref name="series"/> the single highlighted series of the model, or removes
        /// the highlight when it is <c>null</c>.
        /// </summary>
        public static void SetHighlight(PlotModel plotModel, OxyPlot.Series.Series series)
        {
            if (plotModel == null)
                return;

            Clear(plotModel);

            if (series != null)
                plotModel.Annotations.Add(new SeriesHighlightAnnotation { Series = series });
        }

        public static void Clear(PlotModel plotModel)
        {
            if (plotModel == null)
                return;

            var highlights = plotModel.Annotations.OfType<SeriesHighlightAnnotation>().ToList();

            foreach (var highlight in highlights)
                plotModel.Annotations.Remove(highlight);
        }
    }
}
