using HelixToolkit.Wpf.SharpDX;
using Media3D = System.Windows.Media.Media3D;
using Media = System.Windows.Media;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

namespace CapFrameX.ViewModel
{
    public class Animation3DViewModel : Base3DViewModel
    {
        public ViewModel3D VM3D { get; } = new ViewModel3D();

        public Animation3DViewModel()
        {
            EffectsManager = new DefaultEffectsManager();
            RenderTechnique = EffectsManager[DefaultRenderTechniqueNames.Blinn];

            // titles
            Title = "CapFrameX";
            SubTitle = "Animated frame time test";

            // camera setup
            Camera = new PerspectiveCamera { Position = new Media3D.Point3D(8, 9, 7), LookDirection = new Media3D.Vector3D(-5, -12, -5), UpDirection = new Media3D.Vector3D(0, 1, 0) };
        }
    }
}
