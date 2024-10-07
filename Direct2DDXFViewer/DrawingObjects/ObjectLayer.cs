using Direct2DControl;
using netDxf.Tables;
using SharpDX.Direct2D1;
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
    public class ObjectLayer : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private readonly DeviceContext1 _deviceContext;
        private readonly Factory1 _factory;
        private readonly ResourceCache _resourceCache;
        private string _name;
        private List<DrawingObject> _drawingObjects = [];
        private bool isVisible = true;
        private bool _disposed = false;
        #endregion

        #region Properties
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public List<DrawingObject> DrawingObjects
        {
            get { return _drawingObjects; }
            set
            {
                _drawingObjects = value;
                OnPropertyChanged(nameof(DrawingObjects));
            }
        }
        public GeometryGroup GeometryGroup { get; set; }
        public int DrawingObjectsCount
        {
            get { return DrawingObjects.Count; }
        }
        public bool IsVisible
        {
            get { return isVisible; }
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
        public Dictionary<int, List<(List<GeometryRealization> geometryRealizations, Brush brush)>> GeometryRealizations { get; set; } = [];
        public Brush LayerBrush { get; set; }
        public StrokeStyle1 HairlineStrokeStyle { get; set; }
        #endregion

        #region Constructors
        public ObjectLayer(DeviceContext1 deviceContext, Factory1 factory, ResourceCache resCache, string name, Brush layerBrush)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _resourceCache = resCache;
            Name = name;
            LayerBrush = layerBrush;

            GetLayerStrokeStyle();
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void DrawVisibleObjectsToDeviceContext(DeviceContext1 deviceContext, float thickness)
        {
            if (!IsVisible) { return; }

            foreach (var drawingObject in DrawingObjects)
            {
                if (drawingObject.IsInView)
                {
                    drawingObject.DrawToDeviceContext(deviceContext, thickness, drawingObject.Brush, drawingObject.HairlineStrokeStyle);
                }
            }
        }
        public void DrawVisibleObjectsToRenderTarget(RenderTarget renderTarget, float thickness)
        {
            if (!IsVisible) { return; }
            foreach (var drawingObject in DrawingObjects)
            {
                if (drawingObject.IsInView)
                {
                    drawingObject.DrawToRenderTarget(renderTarget, thickness, drawingObject.Brush, drawingObject.HairlineStrokeStyle);
                }
            }
        }

        public void DrawObjectsToDeviceContext(DeviceContext1 deviceContext, float thickness)
        {
            if (!IsVisible) { return; }

            foreach (var drawingObject in DrawingObjects)
            {
                drawingObject.DrawToDeviceContext(deviceContext, thickness, drawingObject.Brush, drawingObject.HairlineStrokeStyle);
            }
        }
        public void DrawObjectsToRenderTarget(RenderTarget renderTarget, float thickness)
        {
            if (!IsVisible) { return; }

            foreach (var drawingObject in DrawingObjects)
            {
                drawingObject.DrawToRenderTarget(renderTarget, thickness, new SolidColorBrush(renderTarget, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1)), drawingObject.HairlineStrokeStyle);
            }
        }

        public void LoadDrawingRealizations()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            //await Task.Run(() =>
            //{
            List<(List<GeometryRealization>, Brush)> geometryRealizations = new();

            Parallel.ForEach(DrawingObjects, drawingObject =>
            {
                List<GeometryRealization> objRealizations = drawingObject.GetGeometryRealization(0.5f);

                geometryRealizations.Add((objRealizations, drawingObject.Brush));
            });

            GeometryRealizations.Add(0, geometryRealizations);

            Debug.WriteLine($"\ngeometryRealizations.Count: {geometryRealizations.Count}");
            //});

            stopwatch.Stop();
            Debug.WriteLine($"LoadDrawingRealizations for layer {_name}: {stopwatch.ElapsedMilliseconds}");
        }

        public void LoadGeometryGroup()
        {
            List<Geometry> geometries = new();
            foreach (var obj in DrawingObjects)
            {
                if (obj is DrawingBlock block)
                {
                    foreach (var blockObj in block.DrawingObjects)
                    {
                        if (obj is DrawingPolyline polyline)
                        {
                            foreach (var segment in polyline.DrawingSegments)
                            {
                                if (segment.Geometry is not null)
                                {
                                    geometries.Add(segment.Geometry);
                                }
                            }
                        }
                        if (blockObj.Geometry is not null)
                        {
                            geometries.Add(blockObj.Geometry);
                        }
                    }
                }
                else if (obj is DrawingPolyline polyline)
                {
                    foreach (var segment in polyline.DrawingSegments)
                    {
                        if (segment.Geometry is not null)
                        {
                            geometries.Add(segment.Geometry);
                        }
                    }
                }
                else if (obj.Geometry is not null)
                {
                    geometries.Add(obj.Geometry);
                }
            }
            if (geometries.Count > 0)
            {
                var geometryArr = geometries.ToArray();
                GeometryGroup = new(_deviceContext.Factory, FillMode.Alternate, geometryArr);
            }
        }

        public void GetLayerStrokeStyle()
        {
            bool hairlineStrokeStyleExists = _resourceCache.StrokeStyles.TryGetValue(ResourceCache.LineType.Solid_Hairline, value: out StrokeStyle1 hairlineStrokeStyle);
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
                HairlineStrokeStyle = new(_factory, ssp);
                _resourceCache.StrokeStyles.Add(ResourceCache.LineType.Solid_Hairline, HairlineStrokeStyle);
            }
            else
            {
                HairlineStrokeStyle = hairlineStrokeStyle;
            }
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
                if (_drawingObjects != null)
                {
                    foreach (var drawingObject in _drawingObjects)
                    {
                        if (drawingObject is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    _drawingObjects.Clear();
                }
            }

            // Free unmanaged resources if any

            _disposed = true;
        }

        ~ObjectLayer()
        {
            Dispose(false);
        }
        #endregion
    }
}
