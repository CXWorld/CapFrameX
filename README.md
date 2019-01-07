# CapFrameX
Frametimes analysis tool compatible with OCAT v1.3

## Release
Download link: https://github.com/DevTechProfile/CapFrameX/releases

# Single record analysis
![Screenshot](Images/Single_Record.png)

# Record comparison
![Screenshot](Images/Record_Comparison.png)

# L-shape analysis
![Screenshot](Images/Lshape_Comparison.png)

# Report table (Excel)
![Screenshot](Images/Report_Table.png)

# Current feature list
* Automatic file record (OCAT) management/directory observer
* Selectable records in DataGrid/ searching in DataGrid
* Displaying frametime graph and moving average
* Scrollable sliding window on frametime data
* Calculating and displaying basic parameter (average, p-quantiles, min)
* Calculating and displaying adaptive standard deviation
* Calculating and displaying stuttering percentage
* Calculating and displaying L-shape curve
* Removing outliers
* Record comparison (performance parameter, L-shape analysis)
* Displaying system info from OCAT record file
* Export performance parameter and graphs (Excel)
* Export comparison table as report (Excel)

# Requirements
* .NET 4.7
* OCAT v1.3 (capturing frametime data)

# Build requirements
* MS Visual Studio 2017 only (Community Edition)
* WiX V3.11.1
* WiX Toolset Visual Studio 2017 Extension
* WiX Toolset and VS Extension: http://wixtoolset.org/releases/

# Dev roadmap
* Some improvements
* Special features/next steps

# Special features
* Simulation: push various synthetic sequences
* Simulation: push real-world data (data from OCAT)
* Watch simultaneous live chart
* Watching smoothness/stuttering and aliasing effects at high-contrast transitions due to low FPS
* 2D/3D animation
* 3D animation using adaptive sync

