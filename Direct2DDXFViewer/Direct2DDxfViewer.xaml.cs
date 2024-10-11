using netDxf;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Direct2DDXFViewer
{
    /// <summary>
    /// Interaction logic for Direct2DDxfViewer.xaml
    /// </summary>
    public partial class Direct2DDxfViewer : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private string _dxfFilePath = @"DXF\LargeDxf.dxf";

        private Point _dxfPointerCoords = new();
        private Point _pointerCoords = new();
        private int _currentZoomStep;
        #endregion

        #region Properties
        public Point DxfPointerCoords
        {
            get { return _dxfPointerCoords; }
            set
            {
                _dxfPointerCoords = value;
                OnPropertyChanged(nameof(DxfPointerCoords));
            }
        }
        public Point PointerCoords
        {
            get { return _pointerCoords; }
            set
            {
                _pointerCoords = value;
                OnPropertyChanged(nameof(PointerCoords));
            }
        }
        public int CurrentZoomStep
        {
            get { return _currentZoomStep; }
            set
            {
                _currentZoomStep = value;
                OnPropertyChanged(nameof(CurrentZoomStep));
            }
        }
        #endregion

        #region Constructor
        public Direct2DDxfViewer()
        {
            InitializeComponent();

            LoadDxfControl();
            dxfControl.PropertyChanged += DxfControl_PropertyChanged;
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        private void LoadDxfControl()
        {
            DxfDocument dxfDoc = DxfDocument.Load(_dxfFilePath);
            if (dxfDoc is not null) { dxfControl.Initialize(dxfDoc); }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void DxfControl_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(dxfControl.DxfPointerCoords))
            {
                DxfPointerCoords = dxfControl.DxfPointerCoords;
            }
            if (e.PropertyName == nameof(dxfControl.PointerCoords))
            {
                PointerCoords = dxfControl.PointerCoords;
            }
            if (e.PropertyName == nameof(dxfControl.CurrentZoomStep))
            {
               CurrentZoomStep = dxfControl.CurrentZoomStep;
            }
        }

        public void ZoomToExtents()
        {
            dxfControl.ZoomToExtents();
        }
        #endregion

   
    }
}
