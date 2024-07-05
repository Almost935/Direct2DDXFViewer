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
        private bool _isSnapped = false;
        private bool _isHighlighted = false;
        #endregion

        #region Properties
        public bool IsSnapped
        {
            get { return _isSnapped; }
            set
            {
                _isSnapped = value;
                OnPropertyChanged(nameof(IsSnapped));
            }
        }
        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set
            {
                _isHighlighted = value;
                OnPropertyChanged(nameof(IsHighlighted));
            }
        }
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
        public Geometry SimplifiedGeometry { get; set; }
        public GeometryRealization GeometryRealization { get; set; }
        public RenderTarget Target { get; set; }
        public DeviceContext1 DeviceContext { get; set; }
        public Factory1 Factory { get; set; }
        public Brush Brush { get; set; }
        public StrokeStyle1 StrokeStyle { get; set; }
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
            if (Brush is not null)
            {
                Brush.Dispose();
            }

            //if (IsSnapped && IsHighlighted)
            //{
            //    if (ResCache.ContainsKey("SnappedHighlightedBrush"))
            //    {
            //        Brush = (Brush)ResCache["SnappedHighlightedBrush"];
            //    }
            //    else { return; }
            //}
            //else if (IsSnapped)
            //{
            //    if (ResCache.ContainsKey("SnappedBrush"))
            //    {
            //        Brush = (Brush)ResCache["SnappedBrush"];
            //    }
            //    else { return; }
            //}
            //else if (IsHighlighted)
            //{
            //    if (ResCache.ContainsKey("HighlightedBrush"))
            //    {
            //        Brush = (Brush)ResCache["HighlightedBrush"];
            //    }
            //    else { return; }
            //}
            //else
            //{
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
            //}
        }

        public void GetStrokeStyle()
        {
            StrokeStyleProperties1 ssp = new()
            {
                StartCap = CapStyle.Round,
                EndCap = CapStyle.Round,
                DashCap = CapStyle.Flat,
                LineJoin = LineJoin.Miter,
                MiterLimit = 10.0f,
                DashStyle = DashStyle.Solid,
                DashOffset = 0.0f,
                TransformType = StrokeTransformType.Normal
            };
            StrokeStyle = new(Factory, ssp);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
