from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Iterable

SCRIPT_DIR = Path(__file__).resolve().parent
VENDOR_DIR = SCRIPT_DIR / "vendor"
if str(VENDOR_DIR) not in sys.path:
    sys.path.insert(0, str(VENDOR_DIR))

import xlsxwriter

REPO_ROOT = SCRIPT_DIR.parent.parent
DATA_DIR = REPO_ROOT / "Utility" / "MumuyAlgorithm" / "Data"
OUTPUT_DIR = REPO_ROOT / "Resource" / "Data" / "Reference"
DEFAULT_OUTPUT_PATH = OUTPUT_DIR / "MumuyReferenceWorkbook.xlsx"
DEFAULT_SUMMARY_PATH = OUTPUT_DIR / "MumuyReferenceWorkbook.summary.tsv"

TOKEN_MAP: dict[str, list[list[str]]] = {
    "f": [["F"]],
    "m": [["M"]],
    "s": [["S"]],
    "d": [["D"]],
    "ob": [["OB"]],
    "lb": [["YB"]],
    "os": [["OS"]],
    "ls": [["YS"]],
    "xb": [["OB"], ["YB"]],
    "xs": [["OS"], ["YS"]],
    "w": [["SP"]],
    "h": [["SP"]],
    "sp": [["SP"]],
}

GENERATION_DELTA = {"F": 1, "M": 1, "S": -1, "D": -1}


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def get_string_list(value) -> list[str]:
    if value is None:
        return []
    if isinstance(value, str):
        return [value]
    return [str(item) for item in value if item is not None]


def primary_term(value) -> str:
    items = get_string_list(value)
    return items[0] if items else ""


def alias_text(value) -> str:
    items = get_string_list(value)
    return ";".join(items[1:]) if len(items) > 1 else ""


def alias_count(value) -> int:
    items = get_string_list(value)
    return max(0, len(items) - 1)


def json_text(value) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def split_top_level_options(content: str) -> list[str]:
    options: list[str] = []
    depth = 0
    buffer: list[str] = []
    for ch in content:
        if ch == "[":
            depth += 1
        elif ch == "]":
            depth -= 1

        if ch == "|" and depth == 0:
            option = "".join(buffer).strip()
            if option:
                options.append(option)
            buffer.clear()
            continue

        buffer.append(ch)

    option = "".join(buffer).strip()
    if option:
        options.append(option)
    return options


def parse_sequence_expression(raw: str) -> list[list[str]]:
    parts: list[object] = []
    i = 0
    while i < len(raw):
        ch = raw[i]
        if ch == "[":
            depth = 1
            j = i + 1
            while j < len(raw) and depth > 0:
                if raw[j] == "[":
                    depth += 1
                elif raw[j] == "]":
                    depth -= 1
                j += 1
            content = raw[i + 1 : j - 1]
            option_strings = split_top_level_options(content)
            option_sequences = []
            for option in option_strings:
                sequence = [part.strip() for part in option.split(",") if part.strip()]
                option_sequences.append(sequence)
            parts.append(option_sequences)
            i = j
            continue
        if ch in ", ":
            i += 1
            continue

        start = i
        while i < len(raw) and raw[i] not in ",[]":
            i += 1
        token = raw[start:i].strip()
        if token:
            parts.append(token)

    sequences: list[list[str]] = [[]]
    for part in parts:
        if isinstance(part, str):
            sequences = [sequence + [part] for sequence in sequences]
        else:
            next_sequences: list[list[str]] = []
            for sequence in sequences:
                for option in part:
                    next_sequences.append(sequence + option)
            sequences = next_sequences
    return sequences


def normalize_selector_token(token: str) -> str:
    normalized = token.strip().replace("\u200b", "")
    if not normalized:
        return ""
    if "&" in normalized:
        normalized = normalized.split("&", 1)[0]
    return normalized.replace("'", "").lower()


def get_unsupported_tokens(sequence: list[str]) -> list[str]:
    unsupported: list[str] = []
    for raw_token in sequence:
        token = normalize_selector_token(raw_token)
        if token and token not in TOKEN_MAP:
            unsupported.append(token)
    return unsupported


