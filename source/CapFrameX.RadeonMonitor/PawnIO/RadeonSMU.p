//  PawnIO Modules - Modules for various hardware to be used with PawnIO.
//  Copyright (C) 2026  Adrenalift and CapFrameX contributors
//
//  This library is free software; you can redistribute it and/or
//  modify it under the terms of the GNU Lesser General Public
//  License as published by the Free Software Foundation; either
//  version 2.1 of the License, or (at your option) any later version.
//
//  This library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//  Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public
//  License along with this library; if not, write to the Free Software
//  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//
//  SPDX-License-Identifier: LGPL-2.1-or-later

/*
 * Bounded SMU telemetry for Radeon RDNA GPUs.
 *
 * Selects the AMD display adapter with the largest VRAM BAR. SMN access is
 * limited to MP1 C2PMSG, and every physical read is bounded to the selected
 * BAR. Fixed IOCTLs expose public metrics, Navi 21 SVI, and the private
 * RDNA2/RDNA3/RDNA4 monitoring table. No general physical-memory access or
 * framebuffer writes are exposed.
 *
 * Register and layout references: Linux amdgpu smu_cmn.c, amdgpu_device.c,
 * the SMU11/13/14 driver interfaces, and smuio_11_0_0_offset.h.
 */

#include <pawnio.inc>

const MODULE_ABI_VERSION = 5;
const AMD_VENDOR_ID = 0x1002;

// BAR5 PCIE_INDEX2/PCIE_DATA2 offsets.
const SMN_INDEX_OFFSET = 0x38;
const SMN_DATA_OFFSET = 0x3C;

// Fixed Navi 21 SMUIO SVI telemetry range.
const NAVI21_DEVICE_ID_MIN = 0x73A0;
const NAVI21_DEVICE_ID_MAX = 0x73BF;
const NAVI21_SVI_OFFSET = 0x5A00C;
const NAVI21_SVI_DWORDS = 4;

// RDNA3 families supported by the private tool mailbox.
const RDNA3_DEVICE_ID_MIN_0 = 0x7440;
const RDNA3_DEVICE_ID_MAX_0 = 0x746F;
const RDNA3_DEVICE_ID_MIN_1 = 0x7470;
const RDNA3_DEVICE_ID_MAX_1 = 0x749F;

// RDNA4 families supported by the private tool mailbox.
const RDNA4_DEVICE_ID_MIN_0 = 0x7550;
const RDNA4_DEVICE_ID_MAX_0 = 0x756F;
const RDNA4_DEVICE_ID_MIN_1 = 0x7590;
const RDNA4_DEVICE_ID_MAX_1 = 0x75AF;

// Private tool mailbox: MP1 C2PMSG 72/96/98/109.
const RDNA_TOOL_MESSAGE_OFFSET = 0x58A20;
const RDNA_TOOL_RESPONSE_OFFSET = 0x58A80;
const RDNA_TOOL_ARGUMENT_OFFSET = 0x58A88;
const RDNA_TOOL_ARG_V10_OFFSET = 0x58AB4;
const RDNA_TOOL_GET_VERSION = 0x14;
const RDNA_TOOL_GET_ADDRESS_HIGH = 0x07;
const RDNA_TOOL_GET_ADDRESS_LOW = 0x08;
const RDNA_TOOL_REFRESH_TABLE = 0x09;
const RDNA_TOOL_REFRESH_SELECTOR = 4;
const RDNA_TOOL_RESPONSE_OK = 1;
const RDNA_TOOL_RESPONSE_BUSY = 0xFC;
const RDNA_TOOL_RSP_PREREQ = 0xFD;
const RDNA_TOOL_RESPONSE_UNKNOWN = 0xFE;
const RDNA_TOOL_POLL_ATTEMPTS = 10000;
const RDNA_TOOL_POLL_DELAY_US = 100;
const RDNA_TOOL_READ_ATTEMPTS = 5;
const RDNA_TOOL_READ_RETRY_DELAY_US = 10000;

// Framebuffer bounds; Navi 21 73BF/D5 uses the alternate pair.
const NAVI21_FB_BASE_OFFSET = 0xE54C;
const NAVI21_FB_TOP_OFFSET = 0xE550;
const RDNA3_FB_BASE_OFFSET = 0xE4D4;
const RDNA3_FB_TOP_OFFSET = 0xE4D8;
// Covers every fixed register on RDNA4's 512-KiB BAR5.
const RDNA_TOOL_MMIO_MAP_SIZE = 0x80000;
const RDNA_TOOL_TABLE_BYTES = 0x2000;
const RDNA_TOOL_TABLE_QWORDS = RDNA_TOOL_TABLE_BYTES / 8;
const RDNA_TOOL_METADATA_QWORDS = 4;
const RDNA_TOOL_OUTPUT_QWORDS =
    RDNA_TOOL_METADATA_QWORDS + RDNA_TOOL_TABLE_QWORDS;

