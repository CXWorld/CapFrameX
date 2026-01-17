# Copyright (C) 2021 Intel Corporation
# Copyright (C) 2021 Google LLC
# SPDX-License-Identifier: BSD-3-Clause

# REQUIREMENT: Install Python3 on your machine
# USAGE: Run from command line with the following parameters -
#
# perf_format_converter.py
# -i (--finput) <Path to Input File> (optional)
# -t (--tma)    <Enable TMA conversion> (optional)
#
# ASSUMES: That the script is being run in the scripts folder of the repo and that all files
#          are JSON format
# OUTPUT: The converted files are outputted to the outputs directory
#
# EXAMPLE: python perf_format_converter -i ./inputs/input_file.json
#   -> Converts single file input_file.json
# EXAMPLE: python perf_format_converter
#   -> Converts all files in input dir

import re
import sys
import json
import argparse
from pathlib import Path

class Metric:
    """
    Metric class. Only used to store data to be serialized into json
    """

    def __init__(self, brief_description, metric_expr,
                 metric_group, metric_name, scale_unit, 
                 metric_threshold, public_description):
        self.BriefDescription = brief_description
        self.MetricExpr = metric_expr
        self.MetricGroup = metric_group
        self.MetricName = metric_name
        self.ScaleUnit = scale_unit
        self.MetricThreshold = metric_threshold
        self.PublicDescription = public_description
        self.MetricgroupNoGroup = None
        self.DefaultMetricgroupName = None

    def apply_extra_properties(self, platform):
        if platform["DefaultLevel"] > 0:
            if self.MetricGroup and "info" not in self.MetricName:
                if "TopdownL1" in self.MetricGroup:
                    if "Default" in self.MetricGroup:
                        self.MetricGroupnoGroup = "TopdownL1;Default"
                        self.DefaultMetricgroupName = "TopdownL1"
                    else:
                        self.MetricgroupNoGroup = "TopdownL1"
                elif "TopdownL2" in self.MetricGroup:
                    if "Default" in self.MetricGroup:
                        self.MetricGroupnoGroup = "TopdownL2;Default"
                        self.DefaultMetricgroupName = "TopdownL2"
                    else:
                        self.MetricgroupNoGroup = "TopdownL2"

# File locations
FILE_PATH = Path(__file__).parent.resolve()
REPLACEMENT_CONFIG_PATH = Path("./config/replacements_config.json")
PLATFORM_CONFIG_PATH = Path("./config/platform_config.json")
INPUT_DIR_PATH = Path("./inputs/")
OUTPUT_DIR_PATH = Path("./outputs/")

# Fields to always display even if empty
PERSISTENT_FIELDS = ["MetricGroup", "BriefDescription"]

# Operators
OPERATORS = ["+", "-", "/", "*", "(", ")", "max(", "min(", "if", "<", ">", ",", "else", "<=", ">="]

def main():
    # Get file pointers from args
    arg_input_file, args_tma = get_args()

    # Check that intput/output dirs exists
    ensure_directories()

    # Check for input file arg
    if arg_input_file:
        # If input file given, convert just input file
        convert_file(arg_input_file, args_tma)
    else:
        # If no input file, convert all files in input dir
        glob = Path(FILE_PATH, INPUT_DIR_PATH).glob("*.json")
        for file in glob:
            convert_file(file, args_tma)

def ensure_directories():
    # Check that intput/output dirs exists
    try:
        if not Path(FILE_PATH, INPUT_DIR_PATH).exists():
            Path(FILE_PATH, INPUT_DIR_PATH).mkdir(parents=True, exist_ok=True)
        if not Path(FILE_PATH, OUTPUT_DIR_PATH).exists():
            Path(FILE_PATH, OUTPUT_DIR_PATH).mkdir(parents=True, exist_ok=True)
    except IOError as e:
        sys.exit(f"[ERROR] - Error setting up inpur/output dirs {str(e)}. Exiting")

