using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// The markup text block is a replacement for <see cref="TextBlock"/> 
    /// that allows to specify markup content dynamically.
    /// </summary>
    [ContentProperty("MarkupText")]
    [Localizability(LocalizationCategory.Text)]
    public class MarkupTextBlock : TextBlock
    {
        /// <summary>
        /// The markup text property.
        /// </summary>
        public static readonly DependencyProperty MarkupTextProperty = DependencyProperty.Register(
            "MarkupText",
            typeof(string),
            typeof(MarkupTextBlock),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextMarkupChanged));

        private const string FlowDocumentPrefix =
            "<FlowDocument xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Paragraph><Span>";

        private const string FlowDocumentSuffix = "</Span></Paragraph></FlowDocument>";

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupTextBlock"/> class.
        /// </summary>
        /// <param name="markupText">
        /// The markup text.
        /// </param>
        public MarkupTextBlock(string markupText)
        {
            MarkupText = markupText;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupTextBlock"/> class.
        /// </summary>
        public MarkupTextBlock()
        {
        }

        /// <summary>
        /// Gets or sets content of the <see cref="MarkupTextBlock"/>.
        /// </summary>
        [Localizability(LocalizationCategory.Text)]
        public string MarkupText
        {
            get { return Inlines.ToString(); }
            set { SetValue(MarkupTextProperty, value); }
        }

        private static void OnTextMarkupChanged(
            DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var markupTextBlock = dependencyObject as MarkupTextBlock;
            if (markupTextBlock != null)
            {
                var flowDocument = new StringBuilder();
                flowDocument.Append(FlowDocumentPrefix);
                flowDocument.Append(dependencyPropertyChangedEventArgs.NewValue);
                flowDocument.Append(FlowDocumentSuffix);

                var document = (FlowDocument)XamlReader.Parse(flowDocument.ToString());
                var paragraph = document.Blocks.FirstBlock as Paragraph;
                if (paragraph != null)
                {
                    var inline = paragraph.Inlines.FirstInline;
                    if (inline != null)
                    {
                        paragraph.Inlines.Remove(inline);
                        markupTextBlock.Inlines.Clear();
                        markupTextBlock.Inlines.Add(inline);
                    }
                }
            }
        }
    }
}