// MP1 C2PMSG register window and public metrics pointer.
const MP1_C2PMSG_BASE = 0x03B10900;
const MP1_C2PMSG_SPAN = 0x200;
const MP1_C2PMSG_80 = MP1_C2PMSG_BASE + 80 * 4;
const MP1_C2PMSG_81 = MP1_C2PMSG_BASE + 81 * 4;

// Public metrics sizes; SMU11 uses its largest supported layout.
const SMU11_METRICS_DWORDS = 41;  /* 164 bytes */
const SMU13_0_0_METRICS_DWORDS = 61;  /* 244 bytes, Navi 31/32 */
const SMU13_0_7_METRICS_DWORDS = 60;  /* 240 bytes, Navi 33    */
const SMU14_METRICS_DWORDS = 65;  /* 260 bytes */

// Require two stable pointer snapshots before mapping the table.
const METRICS_ADDRESS_READ_ATTEMPTS = 5;
const METRICS_ADDRESS_RETRY_DELAY_US = 10000;

// Radeon discrete-GPU VRAM MC base.
const GPU_VRAM_MC_BASE = 0x8000000000;

// Fallback BAR5 span when size probing is inconclusive.
const REG_SPAN = 0x100000;
const REG_SIZE_MAX = 0x1000000;      /* 16 MB */
const VRAM_SIZE_MAX = 0x1000000000;  /* 64 GB */

// Selected PCI device and apertures.
new g_ready = 0;
new g_pci_bus = 0;
new g_pci_device = 0;
new g_pci_function = 0;
new g_device_id = 0;
new g_revision_id = 0;
new g_subsystem_vendor_id = 0;
new g_subsystem_device_id = 0;
new g_reg_bar = 0;
new g_reg_size = 0;
new g_vram_bar = 0;
new g_vram_size = 0;

/* Discovery */

