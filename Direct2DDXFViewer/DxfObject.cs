using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using netDxf;
using netDxf.Header;
using netDxf.Objects;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using Geometry = SharpDX.Direct2D1.Geometry;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;

namespace Direct2DDXFViewer
{
    public class DxfObject
    {
        public DxfDocument DxfDocument { get; set; }
        public bool DocIsValid { get; set; } = false;
        public Rect Extents { get; set; } = Rect.Empty;
        public List<Geometry> Lines { get; set; }

        public DxfObject(string filepath)
        {
            DxfDocument = DxfDocument.Load(filepath);

            if (DxfDocument is not null)
            {
                DocIsValid = true;

                GetExtentsFromHeader();
                if (Extents.IsEmpty)
                {

                }
            }
        }

        public void GetExtentsFromHeader()
        {
            if (DxfDocument is not null)
            {
                Vector3 extMin = DxfDocument.Layouts[Layout.ModelSpaceName].MinExtents;
                Vector3 extMax = DxfDocument.Layouts[Layout.ModelSpaceName].MaxExtents;

                if (DxfDocument.DrawingVariables.TryGetCustomVariable("$EXTMIN", out HeaderVariable extMinHeaderVariable) &&
                    DxfDocument.DrawingVariables.TryGetCustomVariable("$EXTMAX", out HeaderVariable extMaxHeaderVariable))
                {
                    extMin = (Vector3)extMinHeaderVariable.Value;
                    extMax = (Vector3)extMaxHeaderVariable.Value;
                }

                Extents = new(extMin.X, extMin.Y, extMax.X - extMin.X, extMax.Y - extMin.Y);
            }
        }

        public static List<Geometry> GetLines(string filepath, Factory factory)
        {
            List<Geometry> lines = new List<Geometry>();

            DxfDocument doc = DxfDocument.Load(filepath);

            foreach (var line in doc.Entities.Lines)
            {
                PathGeometry pathGeometry = new(factory);
                var sink = pathGeometry.Open();
                sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
                sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
                sink.Dispose();

                lines.Add(pathGeometry);
            }

            return lines;
        }
    }
}
