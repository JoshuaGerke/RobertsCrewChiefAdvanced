import json
import re
from pathlib import Path

print(
r"""
When CC warns about a change in R3E class IDs, copy

steamapps\common\raceroom racing experience\Game\GameData\General\r3e-data.json

to this folder and run this script to check CrewChiefV4\carClassData.json 
for new class IDs and add them to updated_carClassData.json which can
then be compared with the original CrewChiefV4\carClassData.json and merged manually if needed.

Compares carClassData.json / raceroomClassIds
with     r3e-data.json     / classes

Press enter to continue:
"""
)
input()
# -----------------------------
# Helper: load JSON with # comments
# -----------------------------
def load_json_with_comments(path):
    encodings = ["utf-8", "cp1252", "latin-1"]

    for encoding in encodings:
        try:
            with open(path, "r", encoding=encoding) as f:
                lines = f.readlines()
            break
        except UnicodeDecodeError:
            continue
    else:
        raise ValueError(f"Unable to decode file: {path}")

    cleaned_lines = []

    for line in lines:
        stripped = line.lstrip()

        if stripped.startswith("#"):
            continue

        cleaned_lines.append(line)

    return json.loads("".join(cleaned_lines))


# -----------------------------
# Load source data
# -----------------------------
source_data = load_json_with_comments(r"r3e-data.json") # copied from steamapps\common\raceroom racing experience\Game\GameData\General\r3e-data.json

crewchief_path = Path(
    r"..\..\carClassData.json"
)

existing_data = load_json_with_comments(crewchief_path)

car_classes = existing_data["CarClasses"]

# -----------------------------
# Build reverse index:
# raceroomClassId -> entry
# -----------------------------
id_index = {}

for entry in car_classes:
    for rr_id in entry.get("raceroomClassIds", []):
        id_index[int(rr_id)] = entry

# -----------------------------
# Merge missing IDs
# -----------------------------
for class_id, class_data in source_data.get("classes", {}).items():

    class_id_int = int(class_id)
    print(class_id_int)

    # already exists somewhere ? skip
    if class_id_int in id_index:
        continue
    print("New", class_id)

    # create enum name from input class name
    class_name = class_data.get("Name", "")
    car_class_enum = re.sub(r"[^A-Za-z0-9]+", "_", class_name).upper().strip("_")

    new_entry = {
        "carClassEnum": car_class_enum,
        "raceroomClassIds": [class_id_int]
    }

    car_classes.append(new_entry)
    id_index[class_id_int] = new_entry

# -----------------------------
# Save
# -----------------------------
json_text = json.dumps(existing_data, indent=2, ensure_ascii=False)

# Now restore the format to match CC's:

# Collapse lists that contain only strings/numbers/bools/null onto one line
def collapse(match):
    items = re.split(r"\s*,\s*", match.group(1).strip())
    return "[ " + ", ".join(items) + " ]"

json_text = re.sub(
    r'\[\s*((?:"[^"\\]*(?:\\.[^"\\]*)*"|-?\d+(?:\.\d+)?|true|false|null)'
    r'(?:\s*,\s*(?:"[^"\\]*(?:\\.[^"\\]*)*"|-?\d+(?:\.\d+)?|true|false|null))*)\s*\]',
    collapse,
    json_text,
)

out_path = "updated_carClassData.json"
with open(out_path, "w", encoding="utf-8") as f:
    f.write(json_text)

print(f"Done -> {out_path}")