// Select the AMD display adapter with the largest valid VRAM BAR.
find_gpu_and_probe() {
    new best_bus = -1, best_device = -1;
    new best_vram_bar = 0, best_vram_size = 0;
    new best_vendor_device = 0, best_class_revision = 0;
    new best_subsystem = 0;

    for (new bus = 0; bus <= 255; bus++) {
        for (new device = 0; device < 32; device++) {
            new vendor_device = 0;
            if (pci_config_read_dword(bus, device, 0, 0x00, vendor_device) != STATUS_SUCCESS)
                continue;
            if ((vendor_device & 0xFFFF) != AMD_VENDOR_ID)
                continue;

            new class_revision = 0;
            if (pci_config_read_dword(bus, device, 0, 0x08, class_revision) != STATUS_SUCCESS)
                continue;
            if (((class_revision >>> 24) & 0xFF) != 0x03)
                continue;
            if (((class_revision >>> 16) & 0xFF) != 0x00)
                continue;

            new bar0_low = 0, bar0_high = 0;
            if (pci_config_read_dword(bus, device, 0, 0x10, bar0_low) != STATUS_SUCCESS)
                continue;
            if ((bar0_low & 0x1) != 0)
                continue;
            if (((bar0_low >>> 1) & 0x3) != 0x2)
                continue;
            if (pci_config_read_dword(bus, device, 0, 0x14, bar0_high) != STATUS_SUCCESS)
                continue;

            new vram_bar = (bar0_high << 32) | (bar0_low & 0xFFFFFFF0);

            // Probe BAR0 and restore both halves before validation.
            new mask_low = 0, mask_high = 0;
            if (pci_config_write_dword(bus, device, 0, 0x10, 0xFFFFFFFF) != STATUS_SUCCESS)
                continue;
            if (pci_config_write_dword(bus, device, 0, 0x14, 0xFFFFFFFF) != STATUS_SUCCESS) {
                pci_config_write_dword(bus, device, 0, 0x10, bar0_low);
                continue;
            }
            new NTSTATUS:read_low_status = pci_config_read_dword(bus, device, 0, 0x10, mask_low);
            new NTSTATUS:read_high_status = pci_config_read_dword(bus, device, 0, 0x14, mask_high);
            pci_config_write_dword(bus, device, 0, 0x10, bar0_low);
            pci_config_write_dword(bus, device, 0, 0x14, bar0_high);
            if (read_low_status != STATUS_SUCCESS || read_high_status != STATUS_SUCCESS)
                continue;

            new mask = (mask_high << 32) | (mask_low & 0xFFFFFFF0);
            new vram_size = (~mask) + 1;
            if (vram_size <= 0 || vram_size > VRAM_SIZE_MAX)
                continue;

            if (vram_size > best_vram_size ||
                (vram_size == best_vram_size && bus > best_bus)) {
                new subsystem = 0;
                pci_config_read_dword(bus, device, 0, 0x2C, subsystem);

                best_vram_size = vram_size;
                best_vram_bar = vram_bar;
                best_bus = bus;
                best_device = device;
                best_vendor_device = vendor_device;
                best_class_revision = class_revision;
                best_subsystem = subsystem;
            }
        }
    }

    if (best_bus < 0)
        return;

    new bar5_low = 0;
    if (pci_config_read_dword(best_bus, best_device, 0, 0x24, bar5_low) != STATUS_SUCCESS)
        return;
    if ((bar5_low & 0x1) != 0)
        return;

    new bar5_type = (bar5_low >>> 1) & 0x3;
    if (bar5_type != 0x0 && bar5_type != 0x2)
        return;

    new bar5_high = 0;
    if (bar5_type == 0x2 &&
        pci_config_read_dword(best_bus, best_device, 0, 0x28, bar5_high) != STATUS_SUCCESS)
        return;
    new reg_bar = (bar5_high << 32) | (bar5_low & 0xFFFFFFF0);

    new bar5_mask = 0;
    if (pci_config_write_dword(best_bus, best_device, 0, 0x24, 0xFFFFFFFF) != STATUS_SUCCESS)
        return;
    new NTSTATUS:bar5_read_status =
        pci_config_read_dword(best_bus, best_device, 0, 0x24, bar5_mask);
    pci_config_write_dword(best_bus, best_device, 0, 0x24, bar5_low);
    if (bar5_read_status != STATUS_SUCCESS)
        return;

    bar5_mask = bar5_mask & 0xFFFFFFF0;
    new reg_size = 0;
    if (bar5_mask != 0)
        reg_size = ((~bar5_mask) & 0xFFFFFFFF) + 1;
    if (reg_size <= 0 || reg_size > REG_SIZE_MAX)
        reg_size = REG_SPAN;
    if (reg_size < SMN_DATA_OFFSET + 4)
        return;

    g_pci_bus = best_bus;
    g_pci_device = best_device;
    g_pci_function = 0;
    g_device_id = (best_vendor_device >>> 16) & 0xFFFF;
    g_revision_id = best_class_revision & 0xFF;
    g_subsystem_vendor_id = best_subsystem & 0xFFFF;
    g_subsystem_device_id = (best_subsystem >>> 16) & 0xFFFF;
    g_reg_bar = reg_bar;
    g_reg_size = reg_size;
    g_vram_bar = best_vram_bar;
    g_vram_size = best_vram_size;
    g_ready = 1;
}

/* Bounds and SMN access */

// Overflow-safe half-open range check.
bool:in_window(address, length, base, size) {
    if (size <= 0 || length <= 0)
        return false;
    if (address < base)
        return false;
    new offset = address - base;
    if (offset > size)
        return false;
    if (length > size - offset)
        return false;
    return true;
}

// Accept aligned addresses inside MP1 C2PMSG only.
bool:smn_allowed(smn_address) {
    if ((smn_address & 0x3) != 0)
        return false;
    return in_window(smn_address, 4, MP1_C2PMSG_BASE, MP1_C2PMSG_SPAN);
}

// Indirect SMN access through BAR5 PCIE_INDEX2/PCIE_DATA2.
NTSTATUS:smn_read(smn_address, &value) {
    new VA:virtual_address = io_space_map(g_reg_bar + SMN_INDEX_OFFSET, 8);
    if (virtual_address == NULL)
        return STATUS_INSUFFICIENT_RESOURCES;

    new NTSTATUS:status = virtual_write_dword(virtual_address, smn_address);
    if (status == STATUS_SUCCESS)
        status = virtual_read_dword(virtual_address + 4, value);
    io_space_unmap(virtual_address, 8);
    return status;
}

NTSTATUS:smn_write(smn_address, value) {
    new VA:virtual_address = io_space_map(g_reg_bar + SMN_INDEX_OFFSET, 8);
    if (virtual_address == NULL)
        return STATUS_INSUFFICIENT_RESOURCES;

    new NTSTATUS:status = virtual_write_dword(virtual_address, smn_address);
    if (status == STATUS_SUCCESS)
        status = virtual_write_dword(virtual_address + 4, value);
    io_space_unmap(virtual_address, 8);
    return status;
}

