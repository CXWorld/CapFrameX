using HelixToolkit.Wpf.SharpDX;
using Prism.Mvvm;
using SharpDX;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

namespace CapFrameX.ViewModel
{
    public class ViewModel3D : BindableBase
    {
        public MeshGeometry3D Model { set; get; }

        public PhongMaterial ModelMaterial { set; get; } = PhongMaterials.Black;

        public Vector3D Light1Direction { get; set; } = new Vector3D(1, -1, -1);

        public Color Light1Color { set; get; } = Colors.Blue;

        public Vector3D Light2Direction { get; set; } = new Vector3D(-1, -1, -1);

        public Color Light2Color { set; get; } = Colors.Red;

        public Vector3D Light3Direction { get; set; } = new Vector3D(-1, -1, 1);

        public Color Light3Color { set; get; } = Colors.Green;

        public ViewModel3D()
        {
            var builder = new MeshBuilder(true, true, true);
            // ToDo: Better use 2D Shape?
            builder.AddBox(new Vector3(0, 2.5f, 0), 5, 5, 5);
            Model = builder.ToMeshGeometry3D();

            //ModelMaterial.DiffuseMap = diffuseMap;
            //ModelMaterial.NormalMap = normalMap;
        }
    }
}
