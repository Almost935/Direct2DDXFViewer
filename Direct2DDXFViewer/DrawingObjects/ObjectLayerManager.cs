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
        public Dictionary<string, ObjectLayer> Layers { get; set; } = new Dictionary<string, ObjectLayer>();
        #endregion
    }
}
