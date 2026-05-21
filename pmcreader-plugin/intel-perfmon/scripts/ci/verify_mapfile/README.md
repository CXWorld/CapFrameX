# Verify `mapfile.csv`

## Overview

The goal of this helper script is to automatically check `mapfile.csv` for common issues. A few
examples:

* Bad file paths.
* Incorrect versions.
* Mismatched uncore experimental.
* Unexpected columns.
* Event files missing from family models.

## Setup

Install required packages either using the package manager or with `pip`.

```bash
pip install -r requirements.txt
```

## Running

```bash
python verify_mapfile.py
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

## Formatting

VS Code was configured as below for formatting.

```json
"[python]": {
    "editor.defaultFormatter": "eeyore.yapf"
},
"yapf.args": [
    "--style={based_on_style: google, indent_width: 4, column_limit: 100}"
]
```
