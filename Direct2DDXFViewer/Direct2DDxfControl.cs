using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.Mathematics;
using netDxf;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Input;
using SharpDX.Direct2D1.Effects;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

using Point = System.Windows.Point;


namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged
    {
        #region Fields
        private System.Windows.Media.Matrix matrix = new();
        private Point lastTranslatePos = new();
        private float currentThickness = 0.5f;
        private bool dxfLoaded = false;

        private DxfObject dxfObject;
        private string filePath = @"DXF\ACAD-SP1-21 Points.dxf";
        private Point pointerCoords = new();
        private Point dxfPointerCoords = new();
        private List<Geometry> geometries = new();
        #endregion

        #region Properties
        public DxfObject DxfObject
        {
            get { return dxfObject; }
            set
            {
                dxfObject = value;
                OnPropertyChanged(nameof(DxfObject));
            }
        }
        public string FilePath
        {
            get { return filePath; }
            set
            {
                filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }
        public Point PointerCoords
        {
            get { return pointerCoords; }
            set
            {
                pointerCoords = value;
                OnPropertyChanged(nameof(PointerCoords));
            }
        }
        public Point DxfPointerCoords
        {
            get { return dxfPointerCoords; }
            set
            {
                dxfPointerCoords = value;
                OnPropertyChanged(nameof(DxfPointerCoords));
            }
        }
        public List<Geometry> Geometries
        {
            get { return geometries; }
            set
            {
                geometries = value;
                OnPropertyChanged(nameof(Geometries));
            }
        }
        #endregion

        #region Constructor
        public Direct2DDxfControl()
        {
            resCache.Add("BlackBrush", t => new SolidColorBrush(t, new RawColor4(0.0f, 0.0f, 0.0f, 1.0f)));
            resCache.Add("RedBrush", t => new SolidColorBrush(t, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f)));
            resCache.Add("GreenBrush", t => new SolidColorBrush(t, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f)));
            resCache.Add("BlueBrush", t => new SolidColorBrush(t, new RawColor4(0.0f, 0.0f, 1.0f, 1.0f)));
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void LoadDxf()
        {
            DxfObject = new(FilePath);
        }

        public override void Render(RenderTarget target)
        {
            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            Brush brush = resCache["BlackBrush"] as Brush;

            if (!dxfLoaded)
            {
                DxfDocument doc = DxfDocument.Load(FilePath);

                foreach (var line in doc.Entities.Lines)
                {
                    PathGeometry pathGeometry = new(resCache.Factory);
                    var sink = pathGeometry.Open();
                    sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
                    sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();
                    sink.Dispose();

                    //Geometries.Add(pathGeometry);

                    target.DrawGeometry(pathGeometry, brush, currentThickness);
                }

                dxfLoaded = true;
            }

            //foreach (var geometry in Geometries)
            //{
            //    target.DrawGeometry(geometry, brush, currentThickness);
            //}

            //for (int i = 0; i < 2000; i += 4)
            //{
            //PathGeometry pathGeometry = new(resCache.Factory);
            //var sink = pathGeometry.Open();
            //sink.BeginFigure(new RawVector2(i, 0), FigureBegin.Filled);
            //sink.AddLine(new RawVector2(i, 2000));
            //sink.EndFigure(FigureEnd.Closed);
            //sink.Close();
            //sink.Dispose();

            //    target.DrawGeometry(pathGeometry, brush, currentThickness);
            //}
            //for (int i = 0; i < 2000; i += 4)
            //{
            //    PathGeometry pathGeometry = new(resCache.Factory);
            //    var sink = pathGeometry.Open();
            //    sink.BeginFigure(new RawVector2(0, i), FigureBegin.Filled);
            //    sink.AddLine(new RawVector2(2000, i));
            //    sink.EndFigure(FigureEnd.Closed);
            //    sink.Close();
            //    sink.Dispose();

            //    target.DrawGeometry(pathGeometry, brush, currentThickness);
            //}

            target.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
           (float)matrix.OffsetX, (float)matrix.OffsetY);
            NeedsUpdate = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            float zoom;

            if (e.Delta > 0)
            {
                zoom = 1.3f;
            }
            else
            {
                zoom = 1f / 1.3f;
            }

            UpdateZoom(zoom);

            NeedsUpdate = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointerCoords = e.GetPosition(this);

            // Update DxfPointerCoords in background thread
            BackgroundWorker bw = new();
            bw.DoWork += UpdateDxfPointerCoords;
            bw.RunWorkerAsync();

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                var translate = PointerCoords - lastTranslatePos;

                UpdateTranslate(translate);

                lastTranslatePos = PointerCoords;

                NeedsUpdate = true;
            }
        }

        private void UpdateDxfPointerCoords(object? sender, DoWorkEventArgs e)
        {
            var newMatrix = matrix;
            newMatrix.Invert();
            DxfPointerCoords = newMatrix.Transform(PointerCoords);
            //newMatrix.Transform(DxfPointerCoords);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                lastTranslatePos = e.GetPosition(this);
            }
        }

        private void UpdateZoom(float zoom)
        {
            matrix.ScaleAt(zoom, zoom, PointerCoords.X, PointerCoords.Y);
            currentThickness /= zoom;

            resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                (float)matrix.OffsetX, (float)matrix.OffsetY);
        }
        private void UpdateTranslate(Vector translate)
        {
            matrix.Translate(translate.X, translate.Y);

            resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
            (float)matrix.OffsetX, (float)matrix.OffsetY);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
