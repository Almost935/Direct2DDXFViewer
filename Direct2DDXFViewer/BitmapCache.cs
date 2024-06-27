using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer
{
    public class BitmapCache
    {
        public Dictionary<float, Bitmap1> Bitmaps { get; set; } = new();
        public float currentZoom = 1.0f;
        public int currentWidth = 0;
    }
}
