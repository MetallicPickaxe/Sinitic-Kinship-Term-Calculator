import argparse
import csv
import sys
from pathlib import Path


def write_sheet(wb, path: Path, sheet_name: str):
    ws = wb.add_worksheet(sheet_name)
    header_fmt = wb.add_format({"bold": True, "bg_color": "#D9EAF7", "border": 1})
    last_row = 0
    last_cols = 0
    with path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.reader(f, delimiter="\t")
        for r, row in enumerate(reader):
            last_row = r
            last_cols = max(last_cols, len(row))
            for c, value in enumerate(row):
                ws.write(r, c, value, header_fmt if r == 0 else None)
    ws.freeze_panes(1, 0)
    if last_cols:
        ws.autofilter(0, 0, last_row, last_cols - 1)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--compact", required=True)
    parser.add_argument("--unsupported", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    repo = Path(__file__).resolve().parents[2]
    sys.path.insert(0, str(repo / "Utility" / "Scripts" / "vendor"))
    import xlsxwriter

    compact = Path(args.compact).resolve()
    unsupported = Path(args.unsupported).resolve()
    output = Path(args.output).resolve()

    wb = xlsxwriter.Workbook(str(output), {"constant_memory": True, "strings_to_urls": False})
    write_sheet(wb, compact, "MainCompact" if "Main" in output.stem else "ModeMapCompact")
    write_sheet(wb, unsupported, "Unsupported")
    wb.close()
    print(f"XLSX={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
