"""
NER extraction from a .docx procurement document using the Anthropic API.
Uses tool use (function calling) to get guaranteed structured JSON back.

Requirements:
    pip install anthropic python-docx
"""

import json
import os
from docx import Document
import anthropic


# ── Configuration ────────────────────────────────────────────────────────────

DOCX_PATH = r"C:\Users\skeye\PROGRAMMING\Python\ExtractTextApp\myfile.docx"

# Set your key here, or export ANTHROPIC_API_KEY in your shell and leave this as None.
API_KEY = None  # e.g. "sk-ant-..."


# ── Tool schema ───────────────────────────────────────────────────────────────
# Claude will call this tool with the extracted entities as arguments,
# which gives us clean, typed JSON instead of free-form text.

TOOL = {
    "name": "record_entities",
    "description": "Record named entities extracted from a procurement document.",
    "input_schema": {
        "type": "object",
        "properties": {
            "organizations": {
                "type": "array",
                "items": {"type": "string"},
                "description": "Government agencies, departments, divisions, or vendor names."
            },
            "document_info": {
                "type": "object",
                "properties": {
                    "solicitation_number": {"type": "string"},
                    "title":               {"type": "string"},
                    "document_type":       {"type": "string"},
                    "fiscal_year":         {"type": "string"}
                }
            },
            "items": {
                "type": "array",
                "description": "Goods or services being procured.",
                "items": {
                    "type": "object",
                    "properties": {
                        "name":           {"type": "string"},
                        "quantity":       {"type": "string"},
                        "specifications": {
                            "type": "array",
                            "items": {"type": "string"}
                        }
                    },
                    "required": ["name"]
                }
            },
            "monetary_values": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "amount": {"type": "string"},
                        "label":  {"type": "string"}
                    }
                }
            },
            "legal_authorities": {
                "type": "array",
                "items": {"type": "string"},
                "description": "Laws, codes, or regulations cited (e.g. 'Northlandia Procurement Code § 18-201')."
            },
            "procurement_method": {
                "type": "string",
                "description": "e.g. Micro-Purchase, Sealed Bid, Negotiated."
            },
            "dates": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "value": {"type": "string"},
                        "label": {"type": "string"}
                    }
                }
            },
            "contacts": {
                "type": "array",
                "description": "Named people and their roles. Omit blank fill-in fields.",
                "items": {
                    "type": "object",
                    "properties": {
                        "role": {"type": "string"},
                        "name": {"type": "string"}
                    }
                }
            }
        },
        "required": ["organizations", "items"]
    }
}


# ── Functions ─────────────────────────────────────────────────────────────────

def extract_text(docx_path: str) -> str:
    """Pull plain text from a .docx file, one paragraph per line."""
    doc = Document(docx_path)
    return "\n".join(para.text for para in doc.paragraphs)


def extract_entities(text: str, client: anthropic.Anthropic) -> dict:
    """Send document text to Claude and return structured entities as a dict."""
    response = client.messages.create(
        model="claude-opus-4-6",
        max_tokens=1024,
        tools=[TOOL],
        tool_choice={"type": "any"},   # forces Claude to call the tool
        messages=[
            {
                "role": "user",
                "content": (
                    "Extract named entities from this procurement document.\n"
                    "Skip any blank fill-in fields (shown as underscores like ______) "
                    "— only record values that are actually present.\n\n"
                    f"Document:\n{text}"
                )
            }
        ]
    )

    for block in response.content:
        if block.type == "tool_use":
            return block.input

    return {}   # shouldn't happen given tool_choice="any"


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    api_key = API_KEY or os.environ.get("ANTHROPIC_API_KEY")
    if not api_key:
        raise ValueError(
            "No API key found. Set API_KEY in this script "
            "or export ANTHROPIC_API_KEY in your shell."
        )

    client = anthropic.Anthropic(api_key=api_key)

    print(f"Reading: {DOCX_PATH}")
    text = extract_text(DOCX_PATH)

    print("Sending to Claude for NER...")
    entities = extract_entities(text, client)

    print("\n=== Extracted Entities ===\n")
    print(json.dumps(entities, indent=2))


if __name__ == "__main__":
    main()
