# CapFrameX
Frametimes analysis tool compatible with OCAT v1.3 and 1.4

# Release
Download link: https://github.com/DevTechProfile/CapFrameX/releases

# Single record analysis
![Screenshot](Images/Single_Record.png)

# Record comparison
![Screenshot](Images/Record_Comparison.png)

# L-shape analysis
![Screenshot](Images/Lshape_Comparison.png)

# Synchronization info (G-Sync, FreeSync)
![Screenshot](Images/Synchronization_info.png)

# Report table (Excel)
![Screenshot](Images/Report_Table.png)

# Settings dialog
![Screenshot](Images/Settings_Dialog.png)

# Current feature list
* Automatic record file (OCAT) management/directory observer
* Selectable data source (OCAT recordings)
* Selectable recordings in DataGrid/ searching in DataGrid
* Displaying system info from OCAT record file
* Editing comments and system info
* Direct editing comments and system info in record list
* Displaying frametime graph and moving average
* Displaying frametime and display time graph (G-Sync and FreeSync synced vs. dropped frames)
* Scrollable sliding window on frametime data
* Calculating and displaying basic parameter (average, p-quantiles, min)
* Calculating and displaying low average parameter (0.1% and 1% low)
* Calculating and displaying adaptive standard deviation
* Calculating and displaying stuttering percentage (time)
* Calculating and displaying L-shape curve
* Cutting frametime graphs ("Single Record" page)
* Removing outliers
* Record comparison (performance parameter, L-shape analysis)
* Export performance parameter and graphs (Excel)
* Export comparison table as report (Excel)
* Export PNG report picture

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
* New live chats
* Approximation inputlag
* Special features/next steps

# Special features
* Simulation: push various synthetic sequences
* Simulation: push real-world data (data from OCAT)
* Watch simultaneous live chart
* Watching smoothness/stuttering and aliasing effects at high-contrast transitions due to low FPS
* 2D/3D animation
* 3D animation using adaptive sync

