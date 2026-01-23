#!/usr/bin/env python3
# Copyright (C) 2024 Intel Corporation
# SPDX-License-Identifier: BSD-3-Clause

import argparse
import csv
import json
import logging
import re
from jsonschema import validate
from pathlib import Path

logger = logging.getLogger(__name__)


def load_csv(filepath: Path, delimiter: str = ','):
    """Load CSV as list(dict) where each dict corresponds to a CSV row, keyed by the header."""
    with Path(filepath).open() as fi:
        reader = csv.DictReader(fi, delimiter=delimiter)
        return [x for x in reader]


def csv_to_jsonstr(filepath: Path, delimiter: str = ','):
    """Convert CSV contents into a JSON str."""
    csv_dict_list = load_csv(filepath, delimiter=delimiter)
    return json.dumps(csv_dict_list)


def load_schema(schemapath: Path):
    """Basic helper to load a JSON schema."""
    with Path(schemapath).open() as f:
        return json.load(f)


def load_mapfile(perfmon_repo_path: Path) -> list[dict]:
    """Load mapfile.csv."""
    mapfile_path = Path(perfmon_repo_path, 'mapfile.csv')

    if not mapfile_path.is_file():
        raise FileNotFoundError(f'Could not locate {mapfile_path}.')

    return load_csv(mapfile_path)


def get_unique_mapfile_models(mapfile_data) -> list[str]:
    """Find the unique set of Family-model entries."""
    models = [row['Family-model'] for row in mapfile_data]
    return sorted(set(models))


def validate_csv(filepath: Path, schemapath: Path, delimiter: str = ','):
    """Validate the JSON conversion of the csv [filepath] by the json schema [schemapath]."""
    json_s = csv_to_jsonstr(filepath, delimiter=delimiter)
    json_data = json.loads(json_s)
    schema = load_schema(schemapath)

    validate(json_data, schema)


def verify_mapfile_schema(perfmon_repo_path: Path):
    """Verify all mapfile rows match the expected schema definition."""
    mapfile = Path(perfmon_repo_path, 'mapfile.csv')
    # Use a path relative to verify_mapfile.py to simplify unittests. Otherwise,
    # each unittest directory would need to create or link to the schema.
    schema = Path(Path(__file__).resolve().parent, 'mapfile_schema.json')

    logger.info('Checking mapfile.csv schema against %s.', schema.name)
    validate_csv(mapfile, schema)


def verify_mapfile_paths(perfmon_repo_path: Path):
    """Verify files in the Filename column exist."""
    logger.info('Checking mapfile.csv event file paths.')

    for line_number, row in enumerate(load_mapfile(perfmon_repo_path), 2):
        # Trim leading slash from filename and build full path
        event_path = Path(perfmon_repo_path, row['Filename'][1:])

        if not event_path.exists():
            msg = f"Event file {row['Filename']} on line {line_number} does not exist."
            raise FileNotFoundError(msg)


def verify_event_file_versions(perfmon_repo_path: Path):
    """Verify mapfile Version column matches event file Header."""
    version_re = re.compile(r'^[vV](\d{1,2}|\d\.\d{1,2})$')

    logger.info('Checking mapfile.csv version matches event file version.')

    for line_number, row in enumerate(load_mapfile(perfmon_repo_path), 2):
        # TODO(ebaker): Remove if version information is added to retire latency.
        if row['Filename'].endswith('retire_latency.json'):
            continue

        # Trim V from Version column
        mapfile_version = version_re.match(row['Version'])
        if not mapfile_version:
            msg = (f"Failed to extract version from '{row['Version']}' on mapfile.csv line"
                   f" {line_number}. Expected pattern '{version_re.pattern}'.")
            raise RuntimeError(msg)
        mapfile_version = mapfile_version[1]

        # Load JSON version
        event_file_path = Path(perfmon_repo_path, row['Filename'][1:])
        with open(event_file_path, 'r') as f:
            json_header = json.load(f)['Header']

        if mapfile_version != json_header['Version']:
            msg = (f'Version {mapfile_version} on mapfile line {line_number} does not'
                   f' match {event_file_path.name} version {json_header["Version"]}.\n'
                   f'mapfile.csv row {json.dumps(row, indent=2)}\n'
                   f'JSON header {json.dumps(json_header, indent=2)}')
            raise RuntimeError(msg)


