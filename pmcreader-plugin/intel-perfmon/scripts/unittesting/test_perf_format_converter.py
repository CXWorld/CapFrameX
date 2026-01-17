# Copyright (C) 2021 Intel Corporation
# SPDX-License-Identifier: BSD-3-Clause


import os
import sys
import unittest

unittest_dir = os.path.dirname(__file__)
format_converter_dir = os.path.join(unittest_dir, "..")
sys.path.append(format_converter_dir)
from perf_format_converter import PerfFormatConverter


class Testing(unittest.TestCase):

    def test_init(self):
        perf_format_converter = PerfFormatConverter(None)

        # Checks that format converter initializes
        self.assertIsNotNone(perf_format_converter)

    def test_deserialize(self):
        current_dir = os.path.dirname(__file__)
        test_input_file = current_dir + "/test_inputs/test_input_1.json"
        test_input_fp = open(test_input_file, "r")

        perf_format_converter = PerfFormatConverter(test_input_fp)

        perf_format_converter.deserialize_input()

        test_input_fp.close()

        # Checks that the deserializer got 1 metric
        self.assertEqual(len(perf_format_converter.input_data), 1)

        # Checks that the metric has all fields
        self.assertEqual(len(perf_format_converter.input_data[0]), 11)

        # Checks one field for correct data
        self.assertEqual(perf_format_converter.input_data[0]["Name"],
                         "test_metric_1")


if __name__ == "__main__":
    unittest.main()
