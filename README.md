# CapFrameX
Frametimes capture and analysis tool compatible with most common 3D APIs

# Release
Download link: https://github.com/DevTechProfile/CapFrameX/releases

# Capture frametimes
![Screenshot](Images/Capture_Page.PNG)

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

# Current feature list
Capturing and analysing frametimes. Most common 3D APIs are supported. 

## Capture frametimes
* capture service based on PresentMon
* high-accurate frametime recordings
* predefined capture time
* managing processes to capture
* DirectX and Vulkan are supported
* free configurable hotkeys
* very reliable hotkey hooking

## Single record analysis
* Selectable recordings in DataGrid/ searching in DataGrid
* Displaying system info like CPU and GPU
* Editing comments and system info
* Direct editing comments and system info in record list
* Displaying frametime graph and moving average
* Displaying FPS graph and average line graph
* Cutting frametime graphs ("Single Record" page)
* Calculating and displaying basic parameter (average, p-quantiles, min)
* Calculating and displaying low average parameter (0.1% and 1% low)
* Calculating and displaying adaptive standard deviation
* Calculating and displaying stuttering percentage (time)
* Removing outliers

## Record comparison
* Record comparison (performance parameter, L-shape analysis)

## L-shape analysis
* Calculating and displaying L-shape curve

## Synchronization info (G-Sync, FreeSync)
* Displaying frametime and display changed time graph (G-Sync and FreeSync synced vs. dropped frames)

## Reporting
* Export performance parameter and graphs (Excel)
* Export comparison table as report (Excel)
* Export PNG report picture

# Requirements
* .NET 4.7

# Build requirements
* MS Visual Studio 2017 only (Community Edition)
* WiX V3.11.1
* WiX Toolset Visual Studio 2017 Extension
* WiX Toolset and VS Extension: http://wixtoolset.org/releases/

# Dev roadmap
* Approximation input lag
* Advanced comparison method
* Special features/next steps

