#include "precomp.h"
#include "BalBaseBAFunctions.h"
#include "BalBaseBAFunctionsProc.h"

// BAFunctions extension of the WiX standard bootstrapper application (WixStdBA).
//
// Burn refuses to run a bundle while a newer bundle with the same upgrade code is installed:
// it plans that related bundle as "Downgrade", skips planning and fails Apply with
// 0x80070666 (ERROR_PRODUCT_VERSION) before any package runs. WixStdBA has no option to
// allow it - SuppressDowngradeFailure only reports success without installing anything.
//
// CapFrameX supports downgrades on purpose: the MSI is authored with
// MajorUpgrade AllowDowngrades="yes", and the in-app updater rolls back by running an older
// setup. Planning the newer bundle as "Upgrade" instead lets this bundle install its MSI,
// which removes the newer product through its major upgrade, and then retires the newer
// bundle's registration - the same plan Burn uses for a regular upgrade, with the same
// restore of the related bundle if the install rolls back.

class CCapFrameXBAFunctions : public CBalBaseBAFunctions
{
public: // IBootstrapperApplication
    virtual STDMETHODIMP OnPlanRelatedBundleType(
        __in_z LPCWSTR wzBundleCode,
        __in BOOTSTRAPPER_RELATED_BUNDLE_PLAN_TYPE recommendedType,
        __inout BOOTSTRAPPER_RELATED_BUNDLE_PLAN_TYPE* pRequestedType,
        __inout BOOL* /*pfCancel*/
        )
    {
        // Only a newer bundle of this product is planned as Downgrade. WixStdBA has already run
        // its own handler (it plans NONE when only prerequisites are installed), so respect a
        // decision it made instead of the engine's recommendation.
        if (BOOTSTRAPPER_RELATED_BUNDLE_PLAN_TYPE_DOWNGRADE == recommendedType &&
            BOOTSTRAPPER_RELATED_BUNDLE_PLAN_TYPE_DOWNGRADE == *pRequestedType)
        {
            *pRequestedType = BOOTSTRAPPER_RELATED_BUNDLE_PLAN_TYPE_UPGRADE;

            BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "CapFrameX: newer related bundle %ls will be replaced by this version (downgrade).", wzBundleCode);
        }

        return S_OK;
    }

public:
    CCapFrameXBAFunctions(
        __in HMODULE hModule
        ) : CBalBaseBAFunctions(hModule)
    {
    }
};


static HINSTANCE vhInstance = NULL;

extern "C" BOOL WINAPI DllMain(
    __in HINSTANCE hInstance,
    __in DWORD dwReason,
    __in LPVOID /*pvReserved*/
    )
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        ::DisableThreadLibraryCalls(hInstance);
        vhInstance = hInstance;
        break;

    case DLL_PROCESS_DETACH:
        vhInstance = NULL;
        break;
    }

    return TRUE;
}

extern "C" HRESULT WINAPI BAFunctionsCreate(
    __in const BA_FUNCTIONS_CREATE_ARGS* pArgs,
    __inout BA_FUNCTIONS_CREATE_RESULTS* pResults
    )
{
    HRESULT hr = S_OK;
    CCapFrameXBAFunctions* pBAFunctions = NULL;

    // Required for BalLog.
    BalInitialize(pArgs->pEngine);

    pBAFunctions = new CCapFrameXBAFunctions(vhInstance);
    ExitOnNull(pBAFunctions, hr, E_OUTOFMEMORY, "Failed to create the CapFrameX BAFunctions object.");

    hr = pBAFunctions->OnCreate(pArgs->pEngine, pArgs->pCommand);
    ExitOnFailure(hr, "Failed to initialize the CapFrameX BAFunctions object.");

    pResults->pfnBAFunctionsProc = BalBaseBAFunctionsProc;
    pResults->pvBAFunctionsProcContext = pBAFunctions;
    pBAFunctions = NULL;

LExit:
    ReleaseObject(pBAFunctions);

    return hr;
}

extern "C" void WINAPI BAFunctionsDestroy(
    __in const BA_FUNCTIONS_DESTROY_ARGS* /*pArgs*/,
    __inout BA_FUNCTIONS_DESTROY_RESULTS* /*pResults*/
    )
{
    BalUninitialize();
}
