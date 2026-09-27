// IGCL telemetry probe: shows which power telemetry items the installed Intel graphics driver
// reports through ctlPowerTelemetryGet (structure version 0 and 1) and ctlPowerTelemetryGetV2,
// and compares the activity and power rates both functions yield over the same intervals.
// Built against source/CapFrameX.IGCL so it uses the SDK version CapFrameX ships. See README.md.
#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <vector>
#include "igcl_api.h"

namespace
{
    // Items present in both ctl_power_telemetry_t and ctl_power_telemetry_v2_t.
#define CFX_TELEMETRY_ITEMS(X) \
    X(timeStamp) X(gpuEnergyCounter) X(gpuVoltage) X(gpuCurrentClockFrequency) X(gpuCurrentTemperature) \
    X(globalActivityCounter) X(renderComputeActivityCounter) X(mediaActivityCounter) \
    X(vramEnergyCounter) X(vramVoltage) X(vramCurrentClockFrequency) X(vramCurrentEffectiveFrequency) \
    X(vramReadBandwidthCounter) X(vramWriteBandwidthCounter) X(vramCurrentTemperature) X(totalCardEnergyCounter) \
    X(gpuVrTemp) X(vramVrTemp) X(saVrTemp) X(gpuEffectiveClock) X(gpuOverVoltagePercent) \
    X(gpuPowerPercent) X(gpuTemperaturePercent) X(vramReadBandwidth) X(vramWriteBandwidth)

    struct Item
    {
        std::string name;
        bool supported = false;
        ctl_units_t units = CTL_UNITS_UNKNOWN;
        double value = 0.0;
    };

    struct Snapshot
    {
        const char* label = "";
        ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;
        std::vector<Item> items;
        std::vector<std::pair<const char*, bool>> limits;
        ctl_psu_type_t psuTypes[CTL_PSU_COUNT] = {};

        bool Ok() const { return result == CTL_RESULT_SUCCESS; }

        const Item* Find(const std::string& name) const
        {
            for (const Item& item : items)
                if (item.name == name)
                    return &item;
            return nullptr;
        }

        bool Supports(const std::string& name) const
        {
            const Item* item = Find(name);
            return Ok() && item != nullptr && item->supported;
        }
    };

    double ItemValue(const ctl_oc_telemetry_item_t& item)
    {
        switch (item.type)
        {
        case CTL_DATA_TYPE_INT8: return item.value.data8;
        case CTL_DATA_TYPE_UINT8: return item.value.datau8;
        case CTL_DATA_TYPE_INT16: return item.value.data16;
        case CTL_DATA_TYPE_UINT16: return item.value.datau16;
        case CTL_DATA_TYPE_INT32: return item.value.data32;
        case CTL_DATA_TYPE_UINT32: return item.value.datau32;
        case CTL_DATA_TYPE_INT64: return static_cast<double>(item.value.data64);
        case CTL_DATA_TYPE_UINT64: return static_cast<double>(item.value.datau64);
        case CTL_DATA_TYPE_FLOAT: return item.value.datafloat;
        default: return item.value.datadouble;
        }
    }

    const char* UnitName(ctl_units_t units)
    {
        switch (units)
        {
        case CTL_UNITS_FREQUENCY_MHZ: return "MHz";
        case CTL_UNITS_OPERATIONS_GTS: return "GT/s";
        case CTL_UNITS_OPERATIONS_MTS: return "MT/s";
        case CTL_UNITS_VOLTAGE_VOLTS: return "V";
        case CTL_UNITS_POWER_WATTS: return "W";
        case CTL_UNITS_TEMPERATURE_CELSIUS: return "C";
        case CTL_UNITS_ENERGY_JOULES: return "J";
        case CTL_UNITS_TIME_SECONDS: return "s";
        case CTL_UNITS_MEMORY_BYTES: return "B";
        case CTL_UNITS_ANGULAR_SPEED_RPM: return "RPM";
        case CTL_UNITS_POWER_MILLIWATTS: return "mW";
        case CTL_UNITS_PERCENT: return "%";
        case CTL_UNITS_MEM_SPEED_GBPS: return "GB/s";
        case CTL_UNITS_VOLTAGE_MILLIVOLTS: return "mV";
        case CTL_UNITS_BANDWIDTH_MBPS: return "MB/s";
        default: return "?";
        }
    }

