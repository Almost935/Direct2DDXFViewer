using Direct2DControl;
using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace Direct2DDXFViewer.DrawingObjects
{
    public abstract class DrawingObject : INotifyPropertyChanged
    {
        #region Fields
        private ObjectLayer _layer;
        #endregion

        #region Properties
        public ObjectLayer Layer
        {
            get { return _layer; }
            set
            {
                _layer = value;
                OnPropertyChanged(nameof(Layer));
            }
        }

        public Geometry Geometry { get; set;}
        public Factory Factory { get; set; }
        public Brush Brush { get; set;}
        public ResourceCache ResCache { get; set; }
        #endregion

        #region Constructor
        public DrawingObject() { }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public abstract void UpdateGeometry();

        public void UpdateBrush(EntityObject entity, RenderTarget target)
        {
            if (entity.Color.IsByLayer)
            {
                if (entity.Layer.Color.R == 255 && entity.Layer.Color.G == 255 && entity.Layer.Color.B == 255)
                {
                    Brush = new SolidColorBrush(target, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
                }
                else
                {
                    Brush = new SolidColorBrush(target, 
                        new RawColor4((float)(entity.Layer.Color.R / 255), (float)(entity.Layer.Color.G / 255), (float)(entity.Layer.Color.B / 255), 1.0f));
                }
            }
            else
            {
                if (entity.Color.R == 255 && entity.Color.G == 255 && entity.Color.B == 255)
                {
                    Brush = new SolidColorBrush(target, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
                }
                else
                {
                    Brush = new SolidColorBrush(target, new RawColor4((float)(entity.Color.R) / 255, (float)(entity.Color.G) / 255, (float)(entity.Color.B) / 255, 1.0f));
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
