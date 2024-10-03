using Direct2DDXFViewer.DrawingObjects;
using netDxf.Entities;
using netDxf.Tables;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.Helpers
{
    public class GeometryRealizationRenderer
    {
        public static void RenderGeometryRealization(DeviceContext1 deviceContext, DrawingObject drawingObject)
        {
            switch (drawingObject)
            {
                case DrawingSegment drawingSegment:
                    RenderDrawingSegment(deviceContext, drawingSegment);
                    break;
                case DrawingPolyline drawingPolyline:
                    RenderDrawingPolyline(deviceContext, drawingPolyline);
                    break;
                case DrawingBlock drawingBlock:
                    RenderDrawingBlock(deviceContext, drawingBlock);
                    break;
                case DrawingMtext drawingMtext:
                    RenderDrawingMtext(deviceContext, drawingMtext);
                    break;
                default:
                    break;
            }
        }

        public static void RenderDrawingSegment(DeviceContext1 deviceContext, DrawingSegment drawingSegment)
        {
            if (drawingSegment.GeometryRealization is not null)
            {
                deviceContext.DrawGeometryRealization(drawingSegment.GeometryRealization, drawingSegment.Brush);
            }
        }

        public static void RenderDrawingPolyline(DeviceContext1 deviceContext, DrawingPolyline drawingPolyline)
        {
            foreach (var segment in drawingPolyline.DrawingSegments)
            {
                RenderDrawingSegment(deviceContext, segment);
            }
        }

        public static void RenderDrawingBlock(DeviceContext1 deviceContext, DrawingBlock drawingBlock)
        {
            foreach (var obj in drawingBlock.DrawingObjects)
            {
                RenderGeometryRealization(deviceContext, obj);
            }
        }

        public static void RenderDrawingMtext(DeviceContext1 deviceContext, DrawingMtext drawingMtext)
        {
            drawingMtext.DrawToDeviceContext(deviceContext, 0.25f, drawingMtext.Brush);
        }
    }
}
