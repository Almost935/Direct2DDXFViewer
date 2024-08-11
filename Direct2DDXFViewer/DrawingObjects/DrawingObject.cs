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
using System.Windows;


namespace Direct2DDXFViewer.DrawingObjects
{
    public abstract class DrawingObject : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private ObjectLayer _layer;
        private bool _isSnapped = false;
        private bool _isHighlighted = false;
        private float _outerEdgeOpacity = 0.25f;
        private bool _disposed = false;
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
        public Rect Bounds { get; set; }
        public DeviceContext1 DeviceContext { get; set; }
        public Factory1 Factory { get; set; }
        public Brush Brush { get; set; }
        public Brush OuterEdgeBrush { get; set; }
        public StrokeStyle1 StrokeStyle { get; set; }
        public float Thickness { get; set; } = 0.25f;
        public ResourceCache ResCache { get; set; }
        public bool IsInView { get; set; } = true;
        #endregion

        #region Constructor
        public DrawingObject() { }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public abstract void UpdateGeometry();

        public abstract void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush);
        public abstract void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush);
        public abstract bool DrawingObjectIsInRect(Rect rect);

        public void UpdateBrush()
        {
            if (Entity is null || DeviceContext is null)
            {
                return;
            }

            if (Brush is not null)
            {
                Brush.Dispose();
                Brush = null;
            }
            byte r, g, b, a;
            if (Entity.Color.IsByLayer)
            {
                if (Entity.Layer.Color.R == 255 && Entity.Layer.Color.G == 255 && Entity.Layer.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                    //Brush = new SolidColorBrush(DeviceContext, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
                    //OuterEdgeBrush = new SolidColorBrush(DeviceContext, new RawColor4(0.0f, 0.0f, 0.0f, 1));
                    //OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
                else
                {
                    r = Entity.Layer.Color.R; g = Entity.Layer.Color.G; b = Entity.Layer.Color.B; a = 255;
                    //Brush = new SolidColorBrush(DeviceContext,
                    //    new RawColor4((float)(Entity.Layer.Color.R / 255), (float)(Entity.Layer.Color.G / 255), (float)(Entity.Layer.Color.B / 255), 1.0f));
                    //OuterEdgeBrush = new SolidColorBrush(DeviceContext,
                    //    new RawColor4((float)(Entity.Layer.Color.R / 255), (float)(Entity.Layer.Color.G / 255), (float)(Entity.Layer.Color.B / 255), 1));
                    //OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
            }
            else
            {
                if (Entity.Color.R == 255 && Entity.Color.G == 255 && Entity.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                    //Brush = new SolidColorBrush(DeviceContext, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f));
                    //OuterEdgeBrush = new SolidColorBrush(DeviceContext, new RawColor4(0.0f, 0.0f, 0.0f, 1));
                    //OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
                else
                {
                    r = Entity.Color.R; g = Entity.Color.G; b = Entity.Color.B; a = 255;
                    //Brush = new SolidColorBrush(DeviceContext, new RawColor4((float)(Entity.Color.R) / 255, (float)(Entity.Color.G) / 255, (float)(Entity.Color.B) / 255, 1.0f));
                    //OuterEdgeBrush = new SolidColorBrush(DeviceContext, new RawColor4((float)(Entity.Color.R) / 255, (float)(Entity.Color.G) / 255, (float)(Entity.Color.B) / 255, 1f));
                    //OuterEdgeBrush.Opacity = _outerEdgeOpacity;
                }
            }

            bool brushExisted = ResCache.Brushes.TryGetValue((r, g, b, a), value: out Brush brush);
            if (!brushExisted)
            {
                Brush = new SolidColorBrush(DeviceContext, new RawColor4((float)r / 255, (float)g / 255, (float)b / 255, 1.0f));
                ResCache.Brushes.Add((r, g, b, a), Brush);
            }
            else
            {
                Brush = brush;
            }
        }

        public void GetStrokeStyle()
        {
            //if (StrokeStyle is not null)
            //{
            //    StrokeStyle.Dispose();
            //    StrokeStyle = null;
            //}
            //StrokeStyleProperties1 ssp = new()
            //{
            //    StartCap = CapStyle.Round,
            //    EndCap = CapStyle.Round,
            //    DashCap = CapStyle.Flat,
            //    LineJoin = LineJoin.Round,
            //    MiterLimit = 10.0f,
            //    DashStyle = DashStyle.Solid,
            //    DashOffset = 0.0f,
            //    TransformType = StrokeTransformType.Hairline
            //};
            //StrokeStyle = new(Factory, ssp);
        }
        public void GetThickness()
        {
            if (Entity is null)
            {
                return;
            }

            var lineweight = Entity.Lineweight;
        }
        public void UpdateFactory(Factory1 factory)
        {
            Factory = factory;
            GetStrokeStyle();
        }
        public void UpdateDeviceContext(DeviceContext1 deviceContext)
        {
            DeviceContext = deviceContext;
            UpdateBrush();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed resources
                Brush?.Dispose();
                OuterEdgeBrush?.Dispose();
                StrokeStyle?.Dispose();
                Geometry?.Dispose();
            }

            // Free unmanaged resources if any

            _disposed = true;
        }

        ~DrawingObject()
        {
            Dispose(false);
        }
        #endregion
    }
}