bool:is_rdna3_tool_device(device_id) {
    return ((device_id >= RDNA3_DEVICE_ID_MIN_0 && device_id <= RDNA3_DEVICE_ID_MAX_0) ||
            (device_id >= RDNA3_DEVICE_ID_MIN_1 && device_id <= RDNA3_DEVICE_ID_MAX_1));
}

bool:is_rdna4_tool_device(device_id) {
    return ((device_id >= RDNA4_DEVICE_ID_MIN_0 && device_id <= RDNA4_DEVICE_ID_MAX_0) ||
            (device_id >= RDNA4_DEVICE_ID_MIN_1 && device_id <= RDNA4_DEVICE_ID_MAX_1));
}

bool:is_rdna_tool_device(device_id) {
    return ((device_id >= NAVI21_DEVICE_ID_MIN && device_id <= NAVI21_DEVICE_ID_MAX) ||
            is_rdna3_tool_device(device_id) ||
            is_rdna4_tool_device(device_id));
}

// Map supported PM-table families; unknown versions fail closed.
rdna_tool_layout(version) {
    switch ((version >>> 16) & 0xFFFF) {
        case 0x0000: return 1;
        case 0x0027: return 2;
        case 0x0028: return 3;
        case 0x0029: return 4;
        case 0x0034: return 5;
        case 0x003A: return 6;
        case 0x004E: return 7;
        case 0x0066: return 8;
        case 0x0044: return 9;
        case 0x0055: return 10;
        case 0x0056: return 11;
    }
    return 0;
}

NTSTATUS:rdna_tool_response_status(response) {
    response = response & 0xFFFFFFFF;
    if (response == RDNA_TOOL_RESPONSE_OK)
        return STATUS_SUCCESS;
    if (response == RDNA_TOOL_RESPONSE_BUSY)
        return STATUS_DEVICE_BUSY;
    if (response == RDNA_TOOL_RSP_PREREQ)
        return STATUS_INVALID_DEVICE_STATE;
    if (response == RDNA_TOOL_RESPONSE_UNKNOWN)
        return STATUS_NOT_SUPPORTED;
    return STATUS_UNSUCCESSFUL;
}

NTSTATUS:rdna_tool_wait_response(VA:registers, &response) {
    response = 0;
    for (new attempt = 0; attempt < RDNA_TOOL_POLL_ATTEMPTS; attempt++) {
        new NTSTATUS:status = virtual_read_dword(
            registers + RDNA_TOOL_RESPONSE_OFFSET,
            response);
        if (status != STATUS_SUCCESS)
            return status;
        if ((response & 0xFFFFFFFF) != 0)
            return STATUS_SUCCESS;

        status = microsleep(RDNA_TOOL_POLL_DELAY_US);
        if (status != STATUS_SUCCESS)
            return status;
    }
    return STATUS_IO_TIMEOUT;
}

// Send one compile-time-selected private mailbox command.
NTSTATUS:rdna_tool_send(
    VA:registers,
    argument_offset,
    message,
    bool:has_argument,
    &argument) {
    new response = 0;
    new NTSTATUS:status = rdna_tool_wait_response(registers, response);
    if (status != STATUS_SUCCESS)
        return status;

    status = virtual_write_dword(registers + RDNA_TOOL_RESPONSE_OFFSET, 0);
    if (status != STATUS_SUCCESS)
        return status;

    if (has_argument) {
        status = virtual_write_dword(
            registers + argument_offset,
            argument & 0xFFFFFFFF);
        if (status != STATUS_SUCCESS)
            return status;
    }

    status = virtual_write_dword(
        registers + RDNA_TOOL_MESSAGE_OFFSET,
        message & 0xFFFFFFFF);
    if (status != STATUS_SUCCESS)
        return status;

    status = rdna_tool_wait_response(registers, response);
    if (status != STATUS_SUCCESS)
        return status;

    status = rdna_tool_response_status(response);
    if (status != STATUS_SUCCESS)
        return status;

    status = virtual_read_dword(registers + argument_offset, argument);
    if (status != STATUS_SUCCESS)
        return status;
    argument = argument & 0xFFFFFFFF;
    return STATUS_SUCCESS;
}

