using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer
{
    public class BitmapCache
    {
        #region Fields
        private RenderTarget _renderTarget;
        private float _currentZoom;
        #endregion

        #region Properties
        public Size2 Size { get; set; }
        public float ZoomFactor { get; set; }
        public int NumOfBitmaps { get; set; }
        public Bitmap CurrentBitmap { get; set; }
        public Dictionary<float, Bitmap> ZoomBitmaps { get; set; } = new();
        public Dictionary<float, Bitmap> PanBitmaps { get; set; } = new();
        public List<Geometry> Geometries { get; set; } = new();
        #endregion

        #region Constructors
        BitmapCache(RenderTarget renderTarget, List<Geometry> geometries, float currentZoom = 1.0f, int numOfBitmaps = 5)
        {
            _renderTarget = renderTarget;
            Geometries = geometries;
        }
        #endregion

        #region Methods
        public async void InitializeBitmaps()
        {
            for (int i = 1; i <= NumOfBitmaps; i++)
            {
                float zoom = _currentZoom + (i * ZoomFactor);
                Bitmap bitmap = new(_renderTarget, Size);
                
                ZoomBitmaps.Add(zoom, bitmap);
            }   
            for (int i = 1; i <= NumOfBitmaps; i++)
            {
                float zoom = _currentZoom - (i * ZoomFactor);
                Bitmap bitmap = new(_renderTarget, Size);
                ZoomBitmaps.Add(zoom, bitmap);
            }
        }
        public Bitmap GetBitmap(float zoom)
        {
            if (ZoomBitmaps.TryGetValue(zoom, out Bitmap bitmap))
            {
                return bitmap;
            }
            else
            {
                Bitmap newBitmap = new(_renderTarget, Size);
            }

            if (ZoomBitmaps.ContainsKey(zoom))
            {
                return ZoomBitmaps[zoom];
            }
            else
            {
                return null;
            }
        }
        public async void UpdateZoom(float zoom)
        {

        }
        public async void Dispose()
        {
            foreach (var bitmap in ZoomBitmaps)
            {
                bitmap.Value.Dispose();
            }
        }   
        #endregion
    }
}