def expand_to_symbol_paths(sequence: list[str]) -> list[list[str]]:
    paths: list[list[str]] = [[]]
    for raw_token in sequence:
        token = normalize_selector_token(raw_token)
        if not token:
            continue
        if token not in TOKEN_MAP:
            return []
        next_paths: list[list[str]] = []
        for path in paths:
            for option in TOKEN_MAP[token]:
                next_paths.append(path + option)
        paths = next_paths
    return paths


def compute_generation(symbols: list[str]) -> int:
    return sum(GENERATION_DELTA.get(symbol, 0) for symbol in symbols)


def compute_spouse_parity(symbols: list[str]) -> int:
    return sum(1 for symbol in symbols if symbol == "SP")


def iter_name_map_rows(source_name: str, data: dict[str, object]) -> Iterable[list[object]]:
    for key, value in data.items():
        yield [key, primary_term(value), alias_text(value), alias_count(value), source_name]


def iter_cache_rows(cache_data: dict[str, list[str]]) -> Iterable[list[object]]:
    for term, selectors in cache_data.items():
        for index, selector in enumerate(selectors, start=1):
            yield [term, index, selector, "cache.json"]


def iter_key_value_rows(source_name: str, data: dict[str, object]) -> Iterable[list[object]]:
    for key, value in data.items():
        yield [key, json_text(value), source_name]


def iter_filter_rows(data: list[dict[str, object]]) -> Iterable[list[object]]:
    for index, item in enumerate(data, start=1):
        yield [index, item.get("exp", ""), item.get("str", ""), "filter.json"]


def iter_replace_rows(data: list[dict[str, object]]) -> Iterable[list[object]]:
    for index, item in enumerate(data, start=1):
        yield [index, item.get("exp", ""), json_text(item.get("arr", [])), "replace.json"]


def iter_similar_rows(data: dict[str, str]) -> Iterable[list[object]]:
    for variant, normalized in data.items():
        yield [variant, normalized, "similar.json"]


def iter_expanded_rows(
    source_name: str, data: dict[str, object], unsupported_tokens: set[str]
) -> Iterable[list[object]]:
    for raw_key, value in data.items():
        primary = primary_term(value)
        aliases = alias_text(value)
        alias_total = alias_count(value)
        for sequence in parse_sequence_expression(raw_key):
            selector = ",".join(
                token for token in (normalize_selector_token(item) for item in sequence) if token
            )
            unsupported = get_unsupported_tokens(sequence)
            if unsupported:
                unsupported_tokens.update(unsupported)
                yield [
                    source_name,
                    raw_key,
                    selector,
                    "",
                    "",
                    "",
                    primary,
                    aliases,
                    alias_total,
                    "",
                    "Unsupported token(s): " + ",".join(sorted(set(unsupported))),
                ]
                continue

            symbol_paths = expand_to_symbol_paths(sequence)
            if not symbol_paths:
                yield [
                    source_name,
                    raw_key,
                    selector,
                    "",
                    "",
                    "",
                    primary,
                    aliases,
                    alias_total,
                    "",
                    "Expansion failed",
                ]
                continue

            for symbols in symbol_paths:
                symbol_path = ".".join(symbols)
                yield [
                    source_name,
                    raw_key,
                    selector,
                    symbol_path,
                    compute_generation(symbols),
                    compute_spouse_parity(symbols),
                    primary,
                    aliases,
                    alias_total,
                    "auto-" + symbol_path.replace(".", "-"),
                    f"Expanded from {source_name}",
                ]