def convert_file(file_path: Path, output_tma: bool):
    """
    Takes a standard json file and outputs a converted perf file

    @param file_path: path to standard json file
    @param output_tma: boolean representing if tma metrics will be outputted
    """
    with open(file_path) as input_file:
        # Initialize converter with input file
        format_converter = PerfFormatConverter(input_file)
        
        # Get the platform of the file
        platform = format_converter.get_platform(file_path.name)
        #print(f"{platform["Name"]} - {platform["Core"]} Core")
        if not platform:
            print(f"[ERROR] - Could not determine platform for file: <{str(file_path.name)}> Skipping...")
            return

        # Deserialize input DB Json to dictionary
        print(f"Processing file: <{str(file_path.name)}>")
        format_converter.deserialize_input()

        format_converter.populate_issue_dict()

        # Convert the dictionary to list of Perf format metric objects
        perf_metrics = format_converter.convert_to_perf_metrics(platform, output_tma)
        if not perf_metrics:
            print(f"[ERROR] - Could not convert metrics or file is empty: <{str(file_path.name)}> Skipping...")
            return

        # Get the output file
        output_file_path = get_output_file(input_file.name)
        with open(output_file_path, "w+", encoding='ascii') as output_file_fp:
            # Serialize metrics to Json file
            format_converter.serialize_output(perf_metrics, output_file_fp)


def get_args():
    """
    Gets the arguments for the script from the command line

    @returns: input and output files
    """
    # Description
    parser = argparse.ArgumentParser(description="Perf Converter Script")

    # Arguments
    parser.add_argument("-i", "--finput", type=Path,
                        help="Path of input json file", required=False)
    parser.add_argument("-t", "--tma", type=bool, default=False,
                       help="Output TMA metrics [true/false]", required=False)
    
    # Get arguments
    args = parser.parse_args()

    return args.finput, args.tma


def get_output_file(path: str) -> Path:
    """
    Takes the path to the input file and converts it to the output file path.
    eg. inputs/input_file.json -> outputs/input_file_perf.json

    @param path: string containing the path to input file
    @returns: string containing output file path
    """
    file_name = Path(path).stem + "_perf.json"
    return Path(FILE_PATH, OUTPUT_DIR_PATH, file_name)


def pad(string: str) -> str:
    """
    Adds a one space padding to an inputted string

    @param string: string to pad
    @returns: padded string
    """
    return " " + string.strip() + " "


def isNum(string: str) -> str:
    """
    Takes an inputted string and outputs if the string is a num.
    eg. 1.0, 1e9, 29

    @param string: string to check
    @returns: if string is a num as boolean
    """
    if string.isdigit():
        return True
    if string.replace('.', '', 1).isdigit():
        return True
    if string.replace('e', '', 1).isdigit() and not string.startswith("e") and not string.endswith("e"):
        return True
    return False

def fixPercentages(string: str) -> str:
    """
    Takes an inputted string containing a percentage value in % format, and
    changed it to decimal format.
    eg. 60 -> 0.6

    @param string: string containing percentages to fix
    @returns: string with fixed percentages
    """
    splits = string.split(" ")
    fixed = [str(float(split) / 100) if isNum(split) else split for split in splits]
    return " ".join(fixed)

def fixSpacing(string: str) -> str:
    """
    Takes an inputted formula as a string and fixes the spacing in the formula.
    eg. (a / b) -> ( a / b )

    @param string: string containing formula to fix
    @returns: string containing fixed formula
    """
    fixed = string

    # Fix instances of " (x"
    regex = r"(^|[\s])(\()([^\s])"
    match = re.search(regex, fixed)
    while(match):
        fixed = fixed.replace(match.group(0), f" {match.group(2)} {match.group(3)}")
        match = re.search(regex, fixed)

    # Fix instances of "x) "
    regex = r"([^\s])(\))($|[\s])"
    match = re.search(regex, fixed)
    while(match):
        fixed = fixed.replace(match.group(0), f"{match.group(1)} {match.group(2)} ")
        match = re.search(regex, fixed)

    return fixed.strip()