def verify_event_type_matches_file(perfmon_repo_path: Path):
    """
    Verify EventType column matches filename

    Examples:
        WestmereEP-SP_core.json -> core
        sapphirerapids_core.json -> core
        sapphirerapids_uncore.json -> uncore
        skylake_fp_arith_inst.json -> fp_arith_inst
        alderlake_gracemont_core.json -> hybridcore (except for ADL-N)
        sierraforest_metrics.json -> metrics
        alderlake_metrics_goldencove_core.json -> metrics
        graniterapids_retire_latency.json -> retire latency
    """
    # Certain model IDs have unique comparisons. As an example, ADL-N is not listed in the
    # mapfile as a hybridcore. Use the default otherwise.
    event_type_checks = {
        'default': [(re.compile(r'.*/[a-zA-Z-]*_core\.json'), 'core'),
                    (re.compile(r'.*/[a-z]*_[a-z]*_core\.json'), 'hybridcore'),
                    (re.compile(r'.*_uncore\.json'), 'uncore'),
                    (re.compile(r'.*_uncore_experimental\.json'), 'uncore experimental'),
                    (re.compile(r'.*_matrix\.json'), 'offcore'),
                    (re.compile(r'.*_metrics(_.*_core)?\.json'), 'metrics'),
                    (re.compile(r'.*_retire_latency\.json'), 'retire latency'),
                    (re.compile(r'.*_fp_arith_inst\.json'), 'fp_arith_inst')],
        'GenuineIntel-6-BE': [(re.compile(r'.*/[a-z]*_[a-z]*_core\.json'), 'core'),
                              (re.compile(r'.*uncore\.json'), 'uncore'),
                              (re.compile(r'.*uncore_experimental\.json'), 'uncore experimental')]
    }

    logger.info('Checking mapfile.csv EventType matches expected filename patterns.')

    for line_number, row in enumerate(load_mapfile(perfmon_repo_path), 2):
        checks = event_type_checks['default']
        if row['Family-model'] in event_type_checks:
            checks = event_type_checks[row['Family-model']]

        file_pattern_matched = False  # One of the file patterns matched
        for file_pattern, expected_event_type in checks:
            if file_pattern.match(row['Filename']):
                file_pattern_matched = True

                if row['EventType'] != expected_event_type:
                    msg = (f"Mapfile line {line_number} expected EventType '{expected_event_type}'"
                           f" for {row['Filename']} but found '{row['EventType']}'.")
                    raise RuntimeError(msg)

        if not file_pattern_matched:
            raise RuntimeError(f"Filename {row['Filename']} does not match type checks. {checks}.")


