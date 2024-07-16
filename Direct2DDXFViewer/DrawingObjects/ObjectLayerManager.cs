using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class ObjectLayerManager
    {
        #region Properties
        public Dictionary<string, ObjectLayer> Layers { get; set; } = new();
        #endregion

        #region Methods
        public void Draw(RenderTarget renderTarget, float thickness)
        {
            foreach (var layer in Layers.Values)
            {
                layer.Draw(renderTarget, thickness);
            }
        }
        #endregion
    }
}
