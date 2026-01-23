# Copyright (C) 2024 Intel Corporation
# SPDX-License-Identifier: BSD-3-Clause

import unittest
import sys
from pathlib import Path

# Add create_perf_json.py directory to the path before importing.
_script_dir = Path(__file__).resolve().parent
sys.path.append(str(_script_dir.parent))

from create_perf_json import Model

class TestModel(unittest.TestCase):

    def test_extract_pebs_formula(self):
        """Test formulas which use $PEBS but do not include min() or max()."""
        tests = [
            (
                'EVENT.A*$PEBS',
                'EVENT.A * EVENT.A:R',
            ),
            (
                'EVENT.A + cpu_core@EVENT.B@*$PEBS',
                'EVENT.A + cpu_core@EVENT.B@ * cpu_core@EVENT.B@R',
            ),
        ]

        for input, expected_result in tests:
            with self.subTest(input=input, expected_result=expected_result):
                self.assertEqual(expected_result, Model.extract_pebs_formula(input))

    def test_extract_pebs_formula_with_min_max(self):
        """Test formulas which use $PEBS and also min() or max()."""
        tests = [
            (
                'EVENT.A*min( $PEBS, 4) / EVENT.B',
                'EVENT.A * min(EVENT.A:R, 4) / EVENT.B',
            ),
            (
                'EVENT.A*min($PEBS, 2) / (1 + EVENT.B*max($PEBS, 8))',
                'EVENT.A * min(EVENT.A:R, 2) / (1 + EVENT.B * max(EVENT.B:R, 8))',
            ),
            (
                'EVENT.A*min($PEBS, 9 * test_info) * (1 + (cpu_core@EVENT.B@ / cpu_core@EVENT.C@) / 2) / test_info_2',
                'EVENT.A * min(EVENT.A:R, 9 * test_info) * (1 + cpu_core@EVENT.B@ / cpu_core@EVENT.C@ / 2) / test_info_2',
            ),
            (
                '(cpu_core@EVENT.A@*min($PEBS, 24 * test_info) + cpu_core@EVENT.B@*min($PEBS, 24 - test_info) * (1 - (cpu_core@EVENT.C@ / (cpu_core@EVENT.D@ + cpu_core@EVENT.E@)))) * 5',
                '(cpu_core@EVENT.A@ * min(cpu_core@EVENT.A@R, 24 * test_info) + cpu_core@EVENT.B@ * min(cpu_core@EVENT.B@R, 24 - test_info) * (1 - cpu_core@EVENT.C@ / (cpu_core@EVENT.D@ + cpu_core@EVENT.E@))) * 5',
            )
        ]

        for input, expected_result in tests:
            with self.subTest(input=input, expected_result=expected_result):
                self.assertEqual(expected_result, Model.extract_pebs_formula(input))
