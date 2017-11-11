using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AIS_AddIn
{
    #region Converters
    // Define a converter that will set visibility to Visible/Collapsed in XAML based on a boolean variable
    internal class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is Visibility && (Visibility)value == Visibility.Visible);
        }
    }
    #endregion

    #region Dock Pane
    // Class to display DockPane when the corresponding button has been clicked 
    internal class Dockpane1_ShowButton : ArcGIS.Desktop.Framework.Contracts.Button
    {
        protected override void OnClick()
        {
            TemporalSelection.Show();
        }
    }
    #endregion

    #region Data Grid
    // Class definition for Scene which is used by the DataGrid in XAML to auto-generate columns
    internal class Scene
    {
        public string AcqDate { get; set; }
        public string CloudCover { get; set; }
        public string Name { get; set; }
        public string ObjectID { get; set; }
    }

    #endregion
    internal class TemporalSelection : DockPane
    {
        #region Fields

        private List<Dictionary<string, string>> _selection = null;
        private RelayCommand _lockCommand = null;
        private RelayCommand _mapToolCommand = null;
        private RelayCommand _addToMapCommand = null;
        private ICollection<object> _selectedList = null;
        private string _groupLayerName = string.Empty;
        private Layer SelectedLayer = null;
        #endregion

        #region Properties

        public bool GroupNameVisibility { get; private set; }

        public ObservableCollection<Scene> Scenes { get; private set; }
        public bool DataGridEnabled { get; private set; }
        public bool IsLockButtonEnabled { get; private set; }
        public string GroupLayerName
        {
            get
            {
                return _groupLayerName;
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _groupLayerName = value;

                    IsAddToMapEnabled = true;
                    NotifyPropertyChanged(() => IsAddToMapEnabled);
                }
                else
                {
                    _groupLayerName = value;
                }
            }
        }
        public bool IsAddToMapEnabled { get; set; }

        public ICommand LockCommand
        {
            get { return _lockCommand; }
        }

        public ICommand MapToolCommand
        {
            get { return _mapToolCommand; }
        }

        public ICommand AddToMapCommand
        {
            get { return _addToMapCommand; }
        }
        #endregion

        #region Overrides

        // This method will be called when the DockPane is Initialized
        protected override Task InitializeAsync()
        {
            base.InitializeAsync();
            GroupNameVisibility = false;
            NotifyPropertyChanged(() => GroupNameVisibility);

            _selection = new List<Dictionary<string, string>>();

            Scenes = new ObservableCollection<Scene>();
            NotifyPropertyChanged(() => Scenes);

            _mapToolCommand = new RelayCommand(() => MapToolInit(), () => { return true; });
            NotifyPropertyChanged(() => MapToolCommand);

            _lockCommand = new RelayCommand((SelectedItem) => LockScenes(SelectedItem), () => { return true; });
            NotifyPropertyChanged(() => LockCommand);

            _addToMapCommand = new RelayCommand(() => AddToMap(), () => { return true; });
            NotifyPropertyChanged(() => AddToMapCommand);

            return Task.FromResult(0);
        }

        #endregion

        #region Internal Methods

        // This method finds the corresponding DAML ID definition and diplays it
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find("AIS_AddIn_Dockpane1");
            if (pane == null)
                return;

            pane.Activate();
        }

        #endregion

        #region Private Methods

        private static Task<Map> GetMapFromProject(Project project, string mapName)
        {
            // Return null if either of the two parameters are invalid.
            if (project == null || string.IsNullOrEmpty(mapName))
                return null;

            // Find the first project item with name matches with mapName
            MapProjectItem mapProjItem =
                project.GetItems<MapProjectItem>().FirstOrDefault(item => item.Name.Equals(mapName, StringComparison.CurrentCultureIgnoreCase));

            if (mapProjItem != null)
                return QueuedTask.Run<Map>(() => { return mapProjItem.GetMap(); }, Progressor.None);
            else
                return null;
        }

        // This method is called when the Select Scene tool is clicked on the DockPane
        private void MapToolInit()
        {
            FrameworkApplication.CurrentTool = "AIS_AddIn_MapTool1";
            GetMapPoint.Click += CustomPopupTool_Click;

            IsLockButtonEnabled = true;
            NotifyPropertyChanged(() => IsLockButtonEnabled);

            DataGridEnabled = true;
            NotifyPropertyChanged(() => DataGridEnabled);

            SelectedLayer = null;

            Scenes = new ObservableCollection<Scene>();
            NotifyPropertyChanged(() => Scenes);

            GroupNameVisibility = false;
            NotifyPropertyChanged(() => GroupNameVisibility);

            GroupLayerName = String.Empty;
            NotifyPropertyChanged(() => GroupLayerName);
        }

        // This method will be called when the AddToMap button is clicked, this button will be displayed only if more than one scene is selected in the Datagrid
        private async void AddToMap()
        {
            if (SelectedLayer is ImageServiceLayer)
            {
                await QueuedTask.Run(() =>
                {
                    ImageServiceLayer imageServiceLayer = (ImageServiceLayer)SelectedLayer;
                    // Store information of original service layer so that it can be displayed back again 
                    CIMMosaicRule originalMosaicRule = imageServiceLayer.GetMosaicRule();
                    string originalLayerName = imageServiceLayer.Name;
                    //Map _map = await GetMapFromProject(Project.Current, "Map");
                    Map _map = MapView.Active.Map;
                    // Create a Group Layer which will act as a cotainer where selected layers will be added 
                    GroupLayer grplayer = (GroupLayer)LayerFactory.Instance.CreateGroupLayer(_map, 0, _groupLayerName);
                    int _sceneCount = 0;
                    foreach (object obj in _selectedList.Reverse<object>())
                    {
                        Scene scene = obj as Scene;
                        CIMMosaicRule mosaicRule = new CIMMosaicRule();
                        mosaicRule.MosaicMethod = RasterMosaicMethod.LockRaster;
                        mosaicRule.LockRasterID = scene.ObjectID;
                        ((ImageServiceLayer)imageServiceLayer).SetMosaicRule(mosaicRule);
                        imageServiceLayer.SetName(scene.Name);
                        imageServiceLayer.SetVisibility(false);
                        int ListCount = _selectedList.Count - 1;
                        if (_sceneCount == ListCount)
                        {
                            imageServiceLayer.SetVisibility(true);
                        }
                        else
                        {
                            imageServiceLayer.SetVisibility(false);
                            _sceneCount = _sceneCount + 1;
                        }
                        LayerFactory.Instance.CopyLayer(imageServiceLayer, grplayer);
                    }
                    // Done to display original image service layer
                    imageServiceLayer.SetMosaicRule(originalMosaicRule);
                    imageServiceLayer.SetName(originalLayerName);
                    imageServiceLayer.SetVisibility(false);
                    SelectedLayer = imageServiceLayer;
                    // Set visibilty of Group Layer to be true by default
                    grplayer.SetVisibility(true);

                    // Once the user has entered Group Layer Name, reset the Stack Panel
                    GroupLayerName = "";
                    NotifyPropertyChanged(() => GroupLayerName);

                    GroupNameVisibility = false;
                    NotifyPropertyChanged(() => GroupNameVisibility);
                });
            }
        }

        // Do a Query request on the selected point to get the list of overlapping scenes
        private Task ExtractInfoAsync(string X, string Y)
        {
            return Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    if (Scenes != null)
                    {
                        Scenes.Clear();
                        _selection.Clear();
                    }
                    // Get the image service URL from the current layer
                    SelectedLayer = MapView.Active.GetSelectedLayers().FirstOrDefault();
                    if (SelectedLayer is ImageServiceLayer)
                    {
                        string ImageServiceURL = null;
                        ImageServiceLayer imageServiceLayer = (ImageServiceLayer)SelectedLayer;
                        await QueuedTask.Run(() =>
                        {
                            CIMDataConnection DataConnection = imageServiceLayer.GetDataConnection();
                            string DataConnectionXMLString = DataConnection.ToXml();
                            var ParsedDataConnectionString = XElement.Parse(DataConnectionXMLString);
                            var URLNode = ParsedDataConnectionString.Elements("URL");
                            foreach (XElement nodeValue in URLNode)
                            {
                                ImageServiceURL = nodeValue.ToString();
                            }
                        });

                        // Send a request to the REST end point to get information
                        var point = "{x:" + X + ",y:" + Y + "}";
                        string[] URLSplit = Regex.Split(ImageServiceURL, "arcgis/");
                        //string ServiceURL = "https://landsat2.arcgis.com/";
                        string ServiceURL = URLSplit[0].Replace("<URL>", string.Empty);
                        string url = ServiceURL + "query";
                        var ubldr = new UriBuilder(ServiceURL);
                        string path = "arcgis/rest/" + URLSplit[1].Replace("</URL>", string.Empty) + "/query";
                        ubldr.Path = path;
                        //ubldr.Path = $"arcgis/rest/services/Landsat/MS/ImageServer/query";

                        // Allows to give parameters as dictionary key,pair values rather than manually constructing a json 
                        var query = HttpUtility.ParseQueryString(ubldr.Query);
                        query["f"] = "json";
                        query["geometry"] = point;
                        query["geometryType"] = "esriGeometryPoint";
                        query["spatialRel"] = "esriSpatialRelIntersects";
                        query["outFields"] = "*";
                        query["pixelSize"] = "30";
                        query["returnGeometry"] = "false";
                        ubldr.Query = query.ToString();
                        var httpClient = new EsriHttpClient();
                        var response = httpClient?.Get(ubldr.Uri.ToString());
                        var respstr = await response?.Content?.ReadAsStringAsync();
                        var SceneList = JObject.Parse(respstr);
                        var SceneListInfo = (JArray)SceneList["features"];
                        foreach (JObject value in SceneListInfo)
                        {
                            foreach (var property in value.Properties())
                                if (property.Name == "attributes")
                                {
                                    {
                                        // Add obtained json response as a dictionary to a list of dictionaries
                                        var attribute = property.Value.ToString();
                                        var attributeValue = JObject.Parse(attribute);
                                        var category = Int32.Parse(attributeValue["Category"].ToString());
                                        var attributeValueDict = attributeValue.ToObject<Dictionary<string, string>>();
                                        if (category == 1)
                                        {
                                            _selection.Add(attributeValueDict);
                                        }
                                    }
                                }
                        }

                        foreach (var value in _selection)
                        {
                            // Extract and convert epoch time to dd/MM/yyyy format
                            double UnixTime = Convert.ToDouble(value["AcquisitionDate"].ToString());
                            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            DateTime date = epoch.AddMilliseconds(UnixTime);
                            var AcqDate = date.ToString("dd / MM / yyyy");
                            var objID = value["OBJECTID"].ToString();
                            var ClCover = value["CloudCover"].ToString();
                            var SceneName = value["Name"].ToString();
                            Scenes.Add(new Scene { AcqDate = AcqDate, Name = SceneName, CloudCover = ClCover, ObjectID = objID });
                        }
                        // Add recieved information to an Observable Collection and notfy UI
                        NotifyPropertyChanged(() => Scenes);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                }
            });

        }

        #endregion

        #region Event Handlers

        private void CustomPopupTool_Click(GetMapPoint c, EventArgs E)
        {
            try
            {
                // Get the Map-Coordinates from the generated event
                string X = c.MapCoord.Item1.ToString();
                string Y = c.MapCoord.Item2.ToString();
                ExtractInfoAsync(X, Y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        // This method will be called when the lock button is clicked
        private async void LockScenes(object sender)
        {
            // Disable the AddToMap button 
            IsAddToMapEnabled = false;
            NotifyPropertyChanged(() => IsAddToMapEnabled);

            if ((sender as ICollection<object>).Count == 0)
                return;

            _selectedList = sender as ICollection<object>;
            int _selectedListCount = _selectedList.Count;

            // This will be hit if only one scene has been selected 
            if (_selectedListCount == 1)
            {
                if (SelectedLayer is ImageServiceLayer)
                {
                    await QueuedTask.Run(() =>
                    {
                        //Map _map = await GetMapFromProject(Project.Current, "Map");
                        Map _map = MapView.Active.Map;
                        ImageServiceLayer imageServiceLayer = (ImageServiceLayer)SelectedLayer;
                        ImageServiceLayer originalServiceLayer = (ImageServiceLayer)LayerFactory.Instance.CopyLayer(imageServiceLayer, _map, 1);
                        originalServiceLayer.SetVisibility(false);
                        object obj = _selectedList.First();
                        Scene scene = obj as Scene;
                        CIMMosaicRule mosaicRule = new CIMMosaicRule();
                        mosaicRule.MosaicMethod = RasterMosaicMethod.LockRaster;
                        mosaicRule.LockRasterID = scene.ObjectID;
                        imageServiceLayer.SetName(scene.Name);
                        ((ImageServiceLayer)imageServiceLayer).SetMosaicRule(mosaicRule);
                        imageServiceLayer.SetVisibility(true);
                        SelectedLayer = originalServiceLayer;
                    });
                }
            }
            else if (_selectedList.Count > 1)
            {
                // If more than one record has been selected, display the text box to accept GroupName
                GroupNameVisibility = true;
                NotifyPropertyChanged(() => GroupNameVisibility);
            }

            // Once the lock button has been clicked it should not be clickable again, the same goes for the DataGrid

            //IsLockButtonEnabled = false;
            //NotifyPropertyChanged(() => IsLockButtonEnabled);

            //DataGridEnabled = false;
            //NotifyPropertyChanged(() => DataGridEnabled);

        }

        #endregion
    }
}
