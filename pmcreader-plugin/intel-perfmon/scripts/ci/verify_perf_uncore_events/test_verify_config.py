# Copyright (C) 2025 Intel Corporation
# SPDX-License-Identifier: BSD-3-Clause

import unittest
from pathlib import Path

from verify_perf_uncore_events import *

_script_dir = Path(__file__).resolve().parent
_test_data_dir = Path(_script_dir, 'test_data')
_schema_path = Path(_script_dir, 'perf_uncore_csv_schema.json')


class TestVerifyHeader(unittest.TestCase):

    def setUp(self):
        # Comment to see logs.
        logging.getLogger('verify_perf_uncore_events').setLevel(logging.CRITICAL)

    def test_correct_header(self):
        csv_path = Path(_test_data_dir, '00_basic.csv')
        verify_header(csv_path, _schema_path)

    def test_missing_header(self):
        csv_path = Path(_test_data_dir, '01_missing_header.csv')

        with self.assertRaises(ValueError) as assertion_context:
            verify_header(csv_path, _schema_path)
        self.assertIn('CSV header does not match', str(assertion_context.exception))


class TestVerifyConfigSchema(unittest.TestCase):

    def setUp(self):
        # Comment to see logs.
        logging.getLogger('verify_perf_uncore_events').setLevel(logging.CRITICAL)

    def test_valid_config(self):
        csv_path = Path(_test_data_dir, '00_basic.csv')
        verify_config_schema(csv_path, _schema_path)

    def test_missing_column(self):
        csv_path = Path(_test_data_dir, '02_missing_column.csv')

        with self.assertRaises(ValueError) as assertion_context:
            verify_config_schema(csv_path, _schema_path)
        self.assertIn('CSV schema validation failed', str(assertion_context.exception))
