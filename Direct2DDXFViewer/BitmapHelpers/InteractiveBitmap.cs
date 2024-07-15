using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class InteractiveBitmap
    {
        public BitmapRenderTarget BitmapRenderTarget { get; set; }
        public BitmapCache BitmapCache { get; set; }

        public InteractiveBitmap(BitmapCache bitmapCache) 
        {
            BitmapCache = bitmapCache;
            BitmapRenderTarget = new(bitmapCache._renderTarget, CompatibleRenderTargetOptions.None, bitmapCache.RenderTargetSize);
        }
    }
}
