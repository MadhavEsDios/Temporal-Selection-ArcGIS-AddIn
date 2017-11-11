using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using System.Xml.Linq;
using ArcGIS.Desktop.Mapping;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using System.Windows.Data;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.Geometry;
using System.Net.Http;


namespace AIS_AddIn
{
    internal class CustomControl1ViewModel : CustomControl
    {
        // Declare dictionary with corresponding URL's, Keys will be displayed and internally URL will be selected
        Dictionary<string, string> myDict = new Dictionary<string, string>
        {
            {"Landsat","https://landsat2.arcgis.com/arcgis/rest/services/Landsat/MS/ImageServer" },
            {"NAIP","http://naip.arcgis.com/arcgis/rest/services/NAIP/ImageServer" },
            {"WorldElevation","http://elevation.arcgis.com/arcgis/rest/services/WorldElevation/Terrain/ImageServer" },
            {"LandsatGLS","http://imagery.arcgisonline.com/arcgis/rest/services/LandsatGLS/LandsatMaster/ImageServer" }
        };

        // Instantiate Relay Commands which will be maped to Event Changes
        RelayCommand _serviceSelectCommand = null;
        RelayCommand _rftSelectCommand = null;

        //Declare Observable Collection which will reflect changes in UI upon any updation
        public ObservableCollection<string> ServiceList { get; private set; }
        public ObservableCollection<string> RFTList { get; set; }
        public string ServiceURL;

        // Get and Set Properties
        public ICommand ServiceSelectCommand
        {
            get { return _serviceSelectCommand; }
        }

        public ICommand RFTSelectCommand
        {
            get { return _rftSelectCommand; }
        }

        // Constructor
        public CustomControl1ViewModel()
        {
            try
            {
                _serviceSelectCommand = new RelayCommand((SelectedItem) => Service_SelectChanged(SelectedItem), () => { return true; });
                ServiceList = new ObservableCollection<string>
                {
                    "Landsat",
                    "NAIP",
                    "WorldElevation",
                    "LandsatGLS"
                };
                RFTList = new ObservableCollection<string>();
                _rftSelectCommand = new RelayCommand((SelectedItem) => RFT_SelectChanged(SelectedItem), () => { return true; });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        // Event Handler, gets the name of the service selected and queries it to get the list of RFT's
        private async void Service_SelectChanged(object sender)
        {
            try
            {
                ServiceURL = myDict[sender.ToString()];
                if (RFTList != null)
                {
                    RFTList.Clear();
                }
                string Path = $"/rasterFunctionInfos";
                string Query = "?f=json";
                var url = ServiceURL + Path + Query;
                var httpClient = new EsriHttpClient();
                var response = httpClient?.Get(url.ToString());
                var respStr = await response?.Content?.ReadAsStringAsync();
                var rasterFunctionInfo = JObject.Parse(respStr);
                var rasterFunctionInfoValue = (JArray)rasterFunctionInfo["rasterFunctionInfos"];
                if (rasterFunctionInfoValue == null)
                {
                    RFTList.Add("Test");
                }
                else
                {
                    foreach (JObject value in rasterFunctionInfoValue)
                    {
                        foreach (var property in value.Properties())
                        {
                            if (property.Name == "name")
                            {
                                string rasterFunctionTemplate = property.Value.ToString();
                                RFTList.Add(rasterFunctionTemplate);
                            }
                        }
                    }

                }

                // Update Observable Collection to reflect changes in UI
                NotifyPropertyChanged(() => RFTList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        // Event Handler, gets name of selected RFT and changes the rendering rule of the Image Service to be displayed accordingly
        private async void RFT_SelectChanged(object sender)
        {
            try
            {
                
                //Map _map1 = await GetMapFromProject(Project.Current, "Map");
                Map _map = MapView.Active.Map;
                if (_map == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("AddRasterLayerToMap: Failed to get map.");
                    return;
                }

                string dataSoureUrl = ServiceURL;
                ImageServiceLayer imageServiceLayer = null;
                await QueuedTask.Run(() =>
                {
                    imageServiceLayer = (ImageServiceLayer)LayerFactory.Instance.CreateLayer(new Uri(dataSoureUrl), _map);
                    if (imageServiceLayer == null)
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Failed to create layer for url:" + dataSoureUrl);
                        return;
                    }
                    ImageServiceLayer isLayer = imageServiceLayer;
                    // Create a new Rendering rule
                    CIMRenderingRule setRenderingrule = new CIMRenderingRule();
                    // Set the name of the rendering rule.
                    setRenderingrule.Name = sender.ToString();
                    // Update the image service with the new mosaic rule.
                    isLayer.SetRenderingRule(setRenderingrule);
                    string originalLayerName = isLayer.Name;
                    isLayer.SetName(originalLayerName + "_" + sender.ToString());
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        // Function used to get a map object from the current project
        // Currently this function is not being used , it has been replaced by the command MapView.Active.Map
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
    }
}
