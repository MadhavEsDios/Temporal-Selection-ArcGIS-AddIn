using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace AIS_AddIn
{
    // Declare struct in namespace to get X and Y co-ordinates of the clicked point
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // Define class for Map Tool which will retrieve clikced point in Map Co-ordinates 
    internal class GetMapPoint : MapTool
    {
        public static event ClickHandler Click;
        public EventArgs E = null;
        public delegate void ClickHandler(GetMapPoint c, EventArgs E);
        public Tuple<string, string> _mapCoord;
        public Tuple<string, string> MapCoord
        {
            get { return _mapCoord; }
            set
            {
                _mapCoord = value;
                if (Click != null) Click(this, E);

            }
        }

        public GetMapPoint()
        {
            IsSketchTool = true;
            // Specify that selection type will be a point
            SketchType = SketchGeometryType.Point;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task OnToolActivateAsync(bool active)
        {
            return base.OnToolActivateAsync(active);
        }

        // Checks if a point has been clicked on current MapView and handels the event accordingly
        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                e.Handled = true;
        }

        // Event Handler for OnToolMouseDown which returns the clicked point in Map-Coordinates
        protected async override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            await QueuedTask.Run(() =>
            {
                var mapPoint = MapView.Active.ClientToMap(e.ClientPoint);
                MapCoord = new Tuple<string, string>(mapPoint.X.ToString(), mapPoint.Y.ToString());
                return true;

            });
        }
    }
}