def write_sheet(workbook, header_format, name: str, headers: list[str], rows: Iterable[list[object]]) -> int:
    worksheet = workbook.add_worksheet(name)
    for column, header in enumerate(headers):
        worksheet.write(0, column, header, header_format)

    worksheet.freeze_panes(1, 0)
    worksheet.set_row(0, None, header_format)

    row_index = 1
    for row in rows:
        for column, value in enumerate(row):
            worksheet.write(row_index, column, value)
        row_index += 1

    if row_index > 1:
        worksheet.autofilter(0, 0, row_index - 1, len(headers) - 1)
    return row_index - 1


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Rebuild the Mumuy reference workbook from raw JSON sources."
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT_PATH,
        help=f"Workbook output path. Default: {DEFAULT_OUTPUT_PATH}",
    )
    parser.add_argument(
        "--summary",
        type=Path,
        default=DEFAULT_SUMMARY_PATH,
        help=f"Summary TSV output path. Default: {DEFAULT_SUMMARY_PATH}",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Allow overwriting an existing workbook file.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    output_path = args.output.resolve()
    summary_path = args.summary.resolve()

    output_path.parent.mkdir(parents=True, exist_ok=True)
    summary_path.parent.mkdir(parents=True, exist_ok=True)

    main_data = read_json(DATA_DIR / "main.json")
    mode_map = read_json(DATA_DIR / "mode-map.json")
    cache_data = read_json(DATA_DIR / "cache.json")
    branch_data = read_json(DATA_DIR / "branch.json")
    filter_data = read_json(DATA_DIR / "filter.json")
    input_data = read_json(DATA_DIR / "input.json")
    multiple_data = read_json(DATA_DIR / "multiple.json")
    pair_data = read_json(DATA_DIR / "pair.json")
    prefix_data = read_json(DATA_DIR / "prefix.json")
    replace_data = read_json(DATA_DIR / "replace.json")
    similar_data = read_json(DATA_DIR / "similar.json")
    sort_data = read_json(DATA_DIR / "sort.json")

    unsupported_tokens: set[str] = set()
    sheet_summaries: list[tuple[str, int]] = []

    if output_path.exists() and not args.overwrite:
        raise SystemExit(
            f"Refusing to overwrite existing workbook without --overwrite: {output_path}"
        )

    workbook = xlsxwriter.Workbook(str(output_path), {"constant_memory": True})
    header_format = workbook.add_format({"bold": True, "bg_color": "#D9EAF7", "border": 1})

    source_index_rows = [
        [
            "main.json",
            "compact term dictionary",
            "dict",
            len(main_data),
            str(DATA_DIR / "main.json"),
            "authoritative seed term map",
        ],
        [
            "mode-map.json",
            "large selector-to-name map",
            "dict",
            len(mode_map),
            str(DATA_DIR / "mode-map.json"),
            "primary large reference map",
        ],
        [
            "cache.json",
            "derived reverse cache",
            "dict",
            len(cache_data),
            str(DATA_DIR / "cache.json"),
            "diagnostic only, not first authority",
        ],
        [
            "branch.json",
            "branch rewrite rules",
            "dict",
            len(branch_data),
            str(DATA_DIR / "branch.json"),
            "rule support",
        ],
        [
            "filter.json",
            "normalization regex rules",
            "list",
            len(filter_data),
            str(DATA_DIR / "filter.json"),
            "rule support",
        ],
        [
            "input.json",
            "input grouping aliases",
            "dict",
            len(input_data),
            str(DATA_DIR / "input.json"),
            "rule support",
        ],
        [
            "multiple.json",
            "multi-selector term dictionary",
            "dict",
            len(multiple_data),
            str(DATA_DIR / "multiple.json"),
            "extended dictionary source",
        ],
        [
            "pair.json",
            "relationship pair labels",
            "dict",
            len(pair_data),
            str(DATA_DIR / "pair.json"),
            "rule support",
        ],
        [
            "prefix.json",
            "prefix expansion rules",
            "dict",
            len(prefix_data),
            str(DATA_DIR / "prefix.json"),
            "rule support",
        ],
        [
            "replace.json",
            "term replacement rules",
            "list",
            len(replace_data),
            str(DATA_DIR / "replace.json"),
            "rule support",
        ],
        [
            "similar.json",
            "variant normalization map",
            "dict",
            len(similar_data),
            str(DATA_DIR / "similar.json"),
            "rule support",
        ],
        [
            "sort.json",
            "sort preference map",
            "dict",
            len(sort_data),
            str(DATA_DIR / "sort.json"),
            "rule support",
        ],
    ]

    sheet_summaries.append(
        (
            "SourceIndex",
            write_sheet(
                workbook,
                header_format,
                "SourceIndex",
                ["source_name", "role", "top_level_type", "top_level_count", "source_path", "recommended_use"],
                source_index_rows,
            ),
        )
    )
    sheet_summaries.append(
        ("MainRaw", write_sheet(workbook, header_format, "MainRaw", ["raw_key", "primary_term", "aliases", "alias_count", "source_name"], iter_name_map_rows("main.json", main_data)))
    )
    sheet_summaries.append(
        ("ModeMapRaw", write_sheet(workbook, header_format, "ModeMapRaw", ["raw_key", "primary_term", "aliases", "alias_count", "source_name"], iter_name_map_rows("mode-map.json", mode_map)))
    )
    sheet_summaries.append(
        ("MultipleRaw", write_sheet(workbook, header_format, "MultipleRaw", ["raw_key", "primary_term", "aliases", "alias_count", "source_name"], iter_name_map_rows("multiple.json", multiple_data)))
    )
    sheet_summaries.append(
        ("CacheRaw", write_sheet(workbook, header_format, "CacheRaw", ["term", "selector_index", "selector", "source_name"], iter_cache_rows(cache_data)))
    )
    sheet_summaries.append(
        ("BranchRules", write_sheet(workbook, header_format, "BranchRules", ["key", "value_json", "source_name"], iter_key_value_rows("branch.json", branch_data)))
    )
    sheet_summaries.append(
        ("PairRules", write_sheet(workbook, header_format, "PairRules", ["key", "value_json", "source_name"], iter_key_value_rows("pair.json", pair_data)))
    )
    sheet_summaries.append(
        ("InputRules", write_sheet(workbook, header_format, "InputRules", ["key", "value_json", "source_name"], iter_key_value_rows("input.json", input_data)))
    )
    sheet_summaries.append(
        ("PrefixRules", write_sheet(workbook, header_format, "PrefixRules", ["key", "value_json", "source_name"], iter_key_value_rows("prefix.json", prefix_data)))
    )
    sheet_summaries.append(
        ("FilterRules", write_sheet(workbook, header_format, "FilterRules", ["index", "exp", "replacement", "source_name"], iter_filter_rows(filter_data)))
    )
    sheet_summaries.append(
        ("ReplaceRules", write_sheet(workbook, header_format, "ReplaceRules", ["index", "exp", "arr_json", "source_name"], iter_replace_rows(replace_data)))
    )
    sheet_summaries.append(
        ("SimilarRules", write_sheet(workbook, header_format, "SimilarRules", ["variant", "normalized", "source_name"], iter_similar_rows(similar_data)))
    )
    sheet_summaries.append(
        ("SortRules", write_sheet(workbook, header_format, "SortRules", ["key", "value_json", "source_name"], iter_key_value_rows("sort.json", sort_data)))
    )
    sheet_summaries.append(
        (
            "ExpandedMain",
            write_sheet(
                workbook,
                header_format,
                "ExpandedMain",
                ["source_name", "raw_key", "selector", "symbol_path", "generation", "spouse_parity", "primary_term", "aliases", "alias_count", "relation_id", "notes"],
                iter_expanded_rows("main.json", main_data, unsupported_tokens),
            ),
        )
    )
    sheet_summaries.append(
        (
            "ExpandedModeMap",
            write_sheet(
                workbook,
                header_format,
                "ExpandedModeMap",
                ["source_name", "raw_key", "selector", "symbol_path", "generation", "spouse_parity", "primary_term", "aliases", "alias_count", "relation_id", "notes"],
                iter_expanded_rows("mode-map.json", mode_map, unsupported_tokens),
            ),
        )
    )
    sheet_summaries.append(
        (
            "UnsupportedTokens",
            write_sheet(
                workbook,
                header_format,
                "UnsupportedTokens",
                ["token"],
                ([ [token] for token in sorted(unsupported_tokens) ]),
            ),
        )
    )

    workbook.close()

    summary_lines = ["sheet_name\trow_count"]
    summary_lines.extend(f"{name}\t{count}" for name, count in sheet_summaries)
    SUMMARY_PATH.write_text("\n".join(summary_lines) + "\n", encoding="utf-8")

    print(f"Workbook={output_path}")
    print(f"Summary={summary_path}")
    print(f"UnsupportedTokenCount={len(unsupported_tokens)}")


if __name__ == "__main__":
    main()