    const char* PsuTypeName(ctl_psu_type_t type)
    {
        switch (type)
        {
        case CTL_PSU_TYPE_PSU_PCIE: return "PCIe slot";
        case CTL_PSU_TYPE_PSU_6PIN: return "6-pin";
        case CTL_PSU_TYPE_PSU_8PIN: return "8-pin";
        default: return "unknown";
        }
    }

    const char* ResultName(ctl_result_t result)
    {
        switch (result)
        {
        case CTL_RESULT_SUCCESS: return "success";
        case CTL_RESULT_ERROR_NOT_INITIALIZED: return "not initialized (function not exported by this driver)";
        case CTL_RESULT_ERROR_UNSUPPORTED_VERSION: return "unsupported version";
        case CTL_RESULT_ERROR_UNSUPPORTED_FEATURE: return "unsupported feature";
        case CTL_RESULT_ERROR_NOT_IMPLEMENTED: return "not implemented";
        case CTL_RESULT_ERROR_DEVICE_UNAVAILABLE: return "device unavailable";
        case CTL_RESULT_ERROR_DEVICE_LOST: return "device lost";
        default: return "error";
        }
    }

    template <typename T>
    void Collect(const T& telemetry, Snapshot& snapshot)
    {
        auto add = [&snapshot](std::string name, bool parentSupported, const ctl_oc_telemetry_item_t& item)
        {
            snapshot.items.push_back({ std::move(name), parentSupported && item.bSupported, item.units, ItemValue(item) });
        };

#define CFX_ADD_ITEM(field) add(#field, true, telemetry.field);
        CFX_TELEMETRY_ITEMS(CFX_ADD_ITEM)
#undef CFX_ADD_ITEM

        for (int i = 0; i < CTL_FAN_COUNT; ++i)
            add("fanSpeed[" + std::to_string(i) + "]", true, telemetry.fanSpeed[i]);

        for (int i = 0; i < CTL_PSU_COUNT; ++i)
        {
            const std::string prefix = "psu[" + std::to_string(i) + "]";
            add(prefix + ".energyCounter", telemetry.psu[i].bSupported, telemetry.psu[i].energyCounter);
            add(prefix + ".voltage", telemetry.psu[i].bSupported, telemetry.psu[i].voltage);
            snapshot.psuTypes[i] = telemetry.psu[i].bSupported ? telemetry.psu[i].psuType : CTL_PSU_TYPE_PSU_NONE;
        }

        // Throttle reasons carry no bSupported flag; false is indistinguishable from unsupported.
        snapshot.limits = {
            { "gpuPowerLimited", telemetry.gpuPowerLimited },
            { "gpuTemperatureLimited", telemetry.gpuTemperatureLimited },
            { "gpuCurrentLimited", telemetry.gpuCurrentLimited },
            { "gpuVoltageLimited", telemetry.gpuVoltageLimited },
            { "gpuUtilizationLimited", telemetry.gpuUtilizationLimited },
        };
    }

    Snapshot QueryV1(ctl_device_adapter_handle_t device, uint8_t version, const char* label)
    {
        Snapshot snapshot;
        snapshot.label = label;

        ctl_power_telemetry_t telemetry = {};
        telemetry.Size = sizeof(telemetry);
        telemetry.Version = version;
        snapshot.result = ctlPowerTelemetryGet(device, &telemetry);
        if (snapshot.Ok())
            Collect(telemetry, snapshot);

        return snapshot;
    }

    Snapshot QueryV2(ctl_device_adapter_handle_t device)
    {
        Snapshot snapshot;
        snapshot.label = "V2";

        ctl_power_telemetry_v2_t telemetry = {};
        telemetry.Size = sizeof(telemetry);
        telemetry.Version = 1;
        snapshot.result = ctlPowerTelemetryGetV2(device, &telemetry);
        if (snapshot.Ok())
            Collect(telemetry, snapshot);

        return snapshot;
    }

