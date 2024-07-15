using Direct2DControl;
using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        private float _outerEdgeOpacity = 0.25f;
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

        public EntityObject Entity { get; set; }
        public Geometry Geometry { get; set; }
        public Geometry SimplifiedGeometry { get; set; }
        public RenderTarget Target { get; set; }
        public DeviceContext1 DeviceContext { get; set; }
        public Factory1 Factory { get; set; }
        public Brush Brush { get; set; }
        public Brush OuterEdgeBrush { get; set; }
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

        public abstract void Draw(RenderTarget target, float thickness, Brush brush);

        public void UpdateBrush()
        {
            if (Entity is null || Target is null)
            {
                return;
            }

            if (Brush is not null)
            {
                Brush.Dispose();
                Brush = null;
            }

            if (Entity.Color.IsByLayer)
            {
                if (Entity.Layer.Color.R == 255 && Entity.Layer.Color.G == 255 && Entity.Layer.Color.B == 255)
                {
                    Brush = new SolidColorBrush(Target, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
                    OuterEdgeBrush = new SolidColorBrush(Target, new RawColor4(0.0f, 0.0f, 0.0f, 1));
                    OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
                else
                {
                    Brush = new SolidColorBrush(Target,
                        new RawColor4((float)(Entity.Layer.Color.R / 255), (float)(Entity.Layer.Color.G / 255), (float)(Entity.Layer.Color.B / 255), 1.0f));
                    OuterEdgeBrush = new SolidColorBrush(Target,
                        new RawColor4((float)(Entity.Layer.Color.R / 255), (float)(Entity.Layer.Color.G / 255), (float)(Entity.Layer.Color.B / 255), 1));
                    OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
            }
            else
            {
                if (Entity.Color.R == 255 && Entity.Color.G == 255 && Entity.Color.B == 255)
                {
                    Brush = new SolidColorBrush(Target, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
                    OuterEdgeBrush = new SolidColorBrush(Target, new RawColor4(0.0f, 0.0f, 0.0f, 1));
                    OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
                else
                {
                    Brush = new SolidColorBrush(Target, new RawColor4((float)(Entity.Color.R) / 255, (float)(Entity.Color.G) / 255, (float)(Entity.Color.B) / 255, 1.0f));
                    OuterEdgeBrush = new SolidColorBrush(Target, new RawColor4((float)(Entity.Color.R) / 255, (float)(Entity.Color.G) / 255, (float)(Entity.Color.B) / 255, 1f));
                    OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
            }
        }

        public void GetStrokeStyle()
        {
            if (StrokeStyle is not null)
            {
                StrokeStyle.Dispose();
                StrokeStyle = null;
            }
            StrokeStyleProperties1 ssp = new()
            {
                StartCap = CapStyle.Round,
                EndCap = CapStyle.Round,
                DashCap = CapStyle.Flat,
                LineJoin = LineJoin.Round,
                MiterLimit = 10.0f,
                DashStyle = DashStyle.Solid,
                DashOffset = 0.0f,
                TransformType = StrokeTransformType.Normal
            };
            StrokeStyle = new(Factory, ssp);
        }
        public void UpdateFactory(Factory1 factory)
        {
            Factory = factory;
            GetStrokeStyle();
        }
        public void UpdateTarget(RenderTarget target)
        {
            Target = target;
            UpdateBrush();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
