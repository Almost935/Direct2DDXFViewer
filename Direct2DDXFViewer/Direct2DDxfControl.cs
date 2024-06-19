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
using System.Windows.Media;

using Point = System.Windows.Point;
using Brush = SharpDX.Direct2D1.Brush;
using Geometry = SharpDX.Direct2D1.Geometry;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;


namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged
    {
        #region Fields
        private System.Windows.Media.Matrix matrix = new();
        private Point lastTranslatePos = new();
        private float currentThickness = 0.5f;
        private bool dxfLoaded = false;

        private DxfDocument dxfDoc;
        private string filePath = @"DXF\ACAD-SP1-21 Points.dxf";
        private Point pointerCoords = new();
        private Point dxfPointerCoords = new();
        private List<Geometry> geometries = new();
        private Rect extents = new();
        #endregion

        #region Properties
        public DxfDocument DxfDoc
        {
            get { return dxfDoc; }
            set
            {
                dxfDoc = value;
                OnPropertyChanged(nameof(DxfDoc));
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
        public Rect Extents
        {
            get { return extents; }
            set
            {
                extents = value;
                OnPropertyChanged(nameof(Extents));
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
            DxfDoc = DxfDocument.Load(FilePath);
            if (DxfDoc is not null)
            {
                dxfLoaded = true;

                Extents = DxfHelpers.GetExtentsFromHeader(DxfDoc);
            }
        }
        public Matrix GetInitialMatrix()
        {
            if (Extents == Rect.Empty)
            {
                return new Matrix();
            }
            else
            {
                Matrix matrix = new Matrix();

                double raWidth = resCache.RenderTarget.Size.Width; 
                double raHeight = resCache.RenderTarget.Size.Height;

                double scaleX = raWidth / Extents.Width;
                double scaleY = raHeight / Extents.Height;

                Point center = new((extents.X - extents.Width / 2), (extents.Y - extents.Height / 2));
                matrix.Translate(center.X, center.Y);

                if (scaleX < scaleY)
                {
                    matrix.ScaleAt(scaleX, scaleX, center.X, center.Y);
                }
                else
                {
                    matrix.ScaleAt(scaleY, scaleY, center.X, center.Y);
                }

                return matrix;
            }
        }

        public override void Render(RenderTarget target)
        {
            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            Brush brush = resCache["BlackBrush"] as Brush;

            if (!dxfLoaded)
            {
                LoadDxf();
                matrix = GetInitialMatrix();
            }

            foreach (var line in DxfDoc.Entities.Lines)
            {
                PathGeometry pathGeometry = new(resCache.Factory);
                var sink = pathGeometry.Open();
                sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
                sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
                sink.Dispose();

                target.DrawGeometry(pathGeometry, brush, currentThickness);
            }


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
