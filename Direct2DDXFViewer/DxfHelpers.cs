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
using netDxf.Entities;
using netDxf.Tables;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using Geometry = SharpDX.Direct2D1.Geometry;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;

namespace Direct2DDXFViewer
{
    public static class DxfHelpers
    {
        public static Rect GetExtentsFromHeader(DxfDocument doc)
        {
            if (doc is not null)
            {
                Vector3 extMin = doc.Layouts[Layout.ModelSpaceName].MinExtents;
                Vector3 extMax = doc.Layouts[Layout.ModelSpaceName].MaxExtents;

                if (doc.DrawingVariables.TryGetCustomVariable("$EXTMIN", out HeaderVariable extMinHeaderVariable) &&
                    doc.DrawingVariables.TryGetCustomVariable("$EXTMAX", out HeaderVariable extMaxHeaderVariable))
                {
                    extMin = (Vector3)extMinHeaderVariable.Value;
                    extMax = (Vector3)extMaxHeaderVariable.Value;

                    return new Rect(extMin.X, extMin.Y, extMax.X - extMin.X, extMax.Y - extMin.Y);
                }
                return Rect.Empty;
            }

            else
            {
                return Rect.Empty;
            }
        }

        //public static List<Geometry> GetLines(DxfDocument doc, Factory factory)
        //{
        //    List<Geometry> lines = new List<Geometry>();

        //    foreach (var line in doc.Entities.Lines)
        //    {
        //        PathGeometry pathGeometry = new(factory);
        //        var sink = pathGeometry.Open();
        //        sink.BeginFigure(new RawVector2((float)line.StartPoint.X, (float)line.StartPoint.Y), FigureBegin.Filled);
        //        sink.AddLine(new RawVector2((float)line.EndPoint.X, (float)line.EndPoint.Y));
        //        sink.EndFigure(FigureEnd.Closed);
        //        sink.Close();
        //        sink.Dispose();

        //        lines.Add(pathGeometry);
        //    }

        //    return lines;
        //}
    }
}
