# Verify `perf-uncore-events-*.csv`

## Overview

The goal of this helper script is to automatically check `perf-uncore-events-*.csv` files for structural
issues. 

## Setup

Install required packages either using the package manager or with `pip`.

```bash
pip install -r requirements.txt
```

## Running

```bash
python verify_perf_uncore_events.py
```

### Running Tests

```bash
python -m unittest
```

### Coverage

Coverage output is written to `htmlcov`.

```bash
python -m coverage run -m unittest
python -m coverage html
```