def verify_family_model_maps_to_event_files(perfmon_repo_path: Path):
    """
    For each Family-Model verify that all event files are mentioned in mapfile.csv

    Failing example:
        mapfile.csv:
            <snip>
            GenuineIntel-6-CF,V1.13,/SPR/events/sapphirerapids_core.json,core,,,
            GenuineIntel-6-6A,V1.20,/ICX/events/icelakex_core.json,core,,,
            GenuineIntel-6-6A,V1.20,/ICX/events/icelakex_uncore.json,uncore,,,
            GenuineIntel-6-6A,V1.20,/ICX/events/icelakex_uncore_experimental.json,uncore experimental,,,
            GenuineIntel-6-6C,V1.20,/ICX/events/icelakex_core.json,core,,,
            GenuineIntel-6-6C,V1.20,/ICX/events/icelakex_uncore.json,uncore,,,
            GenuineIntel-6-96,V1.04,/EHL/events/elkhartlake_core.json,core,,,
            <snip>
        Files in ICX/events/:
            icelakex_core.json
            icelakex_uncore.json
            icelakex_uncore_experimental.json
        Failure:
            Family-Model 0x6C is missing icelakex_uncore_experimental.json.
    """
    # Known exceptions. Do not flag these combinations of models and files as issues.
    exceptions = {
        # ADL-N is an E-Core only product. Refer to Table 1 in
        # https://cdrdv2.intel.com/v1/dl/getContent/759603
        'GenuineIntel-6-BE': ['alderlake_goldencove_core.json'],
        # ARL model 0xC6 does not reference CMT event content.
        'GenuineIntel-6-C6': ['arrowlake_crestmont_core.json'],
    }

    logger.info('Checking mapfile.csv for missing event files.')

    mapfile_data = load_mapfile(perfmon_repo_path)

    # With the set of unique models compare mapfile.csv rows to actual event files.
    for model in get_unique_mapfile_models(mapfile_data):
        # Filter for any rows with a matching model.
        mapfile_model_rows = [x for x in mapfile_data if x['Family-model'] == model]
        # Then extract file paths that are mentioned. The Filename column includes a leading slash
        # which is trimmed before combining with the repository path.
        mapfile_event_files = [
            Path(perfmon_repo_path, x['Filename'][1:]) for x in mapfile_model_rows
        ]

        # Platforms should only reference event files in one directory. Platform ABC should only
        # use event files in ABC/events. ABC using events in ABC/events and XYZ/events is likely
        # a typo or a mistake.
        mapfile_event_dir = set([x.parent for x in mapfile_event_files])
        mapfile_event_dir = [x for x in mapfile_event_dir if x.name != 'metrics']
        if len(mapfile_event_dir) != 1:
            msg = (f'Family-model {model} references multiple event directories.\n'
                   f'{mapfile_event_dir}')
            raise RuntimeError(msg)
        mapfile_event_dir = mapfile_event_dir.pop()

        # Verify that all event files are mentioned in the mapfile for this specific model.
        event_files = sorted(mapfile_event_dir.glob('*.json'))
        for event_file in event_files:
            if event_file not in mapfile_event_files:
                # First check known exceptions before flagging as an actual issue.
                if model in exceptions and event_file.name in exceptions[model]:
                    logger.warning('\tmapfile.csv %s missing row for %s. Known exception, OK.',
                                   model, event_file.name)
                    continue

                event_files_msg = '\n  '.join([x.name for x in event_files])
                msg = (f'Event file {event_file.name} is not referenced by {model}.\n'
                       f'mapfile.csv\n{json.dumps(mapfile_model_rows, indent=2)}\n'
                       f'Files in {mapfile_event_dir}\n  {event_files_msg}')
                raise RuntimeError(msg)


