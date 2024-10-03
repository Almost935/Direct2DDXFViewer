using Direct2DControl;
using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        public GeometryRealization GeometryRealization { get; set; }
        public Rect Bounds { get; set; }
        public DeviceContext1 DeviceContext { get; set; }
        public Factory1 Factory { get; set; }
        public Brush Brush { get; set; }
        public Brush OuterEdgeBrush { get; set; }
        public StrokeStyle1 HairlineStrokeStyle { get; set; }
        public StrokeStyle1 FixedStrokeStyle { get; set; }
        public float Thickness { get; set; } = 0.25f;
        public ResourceCache ResCache { get; set; }
        public bool IsInView { get; set; } = true;
        public bool IsPartOfBlock { get; set; } = false;
        public DrawingBlock Block { get; set; }
        public int EntityCount { get; set; }
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
        public abstract void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush, StrokeStyle1 strokeStyle);
        public abstract void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush);
        public abstract void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush, StrokeStyle1 strokeStyle);
        public abstract bool DrawingObjectIsInRect(Rect rect);
        public abstract bool Hittest(RawVector2 p, float thickness);

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
            
            (byte r, byte g, byte b, byte a) = DxfHelpers.GetRGBAColor(Entity);

            bool brushExists = ResCache.Brushes.TryGetValue((r, g, b, a), value: out Brush brush);
            if (!brushExists)
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
            bool hairlineStrokeStyleExists = ResCache.StrokeStyles.TryGetValue(ResourceCache.LineType.Solid_Hairline, value: out StrokeStyle1 hairlineStrokeStyle);
            if (!hairlineStrokeStyleExists)
            {
                StrokeStyleProperties1 ssp = new()
                {
                    StartCap = CapStyle.Round,
                    EndCap = CapStyle.Round,
                    DashCap = CapStyle.Flat,
                    LineJoin = LineJoin.Round,
                    MiterLimit = 10.0f,
                    DashStyle = DashStyle.Solid,
                    DashOffset = 0.0f,
                    TransformType = StrokeTransformType.Hairline
                };
                HairlineStrokeStyle = new(Factory, ssp);
                ResCache.StrokeStyles.Add(ResourceCache.LineType.Solid_Hairline, HairlineStrokeStyle);
            }
            else
            {
                HairlineStrokeStyle = hairlineStrokeStyle;
            }

            bool fixedStrokeStyleExists = ResCache.StrokeStyles.TryGetValue(ResourceCache.LineType.Solid_Fixed, value: out StrokeStyle1 fixedStrokeStyle);
            if (!fixedStrokeStyleExists)
            {
                StrokeStyleProperties1 ssp = new()
                {
                    StartCap = CapStyle.Round,
                    EndCap = CapStyle.Round,
                    DashCap = CapStyle.Flat,
                    LineJoin = LineJoin.Round,
                    MiterLimit = 10.0f,
                    DashStyle = DashStyle.Solid,
                    DashOffset = 0.0f,
                    TransformType = StrokeTransformType.Fixed
                };
                FixedStrokeStyle = new(Factory, ssp);
                ResCache.StrokeStyles.Add(ResourceCache.LineType.Solid_Fixed, FixedStrokeStyle);
            }
            else
            {
                FixedStrokeStyle = fixedStrokeStyle;
            }
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
                HairlineStrokeStyle?.Dispose();
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
