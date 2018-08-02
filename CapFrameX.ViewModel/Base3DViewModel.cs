using HelixToolkit.Wpf.SharpDX;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.ViewModel
{
    public abstract class Base3DViewModel : BindableBase, IDisposable
    {
        public const string Orthographic = "Orthographic Camera";
        public const string Perspective = "Perspective Camera";

        private string _cameraModel;
        private Camera _camera;
        private string _subTitle;
        private string _title;
        private IEffectsManager _effectsManager;
        private IRenderTechnique _renderTechnique;
        private string _renderTechniqueName = DefaultRenderTechniqueNames.Blinn;

        public string Title
        {
            get { return _title; }
            set { _title = value; RaisePropertyChanged(); }
        }

        public string SubTitle
        {
            get { return _subTitle; }
            set { _subTitle = value; RaisePropertyChanged(); }
        }

        public IRenderTechnique RenderTechnique
        {
            get { return _renderTechnique; }
            set { _renderTechnique = value; RaisePropertyChanged(); }
        }

        public List<string> CameraModelCollection { get; private set; }

        public string CameraModel
        {
            get { return _cameraModel; }
            set { _cameraModel = value; RaisePropertyChanged(); }
        }

        public Camera Camera
        {
            get { return _camera; }
            set
            {
                _camera = value; RaisePropertyChanged();
                CameraModel = value is PerspectiveCamera
                                       ? Perspective
                                       : value is OrthographicCamera ? Orthographic : null;
            }
        }

        public IEffectsManager EffectsManager
        {
            get { return _effectsManager; }
            set { _effectsManager = value; RaisePropertyChanged(); }
        }

        public string RenderTechniqueName
        {
            set
            {
                _renderTechniqueName = value;
                RenderTechnique = EffectsManager[value];
            }
            get
            {
                return _renderTechniqueName;
            }
        }

        protected OrthographicCamera defaultOrthographicCamera = new OrthographicCamera { Position = new System.Windows.Media.Media3D.Point3D(0, 0, 5), LookDirection = new System.Windows.Media.Media3D.Vector3D(-0, -0, -5), UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0), NearPlaneDistance = 1, FarPlaneDistance = 100 };
        protected PerspectiveCamera defaultPerspectiveCamera = new PerspectiveCamera { Position = new System.Windows.Media.Media3D.Point3D(0, 0, 5), LookDirection = new System.Windows.Media.Media3D.Vector3D(-0, -0, -5), UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0), NearPlaneDistance = 0.5, FarPlaneDistance = 150 };

        public event EventHandler CameraModelChanged;

        protected Base3DViewModel()
        {
            // camera models
            CameraModelCollection = new List<string>()
            {
                Orthographic,
                Perspective,
            };

            // on camera changed callback
            CameraModelChanged += (s, e) =>
            {
                if (_cameraModel == Orthographic)
                {
                    if (!(Camera is OrthographicCamera))
                        Camera = defaultOrthographicCamera;
                }
                else if (_cameraModel == Perspective)
                {
                    if (!(Camera is PerspectiveCamera))
                        Camera = defaultPerspectiveCamera;
                }
                else
                {
                    throw new HelixToolkitException("Camera Model Error.");
                }
            };

            // default camera model
            CameraModel = Perspective;

            Title = "3D animated frame time test";
            SubTitle = "Default Base View Model";
        }

        protected virtual void OnCameraModelChanged()
        {
            CameraModelChanged?.Invoke(this, new EventArgs());
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

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                if (EffectsManager != null)
                {
                    var effectManager = EffectsManager as IDisposable;
                    Disposer.RemoveAndDispose(ref effectManager);
                }
                disposedValue = true;
                GC.SuppressFinalize(this);
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~Base3DViewModel()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}
