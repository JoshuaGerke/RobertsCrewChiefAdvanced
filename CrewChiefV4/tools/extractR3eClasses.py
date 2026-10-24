# Read the RaceRoom online data file and extract any new raceroomClassIds and carClassEnums
# Present these to be edited into carClassData.json and CarData.cs


import json
import re
from pathlib import Path
import urllib.request

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
#source_data = load_json_with_comments(r"k:\SteamLibrary\steamapps\common\raceroom racing experience\Game\GameData\General\r3e-data.json")

# from https://github.com/kwstudios-sweden/r3e-api
url = "https://raw.githubusercontent.com/kwstudios-sweden/r3e-spectator-overlay/refs/heads/master/r3e-data.json"

with urllib.request.urlopen(url) as response:
    source_data = json.loads(response.read().decode("utf-8"))

crewchief_path = Path(
    r"..\carClassData.json"
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

new_car_classes = []
new_car_class_enum = []
# -----------------------------
# Merge missing IDs
# -----------------------------
for class_id, class_data in source_data.get("classes", {}).items():

    class_id_int = int(class_id)

    # already exists somewhere ? skip
    if class_id_int in id_index:
        print(f"Existing class ID {class_id_int}")
        continue

    # create enum name from input class name
    class_name = class_data.get("Name", "")
    car_class_enum = re.sub(r"[^A-Za-z0-9]+", "_", class_name).upper().strip("_")
    print(f"New class ID {class_id_int}: '{car_class_enum}'")
    new_car_class_enum.append(f"{car_class_enum:40}\t{class_id_int}")

    new_entry = {
        "carClassEnum": car_class_enum,
        "raceroomClassIds": [class_id_int]
    }

    new_car_classes.append(new_entry)
    id_index[class_id_int] = new_entry

# -----------------------------
# Save
# -----------------------------
json_text = json.dumps(new_car_classes, indent=2)

# Collapse ALL arrays into one line
json_text = re.sub(
    r'\[\s*((?:-?\d+|".*?")(?:\s*,\s*(?:-?\d+|".*?"))*)\s*\]',
    lambda m: "[ " + re.sub(r"\s+", " ", m.group(1).strip()) + " ]",
    json_text
)

out_path = "updated_carClassData.json"
with open(out_path, "w", encoding="utf-8") as f:
    f.write(json_text)

new_car_class_enum.sort()
print("\nClass names for new class IDs\n")
for car_class_enum in new_car_class_enum:
  print(car_class_enum)

print("\nClass names formatted for inclusion in CarData.cs\n")
for car_class_enum in new_car_class_enum:
  print(f"            {car_class_enum.split()[0]},")

print(f"\nDone -> {out_path}")