# Copyright (C) 2024 Intel Corporation
# SPDX-License-Identifier: BSD-3-Clause

import unittest
import jsonschema
from pathlib import Path

from verify_mapfile import *

_script_dir = Path(__file__).resolve().parent
_test_data_dir = Path(_script_dir, 'test_data')


class TestVerifyMapfileSchema(unittest.TestCase):

    def test_missing_mapfile(self):
        perfmon_repo_dir = Path(_test_data_dir, 'bad_folder_path')

        with self.assertRaises(FileNotFoundError) as assertion_context:
            load_mapfile(perfmon_repo_dir)
        self.assertRegex(str(assertion_context.exception), r'Could not locate.*mapfile')

    def test_missing_mapfile_column(self):
        perfmon_repo_dir = Path(_test_data_dir, '00_missing_mapfile_column')

        with self.assertRaises(jsonschema.exceptions.ValidationError) as assertion_context:
            verify_mapfile_schema(perfmon_repo_dir)
        self.assertIn('Failed validating', str(assertion_context.exception))

    def test_extra_mapfile_column(self):
        perfmon_repo_dir = Path(_test_data_dir, '06_extra_mapfile_column')

        with self.assertRaises(jsonschema.exceptions.ValidationError) as assertion_context:
            verify_mapfile_schema(perfmon_repo_dir)
        self.assertIn('Additional properties are not allowed', str(assertion_context.exception))


class TestVerifyMapfilePaths(unittest.TestCase):

    def test_missing_event_file(self):
        perfmon_repo_dir = Path(_test_data_dir, '01_missing_event_file')

        with self.assertRaises(FileNotFoundError) as assertion_context:
            verify_mapfile_paths(perfmon_repo_dir)
        self.assertIn('sapphirerapids_uncore_experimental.json', str(assertion_context.exception))
        self.assertIn('does not exist', str(assertion_context.exception))

    def test_correct_files_referenced(self):
        perfmon_repo_dir = Path(_test_data_dir, '09_correct_mapfile')
        verify_mapfile_paths(perfmon_repo_dir)


class TestVerifyEventFileVersions(unittest.TestCase):

    def test_mismatched_versions(self):
        perfmon_repo_dir = Path(_test_data_dir, '02_mismatched_versions')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_event_file_versions(perfmon_repo_dir)
        self.assertRegex(str(assertion_context.exception), r'Version \d.\d\d on mapfile line \d')

    def test_bad_mapfile_version(self):
        perfmon_repo_dir = Path(_test_data_dir, '10_bad_mapfile_version')

        with self.assertRaises(RuntimeError) as assertion_context:
            # Comparing event file versions first extracts versions from the mapfile column.
            verify_event_file_versions(perfmon_repo_dir)

        self.assertIn('Failed to extract version', str(assertion_context.exception))

    def test_correct_versions(self):
        perfmon_repo_dir = Path(_test_data_dir, '09_correct_mapfile')
        verify_event_file_versions(perfmon_repo_dir)


class TestVerifyEventTypeMatchesFile(unittest.TestCase):

    def test_correct_event_types(self):
        perfmon_repo_dir = Path(_test_data_dir, '03_correct_event_types')
        # Should not raise an exception
        verify_event_type_matches_file(perfmon_repo_dir)

    def test_incorrect_file_type(self):
        perfmon_repo_dir = Path(_test_data_dir, '11_mismatched_file_type')
        with self.assertRaises(RuntimeError) as assertion_context:
            verify_event_type_matches_file(perfmon_repo_dir)

        self.assertRegex(str(assertion_context.exception), r'expected.*uncore.*found.*core')

    def test_unexpected_file_path(self):
        perfmon_repo_dir = Path(_test_data_dir, '12_unexpected_file_path')
        with self.assertRaises(RuntimeError) as assertion_context:
            verify_event_type_matches_file(perfmon_repo_dir)

        self.assertIn('does not match type checks', str(assertion_context.exception))


class TestVerifyFamilyModelMapsToEventFiles(unittest.TestCase):

    def test_platform_referencing_multiple_event_directories(self):
        perfmon_repo_dir = Path(_test_data_dir, '04_platform_referencing_multiple_event_dirs')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_family_model_maps_to_event_files(perfmon_repo_dir)
        self.assertIn('references multiple event directories', str(assertion_context.exception))

    def test_mapfile_missing_event_file_reference(self):
        perfmon_repo_dir = Path(_test_data_dir, '05_mapfile_missing_event_file_reference')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_family_model_maps_to_event_files(perfmon_repo_dir)
        self.assertRegex(str(assertion_context.exception), r'Event file .* is not referenced')


class TestVerifyFamilyModelMapsToMetricFiles(unittest.TestCase):

    def test_mapfile_missing_metric_file_reference(self):
        perfmon_repo_dir = Path(_test_data_dir, '14_mapfile_missing_latency_file_reference')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_family_model_maps_to_metric_files(perfmon_repo_dir)
        self.assertRegex(str(assertion_context.exception), r'Metric file .* is not referenced')

    def test_platform_referencing_multiple_metric_directories(self):
        perfmon_repo_dir = Path(_test_data_dir, '15_platform_referencing_multiple_metric_dirs')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_family_model_maps_to_metric_files(perfmon_repo_dir)
        self.assertIn('references multiple metric directories', str(assertion_context.exception))


class TestVerifyMapfileDuplicateTypes(unittest.TestCase):

    def test_duplicate_event_file_type(self):
        perfmon_repo_dir = Path(_test_data_dir, '07_duplicate_event_file_type')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_mapfile_duplicate_types(perfmon_repo_dir)
        self.assertIn('duplicate entry for EventType', str(assertion_context.exception))

    def test_duplicate_metric_entries(self):
        perfmon_repo_dir = Path(_test_data_dir, '13_duplicate_metric_entries')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_mapfile_duplicate_types(perfmon_repo_dir)
        self.assertIn('duplicate entry for EventType', str(assertion_context.exception))

    def test_no_duplicates(self):
        perfmon_repo_dir = Path(_test_data_dir, '03_correct_event_types')
        # Should not raise an exception
        verify_mapfile_duplicate_types(perfmon_repo_dir)


class TestVerifyMapfileModelUniqueVersions(unittest.TestCase):

    def test_verify_mismatched_event_versions(self):
        perfmon_repo_dir = Path(_test_data_dir, '08_mismatched_event_file_versions')

        with self.assertRaises(RuntimeError) as assertion_context:
            verify_mapfile_model_event_versions(perfmon_repo_dir)
        self.assertIn('multiple event file versions', str(assertion_context.exception))

    def test_ok_event_versions(self):
        perfmon_repo_dir = Path(_test_data_dir, '03_correct_event_types')
        # Should not raise an exception
        verify_mapfile_model_event_versions(perfmon_repo_dir)
