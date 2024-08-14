using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmap
    {
        #region Fields
        private string _folderpath;
        private string _filepath;
        #endregion

        #region Properties
        public float Zoom { get; set; }
        public Bitmap Bitmap { get; set; }
        #endregion

        #region Constructor
        public DxfBitmap(float zoom, string filepath, Bitmap bitmap)
        {
            Zoom = zoom;
            Bitmap = bitmap;
            _filepath = filepath;
        }
        #endregion

        #region Methods
        #endregion
    }
}