def verify_family_model_maps_to_metric_files(perfmon_repo_path: Path):
    """
    For each Family-Model verify that all metric files are mentioned in mapfile.csv

    Failing example:
        mapfile.csv:
            <snip>
            GenuineIntel-6-AD,V1.10,/GNR/events/graniterapids_core.json,core,,,
            GenuineIntel-6-AD,V1.10,/GNR/events/graniterapids_uncore.json,uncore,,,
            GenuineIntel-6-AD,V1.10,/GNR/events/graniterapids_uncore_experimental.json,uncore experimental,,,
            GenuineIntel-6-AD,V1.02,/GNR/metrics/graniterapids_metrics.json,metrics,,,
            GenuineIntel-6-AD,V1.08,/GNR/metrics/graniterapids_retire_latency.json,retire latency,,,
            GenuineIntel-6-AE,V1.10,/GNR/events/graniterapids_core.json,core,,,
            GenuineIntel-6-AE,V1.10,/GNR/events/graniterapids_uncore.json,uncore,,,
            GenuineIntel-6-AE,V1.10,/GNR/events/graniterapids_uncore_experimental.json,uncore experimental,,,
            GenuineIntel-6-AE,V1.02,/GNR/metrics/graniterapids_metrics.json,metrics,,,
            <snip>
        Files in GNR/metrics/:
            graniterapids_metrics.json
            graniterapids_retire_latency.json
        Failure:
            Family-Model 0xAE is missing graniterapids_retire_latency.json.
    """
    # Known exceptions. Do not flag these combinations of models and files as issues.
    exceptions = {
        # SPR-HBM metrics are not included in mapfile.csv
        'GenuineIntel-6-8F': ['sapphirerapidshbm_metrics.json'],
        # ADL-N is an E-Core only product. Refer to Table 1 in
        # https://cdrdv2.intel.com/v1/dl/getContent/759603
        'GenuineIntel-6-BE': ['alderlake_metrics_goldencove_core.json'],
    }

    logger.info('Checking mapfile.csv for missing metric files.')

    mapfile_data = load_mapfile(perfmon_repo_path)

    # With the set of unique models compare mapfile.csv rows to actual event files.
    for model in get_unique_mapfile_models(mapfile_data):
        # Filter for any rows with a matching model.
        mapfile_model_rows = [x for x in mapfile_data if x['Family-model'] == model]
        # Then extract file paths that are mentioned. The Filename column includes a leading slash
        # which is trimmed before combining with the repository path.
        mapfile_model_files = [
            Path(perfmon_repo_path, x['Filename'][1:]) for x in mapfile_model_rows
        ]

        # Platforms should only reference metric files in one directory. Platform ABC should only
        # use metrics files in ABC/metrics. ABC using events in ABC/metrics and XYZ/metrics is likely
        # a typo or a mistake.
        mapfile_metrics_dir = set([x.parent for x in mapfile_model_files])
        mapfile_metrics_dir = [x for x in mapfile_metrics_dir if x.name == 'metrics']
        if len(mapfile_metrics_dir) > 1:
            msg = (f'Family-model {model} references multiple metric directories.\n'
                   f'{mapfile_metrics_dir}')
            raise RuntimeError(msg)

        # Verify that all metric files are mentioned in the mapfile for this specific model.
        model_metrics_dir = Path(mapfile_model_files[0].parents[1], 'metrics')
        metric_files = sorted(model_metrics_dir.glob('*.json'))
        for metric_file in metric_files:
            logging.error(f'Checking {metric_file.name} for {model}')
            if metric_file not in mapfile_model_files:
                # First check known exceptions before flagging as an actual issue.
                if model in exceptions and metric_file.name in exceptions[model]:
                    logger.warning('\tmapfile.csv %s missing row for %s. Known exception, OK.',
                                   model, metric_file.name)
                    continue

                metric_files_msg = '\n  '.join([x.name for x in metric_files])
                msg = (f'Metric file {metric_file.name} is not referenced by {model}.\n'
                       f'mapfile.csv\n{json.dumps(mapfile_model_rows, indent=2)}\n'
                       f'Files in {model_metrics_dir}\n  {metric_files_msg}')
                raise RuntimeError(msg)