// Read the generation-specific framebuffer interval.
NTSTATUS:rdna_tool_framebuffer_bounds(VA:registers, &fb_base, &fb_top) {
    new base_offset = NAVI21_FB_BASE_OFFSET;
    new top_offset = NAVI21_FB_TOP_OFFSET;
    if (is_rdna3_tool_device(g_device_id) ||
        is_rdna4_tool_device(g_device_id) ||
        (g_device_id == 0x73BF && g_revision_id == 0xD5)) {
        base_offset = RDNA3_FB_BASE_OFFSET;
        top_offset = RDNA3_FB_TOP_OFFSET;
    }

    new base_value = 0, top_value = 0;
    new NTSTATUS:status = virtual_read_dword(registers + base_offset, base_value);
    if (status != STATUS_SUCCESS)
        return status;
    status = virtual_read_dword(registers + top_offset, top_value);
    if (status != STATUS_SUCCESS)
        return status;

    fb_base = (base_value & 0x00FFFFFF) << 24;
    fb_top = (top_value & 0x00FFFFFF) << 24;
    if (fb_top <= fb_base)
        return STATUS_INVALID_ADDRESS;
    return STATUS_SUCCESS;
}

// Query, refresh, validate, and copy the private table atomically.
NTSTATUS:read_rdna_tool_table(result[]) {
    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;
    if (!is_rdna_tool_device(g_device_id))
        return STATUS_NOT_SUPPORTED;
    if (!in_window(0, RDNA_TOOL_MMIO_MAP_SIZE, 0, g_reg_size))
        return STATUS_NOT_SUPPORTED;

    new VA:registers = io_space_map(g_reg_bar, RDNA_TOOL_MMIO_MAP_SIZE);
    if (registers == NULL)
        return STATUS_INSUFFICIENT_RESOURCES;

    new version = 0;
    new NTSTATUS:status = rdna_tool_send(
        registers,
        RDNA_TOOL_ARGUMENT_OFFSET,
        RDNA_TOOL_GET_VERSION,
        false,
        version);
    if (status != STATUS_SUCCESS) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return status;
    }

    new layout = rdna_tool_layout(version);
    if (layout == 0) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return STATUS_NOT_SUPPORTED;
    }
    new argument_offset = layout == 10
        ? RDNA_TOOL_ARG_V10_OFFSET
        : RDNA_TOOL_ARGUMENT_OFFSET;

    new address_high = 0;
    status = rdna_tool_send(
        registers,
        argument_offset,
        RDNA_TOOL_GET_ADDRESS_HIGH,
        false,
        address_high);
    if (status != STATUS_SUCCESS) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return status;
    }

    new address_low = 0;
    status = rdna_tool_send(
        registers,
        argument_offset,
        RDNA_TOOL_GET_ADDRESS_LOW,
        false,
        address_low);
    if (status != STATUS_SUCCESS) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return status;
    }

    new gpu_address =
        ((address_high & 0xFFFFFFFF) << 32) | (address_low & 0xFFFFFFFF);
    if ((gpu_address & 0x3) != 0) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return STATUS_INVALID_ADDRESS;
    }

    new fb_base = 0, fb_top = 0;
    status = rdna_tool_framebuffer_bounds(registers, fb_base, fb_top);
    if (status != STATUS_SUCCESS) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return status;
    }
    if (!in_window(
            gpu_address,
            RDNA_TOOL_TABLE_BYTES,
            fb_base,
            fb_top - fb_base)) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return STATUS_ACCESS_DENIED;
    }

    new vram_offset = gpu_address - fb_base;
    if (!in_window(vram_offset, RDNA_TOOL_TABLE_BYTES, 0, g_vram_size)) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return STATUS_ACCESS_DENIED;
    }
    new physical_address = g_vram_bar + vram_offset;
    if (!in_window(
            physical_address,
            RDNA_TOOL_TABLE_BYTES,
            g_vram_bar,
            g_vram_size)) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return STATUS_ACCESS_DENIED;
    }

    new VA:table = io_space_map(physical_address, RDNA_TOOL_TABLE_BYTES);
    if (table == NULL) {
        io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    new bool:valid_table = false;
    for (new attempt = 0; attempt < RDNA_TOOL_READ_ATTEMPTS; attempt++) {
        new refresh_argument = RDNA_TOOL_REFRESH_SELECTOR;
        status = rdna_tool_send(
            registers,
            argument_offset,
            RDNA_TOOL_REFRESH_TABLE,
            true,
            refresh_argument);
        if (status != STATUS_SUCCESS) {
            io_space_unmap(table, RDNA_TOOL_TABLE_BYTES);
            io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
            return status;
        }

        new first_qword = 0;
        new bool:all_same = true;
        for (new i = 0; i < RDNA_TOOL_TABLE_QWORDS; i++) {
            new data_low = 0, data_high = 0;
            status = virtual_read_dword(table + i * 8, data_low);
            if (status == STATUS_SUCCESS)
                status = virtual_read_dword(table + i * 8 + 4, data_high);
            if (status != STATUS_SUCCESS) {
                io_space_unmap(table, RDNA_TOOL_TABLE_BYTES);
                io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
                return status;
            }

            new data =
                (data_low & 0xFFFFFFFF) | ((data_high & 0xFFFFFFFF) << 32);
            result[RDNA_TOOL_METADATA_QWORDS + i] = data;
            if (i == 0)
                first_qword = data;
            else if (data != first_qword)
                all_same = false;
        }

        if (!all_same) {
            valid_table = true;
            break;
        }

        if (attempt + 1 < RDNA_TOOL_READ_ATTEMPTS) {
            status = microsleep(RDNA_TOOL_READ_RETRY_DELAY_US);
            if (status != STATUS_SUCCESS) {
                io_space_unmap(table, RDNA_TOOL_TABLE_BYTES);
                io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);
                return status;
            }
        }
    }
    io_space_unmap(table, RDNA_TOOL_TABLE_BYTES);
    io_space_unmap(registers, RDNA_TOOL_MMIO_MAP_SIZE);

    if (!valid_table)
        return STATUS_DATA_ERROR;

    result[0] = version & 0xFFFFFFFF;
    result[1] = gpu_address;
    result[2] = fb_base;
    result[3] = fb_top;
    return STATUS_SUCCESS;
}