    void PrintDifference(const char* title, const Snapshot& reporting, const Snapshot& missing)
    {
        printf("  %s:", title);
        if (!reporting.Ok() || !missing.Ok())
        {
            printf(" n/a\n");
            return;
        }

        bool any = false;
        for (const Item& item : reporting.items)
        {
            if (item.supported && !missing.Supports(item.name))
            {
                printf("%s %s", any ? "," : "", item.name.c_str());
                any = true;
            }
        }

        printf("%s\n", any ? "" : " none");
    }

    void PrintSupportMatrix(const std::vector<const Snapshot*>& snapshots)
    {
        printf("\nSupported items (yes = bSupported; value and unit from the first variant reporting it)\n");
        printf("  %-30s", "item");
        for (const Snapshot* snapshot : snapshots)
            printf(" %-6s", snapshot->label);
        printf(" %-5s %s\n", "unit", "value");

        for (size_t i = 0; i < snapshots.front()->items.size(); ++i)
        {
            const std::string& name = snapshots.front()->items[i].name;
            const Item* reported = nullptr;

            printf("  %-30s", name.c_str());
            for (const Snapshot* snapshot : snapshots)
            {
                const bool supported = snapshot->Supports(name);
                printf(" %-6s", supported ? "yes" : "-");
                if (supported && reported == nullptr)
                    reported = snapshot->Find(name);
            }

            if (reported != nullptr)
                printf(" %-5s %.4f", UnitName(reported->units), reported->value);
            printf("\n");
        }

        for (const Snapshot* snapshot : snapshots)
        {
            printf("  PSU types (%s):", snapshot->label);
            for (int i = 0; i < CTL_PSU_COUNT; ++i)
                printf(" [%d] %s", i, PsuTypeName(snapshot->psuTypes[i]));
            printf("\n");
        }

        printf("  Throttle flags (V1 v1, no bSupported flag):");
        for (const auto& limit : snapshots[1]->limits)
            printf(" %s=%d", limit.first, limit.second ? 1 : 0);
        printf("\n");
    }

    struct Rates
    {
        bool valid = false;
        double global = 0, render = 0, media = 0, gpuPower = 0, cardPower = 0;
        bool cardPowerSupported = false;
    };

    Rates ComputeRates(const Snapshot& previous, const Snapshot& current)
    {
        Rates rates;
        if (!previous.Ok() || !current.Ok())
            return rates;

        auto delta = [&](const char* name) { return current.Find(name)->value - previous.Find(name)->value; };
        const double dt = delta("timeStamp");
        if (dt <= 0)
            return rates;

        rates.valid = true;
        rates.global = 100.0 * delta("globalActivityCounter") / dt;
        rates.render = 100.0 * delta("renderComputeActivityCounter") / dt;
        rates.media = 100.0 * delta("mediaActivityCounter") / dt;
        rates.gpuPower = delta("gpuEnergyCounter") / dt;
        rates.cardPowerSupported = current.Supports("totalCardEnergyCounter");
        rates.cardPower = rates.cardPowerSupported ? delta("totalCardEnergyCounter") / dt : 0.0;
        return rates;
    }

    void PrintRates(const Rates& rates)
    {
        if (!rates.valid)
        {
            printf(" %8s %8s %8s %8s %8s", "-", "-", "-", "-", "-");
            return;
        }

        printf(" %8.2f %8.2f %8.2f %8.2f", rates.global, rates.render, rates.media, rates.gpuPower);
        if (rates.cardPowerSupported)
            printf(" %8.2f", rates.cardPower);
        else
            printf(" %8s", "-");
    }

    void CompareRates(ctl_device_adapter_handle_t device, int seconds, bool v2Available)
    {
        printf("\nActivity and power over %d one-second intervals, V1 v1 and V2 sampled back to back\n", seconds);
        printf("(run a game or benchmark now; V1 global is what CapFrameX shows as GPU load)\n");
        printf("  %3s | %8s %8s %8s %8s %8s | %8s %8s %8s %8s %8s\n", "s",
            "V1 glob%", "rend%", "media%", "GPU W", "card W", "V2 glob%", "rend%", "media%", "GPU W", "card W");

        Snapshot previousV1 = QueryV1(device, 1, "V1 v1");
        Snapshot previousV2 = v2Available ? QueryV2(device) : Snapshot();

        for (int second = 1; second <= seconds; ++second)
        {
            Sleep(1000);
            Snapshot currentV1 = QueryV1(device, 1, "V1 v1");
            Snapshot currentV2 = v2Available ? QueryV2(device) : Snapshot();

            printf("  %3d |", second);
            PrintRates(ComputeRates(previousV1, currentV1));
            printf(" |");
            PrintRates(ComputeRates(previousV2, currentV2));
            printf("\n");

            previousV1 = currentV1;
            previousV2 = currentV2;
        }
    }

