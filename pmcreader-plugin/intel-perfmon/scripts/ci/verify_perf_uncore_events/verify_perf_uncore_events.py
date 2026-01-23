#!/usr/bin/env python3
# Copyright (C) 2025 Intel Corporation
# SPDX-License-Identifier: BSD-3-Clause

import argparse
import csv
import json
import logging
import re
from jsonschema import validate, ValidationError
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


def verify_config_schema(filepath: Path, schemapath: Path, delimiter: str = ','):
    """Validate the JSON conversion of the csv [filepath] by the json schema [schemapath]."""
    json_s = csv_to_jsonstr(filepath, delimiter=delimiter)
    json_data = json.loads(json_s)
    schema = load_schema(schemapath)

    try:
        validate(json_data, schema)
    except ValidationError as e:
        logger.error(f'Validating {filepath} failed.')
        logger.error(f'  Instance "{e.instance}" does not match schema "{e.schema}".')
        logger.error(f'  Schema path "{"/".join(str(x) for x in e.absolute_schema_path)}".')

        instance_number = e.absolute_path[0]
        line_number = instance_number + 2  # Header and 0-based index
        logger.error(f'  Error found at CSV line {line_number}.')
        raise ValueError('CSV schema validation failed.')


def verify_header(csv_path: Path, schema_path: Path):
    """Basic CSV header verification. Helpful before running full schema validation."""
    with open(schema_path, 'r') as f:
        schema = json.load(f)
        header_pattern = schema['items']['required']

    # Expects the schema and CSV headers to match order.
    expected = ','.join(header_pattern)

    with open(csv_path, 'r') as f:
        header_line = f.readline().strip()

        if header_line != expected:
            logger.error(f'Unexpected CSV header in {csv_path}.')
            logger.error(f'  Expected: {expected}')
            logger.error(f'  Found:    {header_line}')
            raise ValueError('CSV header does not match expected values.')


def run_verifications(perfmon_repo_path: Path, schema_path: Path):
    """Run all verifications for perf-uncore-events-*.csv files."""
    config_dir = Path(perfmon_repo_path, 'scripts', 'config')

    for csv_path in sorted(config_dir.glob('perf-uncore-events-*.csv')):
        logger.info('Verifying %s', csv_path.name)
        verifications = [
            verify_header,
            verify_config_schema,
        ]
        for verification in verifications:
            verification(csv_path, schema_path)


if __name__ == '__main__':
    logging.basicConfig(format='%(levelname).4s; %(message)s', level=logging.INFO)

    script_dir = Path(__file__).resolve().parent
    perfmon_repo_path = script_dir.parents[2]
    schema_path = Path(script_dir, 'perf_uncore_csv_schema.json')

    parser = argparse.ArgumentParser(description='Verify perf-uncore-events-*.csv.',
                                     epilog='This utility does not require any arguments.')
    args = parser.parse_args()

    run_verifications(perfmon_repo_path, schema_path)

    logger.info('All perf-uncore-events-*.csv checks passed.')