// Preserve the Navi 21-only ABI entry point.
NTSTATUS:read_navi21_tool_table(result[]) {
    if (g_device_id < NAVI21_DEVICE_ID_MIN || g_device_id > NAVI21_DEVICE_ID_MAX)
        return STATUS_NOT_SUPPORTED;
    return read_rdna_tool_table(result);
}

NTSTATUS:read_navi21_svi(result[]) {
    new length = NAVI21_SVI_DWORDS * 4;
    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;
    if (g_device_id < NAVI21_DEVICE_ID_MIN || g_device_id > NAVI21_DEVICE_ID_MAX)
        return STATUS_NOT_SUPPORTED;
    if (!in_window(NAVI21_SVI_OFFSET, length, 0, g_reg_size))
        return STATUS_NOT_SUPPORTED;

    new VA:virtual_address = io_space_map(g_reg_bar + NAVI21_SVI_OFFSET, length);
    if (virtual_address == NULL)
        return STATUS_INSUFFICIENT_RESOURCES;

    for (new i = 0; i < NAVI21_SVI_DWORDS; i++) {
        new value = 0;
        new NTSTATUS:status = virtual_read_dword(virtual_address + i * 4, value);
        if (status != STATUS_SUCCESS) {
            io_space_unmap(virtual_address, length);
            return status;
        }
        result[i] = value & 0xFFFFFFFF;
    }

    io_space_unmap(virtual_address, length);
    return STATUS_SUCCESS;
}

// Read a pointer with a stable high half.
NTSTATUS:read_metrics_pointer_candidate(&address) {
    address = 0;

    new high_before = 0, high_after = 0, low = 0;
    new NTSTATUS:status = smn_read(MP1_C2PMSG_80, high_before);
    if (status != STATUS_SUCCESS)
        return status;
    status = smn_read(MP1_C2PMSG_81, low);
    if (status != STATUS_SUCCESS)
        return status;
    status = smn_read(MP1_C2PMSG_80, high_after);
    if (status != STATUS_SUCCESS)
        return status;
    if ((high_before & 0xFFFFFFFF) != (high_after & 0xFFFFFFFF))
        return STATUS_RETRY;

    address = ((high_before & 0xFFFFFFFF) << 32) | (low & 0xFFFFFFFF);
    return STATUS_SUCCESS;
}

// Require two equal snapshots, then validate and translate the pointer.
NTSTATUS:resolve_metrics_buffer(length, &gpu_address, &vram_offset, &physical_address) {
    gpu_address = 0;
    vram_offset = 0;
    physical_address = 0;

    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;
    if (length <= 0 || length > SMU14_METRICS_DWORDS * 4)
        return STATUS_INVALID_PARAMETER;

    new NTSTATUS:last_status = STATUS_RETRY;
    for (new attempt = 0; attempt < METRICS_ADDRESS_READ_ATTEMPTS; attempt++) {
        new first_address = 0, second_address = 0;
        new NTSTATUS:status = read_metrics_pointer_candidate(first_address);
        if (status != STATUS_SUCCESS && status != STATUS_RETRY)
            return status;

        if (status == STATUS_SUCCESS)
            status = read_metrics_pointer_candidate(second_address);
        if (status != STATUS_SUCCESS && status != STATUS_RETRY)
            return status;

        if (status == STATUS_SUCCESS && first_address == second_address) {
            if ((first_address & 0x3) != 0) {
                last_status = STATUS_INVALID_ADDRESS;
            } else if (first_address < GPU_VRAM_MC_BASE) {
                last_status = STATUS_DEVICE_NOT_READY;
            } else {
                new candidate_offset = first_address - GPU_VRAM_MC_BASE;
                new candidate_physical = g_vram_bar + candidate_offset;
                if (!in_window(candidate_offset, length, 0, g_vram_size) ||
                    !in_window(
                        candidate_physical,
                        length,
                        g_vram_bar,
                        g_vram_size)) {
                    last_status = STATUS_ACCESS_DENIED;
                } else {
                    gpu_address = first_address;
                    vram_offset = candidate_offset;
                    physical_address = candidate_physical;
                    return STATUS_SUCCESS;
                }
            }
        } else {
            last_status = STATUS_RETRY;
        }

        if (attempt + 1 < METRICS_ADDRESS_READ_ATTEMPTS) {
            status = microsleep(METRICS_ADDRESS_RETRY_DELAY_US);
            if (status != STATUS_SUCCESS)
                return status;
        }
    }

    return last_status;
}

