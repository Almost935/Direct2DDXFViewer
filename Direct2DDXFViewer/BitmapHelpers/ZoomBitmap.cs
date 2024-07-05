using SharpDX;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class ZoomBitmap : Bitmap
    {
        public float Zoom { get; set; }

        public ZoomBitmap(RenderTarget renderTarget, Size2 size, float zoom) : base(renderTarget, size)
        {
            Zoom = zoom;
        }
    }
}
