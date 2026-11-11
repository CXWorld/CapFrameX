#pragma once

#include <windows.h>

#pragma warning(push)
#pragma warning(disable:4458) // declaration of 'xxx' hides class member
#include <gdiplus.h>
#pragma warning(pop)

#include <msiquery.h>
#include <objbase.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <stdlib.h>
#include <strsafe.h>
#include <CommCtrl.h>

// WiX native SDK (WixToolset.WixStandardBootstrapperApplicationFunctionApi and the
// BootstrapperApplicationApi / DUtil packages it depends on), same set as the WiX
// BAFunctions sample.
#include "dutil.h"
#include "dictutil.h"
#include "fileutil.h"
#include "pathutil.h"
#include "strutil.h"
#include "regutil.h"

#include "BootstrapperApplicationBase.h"

#include "BAFunctions.h"
#include "IBAFunctions.h"