    void ProbeDevice(uint32_t index, ctl_device_adapter_handle_t device, int seconds)
    {
        LUID luid = {};
        ctl_device_adapter_properties_t properties = {};
        properties.Size = sizeof(properties);
        properties.pDeviceID = &luid;
        properties.device_id_size = sizeof(luid);

        if (ctlGetDeviceProperties(device, &properties) != CTL_RESULT_SUCCESS ||
            properties.device_type != CTL_DEVICE_TYPE_GRAPHICS)
            return;

        LARGE_INTEGER driverVersion;
        driverVersion.QuadPart = static_cast<LONGLONG>(properties.driver_version);
        const bool integrated = (properties.graphics_adapter_properties & CTL_ADAPTER_PROPERTIES_FLAG_INTEGRATED) != 0;

        printf("\n=== Adapter %u: %s (PCI %04X:%04X rev %02X, %s)\n", index, properties.name,
            properties.pci_vendor_id, properties.pci_device_id, properties.rev_id, integrated ? "integrated" : "discrete");
        printf("    Driver %d.%d.%d.%d\n", HIWORD(driverVersion.HighPart), LOWORD(driverVersion.HighPart),
            HIWORD(driverVersion.LowPart), LOWORD(driverVersion.LowPart));

        const Snapshot v1v0 = QueryV1(device, 0, "V1 v0");
        const Snapshot v1v1 = QueryV1(device, 1, "V1 v1");
        const Snapshot v2 = QueryV2(device);

        printf("\nCalls\n");
        for (const Snapshot* snapshot : { &v1v0, &v1v1, &v2 })
            printf("  %-6s 0x%08X %s\n", snapshot->label, static_cast<unsigned>(snapshot->result), ResultName(snapshot->result));
        printf("  (V1 = ctlPowerTelemetryGet with structure version 0 or 1, V2 = ctlPowerTelemetryGetV2)\n");

        if (!v1v0.Ok() && !v1v1.Ok() && !v2.Ok())
            return;

        PrintSupportMatrix({ &v1v0, &v1v1, &v2 });

        printf("\nSummary\n");
        PrintDifference("Added by structure version 1", v1v1, v1v0);
        PrintDifference("Only reported by V2 (vs V1 v1)", v2, v1v1);
        PrintDifference("Only reported by V1 v1 (vs V2)", v1v1, v2);

        if (v1v1.Ok())
            CompareRates(device, seconds, v2.Ok());
    }
}

int main(int argc, char** argv)
{
    int seconds = argc > 1 ? atoi(argv[1]) : 10;
    if (seconds < 1)
        seconds = 1;

    ctl_init_args_t initArgs = {};
    initArgs.Size = sizeof(initArgs);
    initArgs.AppVersion = CTL_MAKE_VERSION(CTL_IMPL_MAJOR_VERSION, CTL_IMPL_MINOR_VERSION);
    initArgs.flags = CTL_INIT_FLAG_USE_LEVEL_ZERO;

    ctl_api_handle_t api = nullptr;
    const ctl_result_t initResult = ctlInit(&initArgs, &api);
    printf("IGCL telemetry probe (SDK header API %d.%d)\n", CTL_IMPL_MAJOR_VERSION, CTL_IMPL_MINOR_VERSION);
    if (initResult != CTL_RESULT_SUCCESS)
    {
        printf("ctlInit failed: 0x%08X %s\n", static_cast<unsigned>(initResult), ResultName(initResult));
        return 1;
    }

    uint32_t count = 0;
    ctlEnumerateDevices(api, &count, nullptr);
    std::vector<ctl_device_adapter_handle_t> devices(count);
    if (count > 0)
        ctlEnumerateDevices(api, &count, devices.data());
    printf("Adapters: %u\n", count);

    for (uint32_t i = 0; i < count; ++i)
        ProbeDevice(i, devices[i], seconds);

    ctlClose(api);
    return 0;
}
