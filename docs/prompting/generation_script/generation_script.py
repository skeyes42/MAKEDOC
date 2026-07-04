import json
from pathlib import Path

def prompt_path(label):
    """Prompt the user for a path and return a Path object."""
    raw = input(f"Enter {label} path: ").strip()
    return Path(raw)

def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def load_template(path):
    with open(path, "r", encoding="utf-8") as f:
        return f.read()

def substitute(template, params):
    # Substitute DocTitle and ReqNumber
    output = template.replace("{{DocTitle}}", params["DocTitle"])
    output = output.replace("{{ReqNumber}}", params["ReqNumber"])
    output = output.replace("{{Phase}}", params["Phase"])
    output = output.replace("{{ToPhase}}", params["ToPhase"])
    output = output.replace("{{FromPhase}}", params["FromPhase"])
    output = output.replace("{{Tier}}", params["Tier"])

    # Substitute line items
    for idx, item in enumerate(params["lineItems"], start=1):
        output = output.replace(f"{{{{Description{idx}}}}}", item["Description"])
        output = output.replace(f"{{{{NAICS{idx}}}}}", str(item["NAICS"]))
        output = output.replace(f"{{{{Unit{idx}}}}}", item["Unit"])
        output = output.replace(f"{{{{Qty{idx}}}}}", str(item["Qty"]))
        output = output.replace(f"{{{{Price{idx}}}}}", str(item["UnitPrice"]))

    return output

def write_output(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)

def main():
    print("\n--- Prompted Document Generator ---\n")

    DATA_FILE = prompt_path("DATA_FILE (JSON)")
    TEMPLATE_FILE = prompt_path("TEMPLATE_FILE (MD template)")
    OUTPUT_FILE = prompt_path("OUTPUT_FILE (destination .md)")

    print("\nLoading JSON parameters...")
    params = load_json(DATA_FILE)

    print("Loading template...")
    template = load_template(TEMPLATE_FILE)

    print("Substituting values...")
    filled = substitute(template, params)

    print("Writing output file...")
    write_output(OUTPUT_FILE, filled)

    print(f"\nGenerated: {OUTPUT_FILE}\n")

if __name__ == "__main__":
    main()