def verify_mapfile_duplicate_types(perfmon_repo_path: Path):
    """
    Per Family-Model verify that there is only one instance of each event file type.

    Failing example:
        mapfile.csv:
            GenuineIntel-6-A7,V1.18,/ICL/events/icelake_core.json,core,,,
            GenuineIntel-6-A7,V1.18,/ICL/events/icelake_uncore.json,uncore,,,
            GenuineIntel-6-A7,V1.18,/ICL/events/icelake_uncore_experimental.json,uncore experimental,,,
            GenuineIntel-6-A7,V1.18,/ICL/events/icelake_core.json,core,,,
            GenuineIntel-6-86,V1.21,/SNR/events/snowridgex_core.json,core,,,
            GenuineIntel-6-86,V1.21,/SNR/events/snowridgex_uncore.json,uncore,,,
            GenuineIntel-6-86,V1.21,/SNR/events/snowridgex_uncore_experimental.json,uncore experimental,,,
        Failure:
            0xA7 has two entries for 'core'.
    """
    logger.info('Checking mapfile.csv for EventType duplicates.')

    mapfile_data = load_mapfile(perfmon_repo_path)

    for model in get_unique_mapfile_models(mapfile_data):
        # Filter for any rows with a matching model.
        mapfile_rows = [x for x in mapfile_data if x['Family-model'] == model]

        # Create a dict key based on EventType. For hybrid platforms also include Core Type
        # and Native Model ID otherwise there would be overlapping entries for 'hybridcore'.
        unique_entry_check = {}
        for row in mapfile_rows:
            event_type = row['EventType']
            core_type = row['Core Type']
            native_model_id = row['Native Model ID']

            key = f'{event_type}'
            if event_type == 'hybridcore':
                key = f'{event_type}_{core_type}_{native_model_id}'

            # This row already existed if the key is present.
            if key in unique_entry_check:
                msg = (f'Family-model {model} includes duplicate entry for EventType {event_type}. '
                       f'Mapfile row {row}.')
                raise RuntimeError(msg)

            unique_entry_check[key] = 1


def verify_mapfile_model_event_versions(perfmon_repo_path: Path):
    """
    Per Family-Model verify only one version is used across all event entries.

    Failing example:
        mapfile.csv:
            GenuineIntel-6-8F,V1.14,/SPR/events/sapphirerapids_core.json,core,,,
            GenuineIntel-6-8F,V1.14,/SPR/events/sapphirerapids_uncore.json,uncore,,,
            GenuineIntel-6-8F,V1.14,/SPR/events/sapphirerapids_uncore_experimental.json,uncore experimental,,,
            GenuineIntel-6-CF,V1.14,/SPR/events/sapphirerapids_core.json,core,,,
            GenuineIntel-6-6A,V1.21,/ICX/events/icelakex_core.json,core,,,
            GenuineIntel-6-6A,V1.21,/ICX/events/icelakex_uncore.json,uncore,,,
            GenuineIntel-6-6A,V1.21,/ICX/events/icelakex_uncore_experimental.json,uncore experimental,,,
            GenuineIntel-6-6C,V1.22,/ICX/events/icelakex_core.json,core,,,
            GenuineIntel-6-6C,V1.21,/ICX/events/icelakex_uncore.json,uncore,,,
            GenuineIntel-6-6C,V1.21,/ICX/events/icelakex_uncore_experimental.json,uncore experimental,,,
        Failure:
            0x6C references both V1.21 and V1.22.
    """

    logger.info('Checking mapfile.csv for consistent event versions.')

    mapfile_data = load_mapfile(perfmon_repo_path)

    for model in get_unique_mapfile_models(mapfile_data):
        # Extract file versions for rows with a matching model.
        skip_types = ['metrics', 'retire latency']  # Not event file rows
        event_file_versions = [
            x['Version']
            for x in mapfile_data
            if x['Family-model'] == model and x['EventType'] not in skip_types
        ]
        event_file_versions = set(event_file_versions)

        if len(event_file_versions) != 1:
            msg = f'Family-model {model} has multiple event file versions {event_file_versions}'
            raise RuntimeError(msg)


if __name__ == '__main__':
    logging.basicConfig(format='%(levelname).4s; %(message)s', level=logging.INFO)

    script_dir = Path(__file__).resolve().parent
    perfmon_repo_path = script_dir.parents[2]

    parser = argparse.ArgumentParser(description='Verify mapfile.csv.',
                                     epilog='This utility does not require any arguments.')
    args = parser.parse_args()

    verifications = [
        verify_mapfile_schema,
        verify_mapfile_paths,
        verify_event_file_versions,
        verify_event_type_matches_file,
        verify_family_model_maps_to_event_files,
        verify_family_model_maps_to_metric_files,
        verify_mapfile_duplicate_types,
        verify_mapfile_model_event_versions,
    ]
    for verification in verifications:
        verification(perfmon_repo_path)

    logger.info('All mapfile.csv checks passed.')