// Resolve and copy a fixed-size public metrics table.
NTSTATUS:read_current_metrics(dword_count, result[]) {
    new gpu_address = 0, vram_offset = 0, physical_address = 0;
    new length = dword_count * 4;
    new NTSTATUS:status = resolve_metrics_buffer(
        length,
        gpu_address,
        vram_offset,
        physical_address);
    if (status != STATUS_SUCCESS)
        return status;

    new VA:virtual_address = io_space_map(physical_address, length);
    if (virtual_address == NULL)
        return STATUS_INSUFFICIENT_RESOURCES;

    for (new i = 0; i < dword_count; i++) {
        new value = 0;
        status = virtual_read_dword(virtual_address + i * 4, value);
        if (status != STATUS_SUCCESS) {
            io_space_unmap(virtual_address, length);
            return status;
        }
        result[i] = value & 0xFFFFFFFF;
    }

    io_space_unmap(virtual_address, length);
    return STATUS_SUCCESS;
}

/* Legacy IOCTLs */

// Read one allowlisted C2PMSG DWORD: in[0] address, out[0] value.
DEFINE_IOCTL_SIZED(ioctl_read_smn, 1, 1) {
    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;
    new smn_address = in[0];
    if (!smn_allowed(smn_address))
        return STATUS_ACCESS_DENIED;

    new value = 0;
    new NTSTATUS:status = smn_read(smn_address, value);
    out[0] = value & 0xFFFFFFFF;
    return status;
}

// Write one allowlisted C2PMSG DWORD: in[0] address, in[1] value.
DEFINE_IOCTL_SIZED(ioctl_write_smn, 2, 1) {
    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;
    new smn_address = in[0];
    if (!smn_allowed(smn_address)) {
        out[0] = _:STATUS_ACCESS_DENIED & 0xFFFFFFFF;
        return STATUS_ACCESS_DENIED;
    }

    new NTSTATUS:status = smn_write(smn_address, in[1] & 0xFFFFFFFF);
    out[0] = _:status & 0xFFFFFFFF;
    return status;
}

// Deprecated caller-addressed SMU14 read, bounded to the selected VRAM BAR.
DEFINE_IOCTL_SIZED(ioctl_read_metrics, 1, SMU14_METRICS_DWORDS) {
    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;
    new physical_address = in[0];
    new length = SMU14_METRICS_DWORDS * 4;
    if ((physical_address & 0x3) != 0)
        return STATUS_INVALID_PARAMETER;
    if (!in_window(physical_address, length, g_vram_bar, g_vram_size))
        return STATUS_ACCESS_DENIED;

    new VA:virtual_address = io_space_map(physical_address, length);
    if (virtual_address == NULL)
        return STATUS_INSUFFICIENT_RESOURCES;
    for (new i = 0; i < SMU14_METRICS_DWORDS; i++) {
        new value = 0;
        new NTSTATUS:status = virtual_read_dword(virtual_address + i * 4, value);
        if (status != STATUS_SUCCESS) {
            io_space_unmap(virtual_address, length);
            return status;
        }
        out[i] = value & 0xFFFFFFFF;
    }
    io_space_unmap(virtual_address, length);
    return STATUS_SUCCESS;
}

// Return readiness and selected register/VRAM BAR bounds.
DEFINE_IOCTL_SIZED(ioctl_get_bounds, 1, 5) {
    out[0] = g_ready;
    out[1] = g_reg_bar;
    out[2] = g_reg_size;
    out[3] = g_vram_bar;
    out[4] = g_vram_size;
    return STATUS_SUCCESS;
}

