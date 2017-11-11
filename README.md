# Temporal-Selection-ArcGIS-AddIn
This is a Temporal Selection add-in created using the ArcGIS Pro C# add-in API

This is a project to create an ADD-IN in ArcGIS Pro which will allow users to add an image service from a dropdown
with a selected RFT.
Further the user can click on the Temporal Selection Dockpane.
After this the user clicks on the "Select Scene" button which opens a Map Tool, which can be utilized by the user to
select a point on the image service layer.
Clicking a point populates the DataGrid and then selecting one record and clicking on the lock button locks to the selected raster id

To select multiple records click on the records pressing the "ctrl" button and then click on the lock button.
After this the dockpane will ask the user to enter a Group Layer name, and to lock to the specified rasters click on the AddToMap Button


Installation:

For debugging, build the project in debug mode and click on start, it will open ArcGIS Pro, then click on the Add-In tab to see your add-in.

For others who want to install the add-in without building the project,
build your project in release mode. Then go to your project location/bin folder
In this bin there will be 2 folders i.e. debug,release.
Click on release to see the esri addin file.
Now copy the whole release file and send it to whomsoever concerned.
