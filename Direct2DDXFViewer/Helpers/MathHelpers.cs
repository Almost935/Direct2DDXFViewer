using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;

using FeatureLevel = SharpDX.Direct3D.FeatureLevel;
using System.Net;
using System.Windows;

namespace Direct2DDXFViewer.Helpers
{
    public static class MathHelpers
    {
        public static bool IsGeometryInRect(RawRectangleF viewport, Geometry geometry, float strokeThickness)
        {
            // Attempt to get the bounds of the geometry
            var bounds = geometry.GetWidenedBounds(strokeThickness);

            // Check if the bounds intersect with the viewport
            return bounds.Left < viewport.Right &&
                   bounds.Right > viewport.Left &&
                   bounds.Top < viewport.Bottom &&
                   bounds.Bottom > viewport.Top;
        }

        public static bool IsLineInRect(Rect rect, Point startPoint, Point endPoint)
        {
            // Check if either of the line's endpoints are within the rectangle
            if (rect.Contains(startPoint) || rect.Contains(endPoint))
            {
                return true;
            }

            // Check if the line intersects any of the rectangle's sides
            return LineIntersectsRect(rect, startPoint, endPoint);
        }

        public static bool LineIntersectsRect(Rect rect, Point startPoint, Point endPoint)
        {
            // Define the rectangle's corners
            var topLeft = new Point(rect.Left, rect.Top);
            var topRight = new Point(rect.Right, rect.Top);
            var bottomLeft = new Point(rect.Left, rect.Bottom);
            var bottomRight = new Point(rect.Right, rect.Bottom);

            // Check for intersection with each side of the rectangle
            return LinesIntersect(startPoint, endPoint, topLeft, topRight) ||
                   LinesIntersect(startPoint, endPoint, topRight, bottomRight) ||
                   LinesIntersect(startPoint, endPoint, bottomRight, bottomLeft) ||
                   LinesIntersect(startPoint, endPoint, bottomLeft, topLeft);
        }

        public static bool LinesIntersect(Point p1, Point p2, Point q1, Point q2)
        {
            double d1 = CrossProduct(p1, p2, q1);
            double d2 = CrossProduct(p1, p2, q2);
            double d3 = CrossProduct(q1, q2, p1);
            double d4 = CrossProduct(q1, q2, p2);

            if (d1 * d2 < 0 && d3 * d4 < 0)
            {
                return true;
            }

            return false;
        }

        public static double CrossProduct(Point a, Point b, Point c)
        {
            return (b.Y - a.Y) * (c.X - b.X) - (b.X - a.X) * (c.Y - b.Y);
        }

        public static float GetZoom(float zoomFactor, int zoomStep, int numOfDigits)
        {
            return (float)Math.Round(Math.Pow(zoomFactor, zoomStep), numOfDigits);
        }

        public static bool RectsIntersect(Rect rect1, Rect rect2)
        {
            if (rect1.IntersectsWith(rect2) || rect1.Contains(rect2) || rect2.Contains(rect1))
            {
                return true;
            }
            return false;
        }
    }
}
