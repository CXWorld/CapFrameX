# CapFrameX
Frame capture analysis tool (in process of developing)

<img src="https://github.com/DevTechProfile/CapFrameX/tree/master/Images/CX_UI.png" width="660" height="400" />

# Current feature list
* Automatic file record (OCAT) management/directory observer
* Selectable records in DataGrid/ searching in DataGrid
* Displaying frametime graph and moving average
* Calculating and displaying basic parameter (average, p-quantiles, min)
* Calculating and displaying adaptive standard deviation
* Removing outliers
* Calculating and displaying L-shape curve
* Minimal WIX installer

# Requirements
* .NET 4.7
* OCAT (capturing frametime data)

# Dev Roadmap
* Application architecture based on MVVM pattern und DI (done)
* UI design (work in progess)
* OCAT interface(work in progess)
* Advanced statistical methods (work in progess)
* Record comparing
* Database management
* WIX installer(work in progess)
* First beta phase
* Special features/next steps

# Special features
* Push various synthetic sequences
* Push real-world data (data from OCAT)
* Watch simultaneous live chart
* Watching smoothness/stuttering and aliasing effects at high-contrast transitions due to low FPS
* 2D/3D animation
* 3D animation using adaptive sync