class PerfFormatConverter:
    """
    Perf Format Converter class. Used to convert the json files. Contains all
    methods required to load, transform, and output perf metrics.
    """

    def __init__(self, input_fp):
        self.input_fp = input_fp
        self.input_data = None
        self.issue_dict = {}
        self.metric_name_replacement_dict = None
        self.metric_assoc_replacement_dict = None
        self.metric_source_event_dict = None
        self.scale_unit_replacement_dict = None
        self.core_option_translation_dict = None
        self.uncore_option_translation_dict = None
        self.platforms = None
        self.init_dictionaries()

    def init_dictionaries(self):
        """
        Loads dictionaries to be used for metric name replacements
        and metric association (events and constants) replacements.
        """
        try:
            full_config_path = Path(FILE_PATH, REPLACEMENT_CONFIG_PATH)
            with open(full_config_path) as replacement_config_fp:
                config_dict = json.load(replacement_config_fp)
        except Exception as e:
            print(f"[ERROR] - Could not open and read config file: {str(e)}. Quitting...")
            sys.exit()

        try:
            full_platform_path = Path(FILE_PATH, PLATFORM_CONFIG_PATH)
            with open(full_platform_path) as platform_config_fp:
                self.platforms = json.load(platform_config_fp)
        except Exception as e:
            print(f"[ERROR] - Could not open and read platform config file: {str(e)}.  Quitting...")
            sys.exit()

        try:
            self.metric_name_replacement_dict = config_dict["metric_name_replacements"]
            self.metric_assoc_replacement_dict = config_dict["metric_association_replacements"]
            self.core_option_translation_dict = config_dict["core_option_translations"]
            self.uncore_option_translation_dict = config_dict["uncore_option_translations"]
            self.metric_source_event_dict = config_dict["metric_source_events"]
            self.scale_unit_replacement_dict = config_dict["scale_unit_replacements"]
        except KeyError as e:
            sys.exit(f"[ERROR] - Error in config JSON format {str(e)}. Exiting")

    def get_platform(self, file_name: str) -> dict:
        """
        Determines the platform of the inputted file. Uses platform_config.json

        @param file_name: string containing the name of the file to get the platform from
        @returns: dictionary containing the platform info or None if not found
        """
        for platform in self.platforms:
            if platform["FileName"].lower() in file_name:
                if platform["IsHybrid"]:    # Hybrid platform. Get correct core
                    for platform in self.platforms:
                        if platform["FileName"].lower() in file_name and platform["Core"].lower() in file_name:
                            return platform
                else:   # Non Hybrid platform. Return platform
                    return platform 
        return None

    def deserialize_input(self):
        """
        Loads in the metric in db format into a dictionary to be transformed.
        """
        self.input_data = json.load(self.input_fp)    

    def populate_issue_dict(self):
        """
        Populates a dictionary containing all metrics that have certain issues. Used to add a 
        "Related metrics:" blurb to descriptions.
        """
        for metric in self.input_data["Metrics"]:
            # Add Threshold issues
            if "Threshold" in metric and "ThresholdIssues" in metric["Threshold"] and metric["Threshold"]["ThresholdIssues"] != "":
                issues = metric["Threshold"]["ThresholdIssues"].split(",")
                for issue in issues:
                    issue = issue.strip()
                    if issue == "#NA":
                        continue
                    elif issue not in self.issue_dict:
                        self.issue_dict[issue] = [self.translate_metric_name(metric)]
                    else:
                        self.issue_dict[issue].append(self.translate_metric_name(metric))

    def convert_to_perf_metrics(self, platform: str, output_tma: bool) -> list[Metric]:
        """
        Converts the json dictionary read into the script to a list of
        metric objects in PERF format.

        @param platform: platform of the file
        @param output_tma: boolean representing if tma metrics will be outputted
        @returns: list of perf metric objects
        """
        metrics = []

        try:
            for metric in self.input_data["Metrics"]:
                # Check if outputting TMA metrics
                if not output_tma:
                    if "TMA" in metric["Category"]:
                        continue
                
                # Add new metric object for each metric dictionary
                new_metric = Metric(
                    public_description=self.get_public_description(metric),
                    brief_description=self.get_brief_description(metric),
                    metric_expr=self.get_expression(metric, platform),
                    metric_group=self.get_groups(metric, platform),
                    metric_name=self.translate_metric_name(metric),
                    scale_unit=self.get_scale_unit(metric),
                    metric_threshold=self.get_threshold(metric))
                new_metric.apply_extra_properties(platform)
                metrics.append(new_metric)
        except KeyError as error:
            print(f"Error in input JSON format during convert_to_perf_metrics(): {str(error)} Skipping...")
            return None
        return metrics

    def get_expression(self, metric: dict, platform: dict) -> str:
        """
        Converts the aliased formulas and events/constants into
        un-aliased expressions.

        @param metric: metric data as a dictionary
        @param platform: dictonary with platform info
        @returns: string containing un-aliased expression
        """
        try:
            # TMA metric
            if "TMA" in metric["Category"]:
                if "BaseFormula" in metric and metric["BaseFormula"] != "":
                    base_formula = metric["BaseFormula"]
                else:
                    print("Error: TMA metric without base formula found")
            # Non TMA metric
            else:
                # Seperate operators with spaces
                aliased_formula = fixSpacing(metric["Formula"])

                # Get formula and events for conversion
                if aliased_formula.startswith("100 *") and metric["UnitOfMeasure"] == "percent":
                    aliased_formula = aliased_formula.replace("100 *", "")
                events = metric["Events"]
                constants = metric["Constants"]

                # Replace event/const aliases with names
                base_formula = aliased_formula.lower()
                for event in events:
                    reg = r"((?<=[\s+\-*\/\(\)])|(?<=^))({})((?=[\s+\-*\/\(\)\[])|(?=$))".format(event["Alias"].lower())
                    base_formula = re.sub(reg, event["Name"], base_formula)
                for const in constants:
                    reg = r"((?<=[\s+\-*\/\(\)])|(?<=^))({})((?=[\s+\-*\/\(\)])|(?=$))".format(const["Alias"].lower())
                    base_formula = re.sub(reg, pad(const["Name"]), base_formula)

            # Take base formula and translate all events
            expression_list = [a.strip() for a in base_formula.split(" ") if a != ""]
            for i, term in enumerate(expression_list):
                if (term not in OPERATORS) and (not isNum(term)) and (not term.startswith("*")):
                    # Term is not an operator or a numeric value
                    if "tma_" not in term:
                        # Translate any event names
                        expression_list[i] = self.translate_metric_event(term, metric, platform)
            
            # Combine into formula
            expression = " ".join(expression_list).strip()

            # Add slots to metrics that have topdown events but not slots
            if "topdown" in expression and "slots" not in expression:
                    expression = "( " + expression + " ) + ( 0 * slots )"
            
            return re.sub(r"[\s]{2,}", " ", expression.strip()).replace("TXL", "TxL")
        
        except KeyError as error:
            sys.exit("Error in input JSON format during get_expressions(): " + str(error) + ". Exiting")


    def get_public_description(self, metric: dict) -> str:
        """
        Takes a base description and adds the extra "Related metrics" and "Sample with"
        blurbs to the end.

        @param metric: metric data as a dictionary
        @returns: string containing the extended description
        """
        # Start with base description
        description = metric["BriefDescription"].strip()
        if not description.endswith("."):
            description += ". "
        else:
            description += " "
    
        # Add "Sample with:" blurb
        if "LocateWith" in metric and metric["LocateWith"] != "":
            events = metric["LocateWith"].split(";")
            events = [event.strip() for event in events if event.strip() != "#NA"]
            if len(events) >= 1:
                description += f"Sample with: {', '.join(events)}" + ". "

        # Add "Related metrics:" blurb
        related_metrics = []
        if "Threshold" in metric and "ThresholdIssues" in metric["Threshold"] and metric["Threshold"]["ThresholdIssues"] != "":
            issues = metric["Threshold"]["ThresholdIssues"].split(",")
            for issue in issues:
                related_metrics.extend(self.issue_dict[issue.strip()])
            
            # Filter out self from list
            related_metrics = sorted(set([m for m in related_metrics if m != self.translate_metric_name(metric)]))
            
            if len(related_metrics) >= 1:
                description += f"Related metrics: {', '.join(related_metrics)}" + ". "
        
        # Make sure description is more than one sentence
        elif description.count(". ") == 1 and description.strip().endswith("."):
            return None
        
        return description.strip()
    
    def get_brief_description(self, metric: dict) -> str:
        """
        Takes a base description and shortens it to a single sentence

        @param metric: metric data as a dictionary
        @returns: string containing the shortened description
        """
         # Start with base description
        description = metric["BriefDescription"] + " "

        # Sanitize 
        if "i.e." in description:   # Special case if i.e. in description
            description = description.replace("i.e.", "ie:")

        # Get only first sentence
        if description.count(". ") > 1:
            description = description.split(". ")[0]
        elif description.count(". ") == 1 and description.strip().endswith("."):
            description = description.strip()
        elif description.count(". ") == 1 and not description.strip().endswith("."):
            description = description.split(". ")[0]
        else:
            description = description.strip()

        # Remove ending period
        if description.endswith("."):
            description = description[0:-1]

        return description.replace("ie:", "i.e.").strip()

    def translate_metric_name(self, metric: dict) -> str:
        """ 
        Replaces the metric name with a replacement found in the metric 
        name replacements json file
        """
        # Check if name has replacement
        if metric["MetricName"] in self.metric_name_replacement_dict:
            return self.metric_name_replacement_dict[metric["MetricName"]]
        else:
            if "TMA" in metric["Category"]:
                return "tma_" + metric["MetricName"].replace(" ", "_").lower()
            return metric["MetricName"]

    def translate_metric_event(self, event_name: str, metric: dict, platform: dict) -> str:
        """
        Replaces the event name with a replacement found in the metric
        association replacements json file. (An "association" is either an event
        or a constant. "Association" is the encompassing term for them both.

        @param event_name: string containing event name
        @returns: string containing un-aliased expression
        """
        translated_event = None

        # Get prefix
        prefix = None
        if platform["IsHybrid"]:    # Hybrid
            if platform["CoreType"] == "P-core":
                if self.is_core_event(event_name):
                    prefix = "cpu_core"
            elif platform["CoreType"] == "E-core":
                if self.is_core_event(event_name):
                    prefix = "cpu_atom"
        else:
            if self.is_core_event(event_name):
                prefix = "cpu"
            else:
                if "unc_cha_" in event_name.lower():
                    prefix = "cha"
                elif "unc_c_" in event_name.lower():
                    prefix = "cbox"

        # Check if association has 1:1 replacement
        if event_name in self.metric_assoc_replacement_dict:
            return self.metric_assoc_replacement_dict[event_name]
        elif event_name.upper() in self.metric_assoc_replacement_dict:
            return self.metric_assoc_replacement_dict[event_name.upper()]
        
        # Check for source_count() constant
        for replacement in self.metric_source_event_dict:
            if re.match(replacement, event_name):
                for event in metric["Events"]:
                    if re.match(self.metric_source_event_dict[replacement], event["Name"]) and ":" not in event["Name"]:
                        return "source_count(" + event["Name"].split(":")[0] + ")"

        # Check for ignored events
        if event_name.lower() == "tsc":
            return event_name.upper()

        # Translate other events
        if ":" in event_name.lower(): 
            if ":retire_latency" in event_name.lower(): # Check for retire latency option
                split = event_name.split(":")
                if platform["IsHybrid"]:
                    translated_event = f"{prefix}@{split[0]}@R"
                else:
                    translated_event = split[0] + ":R"
            else: # Check for other event option
                split = event_name.split(":")
                base_event = split[0]   # Base event
                event_options = split[1:]   # Event options
            
                translated_options = []
                for option in event_options:
                    translated_option = self.translate_event_option(option, self.is_core_event(base_event))
                    if translated_option is not None:
                        translated_options.append(translated_option)
                if prefix:
                    # translated_event = f"{prefix}@{base_event.upper()}\\\\,{'\\\\,'.join(translated_options)}@"
                    translated_event = "{}@{}\\,{}@".format(prefix, base_event.upper(), "\\,".join(translated_options))

                else:
                    # translated_event = f"{base_event.upper()}@{'\\,'.join(translated_options)}@"
                    translated_event = "{}@{}@".format(base_event.upper(), "\\,").join(translated_options)
        else: # No event options
            if prefix and self.is_core_event(event_name) and platform["IsHybrid"]:
                translated_event = f"{prefix}@{event_name.upper()}@"
            else:
                translated_event = event_name.upper()
        
        return translated_event.replace("RXL", "RxL")

    def is_core_event(self, event: str) -> bool:
        if "unc_" in event.lower():
            return False
        return True
    
    def translate_event_option(self, full_option: str, is_core_event: bool) -> str:
        if "=" in full_option:
            split = full_option.split("=")
            option = split[0]
            value = split[1]
        elif "0x" in full_option.lower():
            split = full_option.lower().split("0x")
            option = split[0]
            value = split[1]
        else:
            match = re.match(r"([a-zA-Z]+)([\d]+)", full_option.lower())
            if match:
                option = match[1]
                value = match[2]
        
        translated_option = option
        try:
            if is_core_event:
                translated_option = self.core_option_translation_dict[option.lower()]
            else:
                translated_option = self.uncore_option_translation_dict[option.lower()]
        except KeyError:
            return None
        
        if translated_option is None or translated_option == "None":
            return None
        
        if "0x" not in value:
            value = f"0x{value}"
            
        return f"{translated_option}\\={value}"

    def serialize_output(self, perf_metrics: list, output_fp):
        """
        Serializes the list of perf metrics into a json file output.
        """
        # Dump new metric object list to output json file
        json.dump(perf_metrics,
                  output_fp,
                  # default=lambda obj: obj.__dict__,
                  default=lambda obj: {key: value for key, value in obj.__dict__.items()
                                           if value or key in PERSISTENT_FIELDS},
                  ensure_ascii=True,
                  indent=4)

    def get_scale_unit(self, metric: dict) -> str:
        """
        Converts a metrics unit of measure field into a scale unit. Scale unit
        is formatted as a scale factor x and a unit. Eg. 1ns, 10Ghz, etc

        @param metric: metric data as a dictionary
        @returns: string containing the scale unit of the metric
        """

        # Get the unit of measure of the metric
        unit = metric["UnitOfMeasure"]
        metric_type = metric["Category"]

        if metric["Formula"].startswith("100 *") and unit == "percent":
            return "100%"
        elif unit in self.scale_unit_replacement_dict:
            return "1" + self.scale_unit_replacement_dict[unit]
        else:
            return None

    def get_groups(self, metric: dict, platform: dict) -> str:
        """
        Converts a metrics group field delimited by commas to a new list
        delimited by semi-colons

        @param metric: metric json object
        @returns: new string list of groups delimited by semi-colons
        """
        if "MetricGroup" not in metric:
            return ""
        
        # Get current groups
        groups = metric["MetricGroup"]
        if groups.isspace() or groups == "":
            new_groups = []
        else:
            #new_groups = [g.strip() for g in groups.split(";") if not g.isspace() and g != ""]
            new_groups = [g.strip() for g in re.split(";|,", groups) if not g.isspace() and g != ""]

        # TMA metrics
        if metric["Category"] == "TMA":

            # Add level and parent groups
            if "Info" not in metric["MetricName"] or "TmaL1" in metric["MetricGroup"]:
                new_groups.append("TopdownL" + str(metric["Level"]))
                new_groups.append("tma_L" + str(metric["Level"]) + "_group")
                if "ParentCategory" in metric:
                    new_groups.append("tma_" + metric["ParentCategory"].lower().replace(" ", "_") + "_group")
            
            # Add default group for levels 1 & 2
            if metric["Level"] <= platform["DefaultLevel"] and "info" not in metric["MetricName"].lower():
                new_groups.append("Default")

        # Add count domain
        if "CountDomain" in metric and metric["CountDomain"] != "":
            new_groups.append(metric["CountDomain"])

        # Add Threshold issues
        if "Threshold" in metric and "ThresholdIssues" in metric["Threshold"] and metric["Threshold"]["ThresholdIssues"] != "":
            threshold_issues = [f"tma_{issue.replace('$', '').replace('~', '').strip()}" for issue in metric["Threshold"]["ThresholdIssues"].split(",")]
            new_groups.extend(threshold_issues)

        return ";".join(new_groups) if new_groups.count != 0 else ""

    def get_threshold(self, metric: dict):
        if "Threshold" in metric:
            if "BaseFormula" in metric["Threshold"]:
                threshold = metric["Threshold"]["BaseFormula"].replace("&&", "&").replace("||", "|")
                if metric["UnitOfMeasure"] == "percent":
                    return fixPercentages(self.clean_metric_names(threshold))
                else:
                    return self.clean_metric_names(threshold)
            elif "Formula" in metric["Threshold"]:
                threshold = metric["Threshold"]["Formula"].replace("&&", "&").replace("||", "|")
                return self.clean_metric_names(threshold)

    def clean_metric_names(self, formula: str):
        return re.sub(r'\([^\(\)]+\)', "", formula).lower().replace("metric_","").replace("..", "")


if __name__ == "__main__":
    main()
