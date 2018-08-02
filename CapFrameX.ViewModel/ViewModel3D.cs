using Prism.Mvvm;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Vector3D = System.Windows.Media.Media3D.Vector3D;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using System.IO;

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

        private string NormalTexture = @"TextureCheckerboard2_dot3.jpg";

        private string Texture = @"TextureCheckerboard2.jpg";

        public ViewModel3D()
        {
            var builder = new MeshBuilder(true, true, true);
            builder.AddBox(new Vector3(0, 2.5f, 0), 5, 5, 5);
            // builder.AddBox(new Vector3(0, 0, 0), 10, 0.1, 10);
            Model = builder.ToMeshGeometry3D();
            //var diffuseMap = LoadFileToMemory(new System.Uri(Texture, System.UriKind.RelativeOrAbsolute).ToString());
            //var normalMap = LoadFileToMemory(new System.Uri(NormalTexture, System.UriKind.RelativeOrAbsolute).ToString());
            //ModelMaterial.DiffuseMap = diffuseMap;
            //ModelMaterial.NormalMap = normalMap;
        }

        public static MemoryStream LoadFileToMemory(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Open))
            {
                var memory = new MemoryStream();
                file.CopyTo(memory);
                return memory;
            }
        }
    }
}