/* Monitoring IOCTLs */

// Return ABI, PCI identity, BARs, metrics address, and supported table sizes.
DEFINE_IOCTL_SIZED(ioctl_get_device_info, 0, 21) {
    if (!g_ready)
        return STATUS_DEVICE_NOT_READY;

    new gpu_address = 0, vram_offset = 0, physical_address = 0;
    new NTSTATUS:address_status = resolve_metrics_buffer(
        SMU14_METRICS_DWORDS * 4,
        gpu_address,
        vram_offset,
        physical_address);
    if (address_status != STATUS_SUCCESS) {
        gpu_address = 0;
        vram_offset = 0;
        physical_address = 0;
    }

    out[0] = MODULE_ABI_VERSION;
    out[1] = g_pci_bus;
    out[2] = g_pci_device;
    out[3] = g_pci_function;
    out[4] = g_device_id;
    out[5] = g_revision_id;
    out[6] = g_subsystem_vendor_id;
    out[7] = g_subsystem_device_id;
    out[8] = g_reg_bar;
    out[9] = g_reg_size;
    out[10] = g_vram_bar;
    out[11] = g_vram_size;
    out[12] = gpu_address;
    out[13] = vram_offset;
    out[14] = physical_address;
    out[15] = SMU11_METRICS_DWORDS;
    out[16] = SMU13_0_0_METRICS_DWORDS;
    out[17] = SMU13_0_7_METRICS_DWORDS;
    out[18] = SMU14_METRICS_DWORDS;
    out[19] = NAVI21_SVI_DWORDS;
    out[20] = RDNA_TOOL_TABLE_QWORDS;
    return STATUS_SUCCESS;
}

// Return GPU, VRAM-offset, and physical metrics addresses.
DEFINE_IOCTL_SIZED(ioctl_get_metrics_address, 0, 3) {
    new NTSTATUS:status = resolve_metrics_buffer(
        SMU14_METRICS_DWORDS * 4,
        out[0],
        out[1],
        out[2]);
    return status;
}

// Read four fixed Navi 21 SVI telemetry DWORDs without mailbox or I2C access.
DEFINE_IOCTL_SIZED(ioctl_read_navi21_svi, 0, NAVI21_SVI_DWORDS) {
    return read_navi21_svi(out);
}

// Read the bounded Navi 21 private table with metadata.
DEFINE_IOCTL_SIZED(ioctl_read_navi21_tool_table, 0, RDNA_TOOL_OUTPUT_QWORDS) {
    return read_navi21_tool_table(out);
}

// Read the bounded RDNA private table with metadata.
DEFINE_IOCTL_SIZED(ioctl_read_rdna_tool_table, 0, RDNA_TOOL_OUTPUT_QWORDS) {
    return read_rdna_tool_table(out);
}

// Read the current SMU11 metrics table.
DEFINE_IOCTL_SIZED(ioctl_read_metrics_rdna2, 0, SMU11_METRICS_DWORDS) {
    return read_current_metrics(SMU11_METRICS_DWORDS, out);
}

// Read the current SMU13.0.0-layout metrics table.
DEFINE_IOCTL_SIZED(ioctl_read_metrics_rdna3_0, 0, SMU13_0_0_METRICS_DWORDS) {
    return read_current_metrics(SMU13_0_0_METRICS_DWORDS, out);
}

// Read the current SMU13.0.7 metrics table.
DEFINE_IOCTL_SIZED(ioctl_read_metrics_rdna3_7, 0, SMU13_0_7_METRICS_DWORDS) {
    return read_current_metrics(SMU13_0_7_METRICS_DWORDS, out);
}

// Read the current SMU14 metrics table.
DEFINE_IOCTL_SIZED(ioctl_read_metrics_rdna4, 0, SMU14_METRICS_DWORDS) {
    return read_current_metrics(SMU14_METRICS_DWORDS, out);
}

/* Lifecycle */

NTSTATUS:main() {
    if (get_arch() != ARCH_X64)
        return STATUS_NOT_SUPPORTED;

    g_ready = 0;
    g_pci_bus = 0;
    g_pci_device = 0;
    g_pci_function = 0;
    g_device_id = 0;
    g_revision_id = 0;
    g_subsystem_vendor_id = 0;
    g_subsystem_device_id = 0;
    g_reg_bar = 0;
    g_reg_size = 0;
    g_vram_bar = 0;
    g_vram_size = 0;
    find_gpu_and_probe();

    if (!g_ready)
        return STATUS_NOT_SUPPORTED;
    return STATUS_SUCCESS;
}

public NTSTATUS:unload() {
    return STATUS_SUCCESS;
}
