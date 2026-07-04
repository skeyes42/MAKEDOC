"""
fts_backfill.py
───────────────
Extracts plain text from DOCX blobs stored in Node.Content and writes it
to Node.PlainText, then rebuilds the FTS5 index.

Run AFTER fts_migrate.sql has been applied to MAKEDOC.db.

Usage:
    python fts_backfill.py
    python fts_backfill.py --db "C:\\path\\to\\MAKEDOC.db"
    python fts_backfill.py --force          # re-extract even if PlainText exists

Requirements:
    pip install python-docx
"""

import argparse
import os
import sqlite3
from io import BytesIO

try:
    from docx import Document
    from docx.opc.exceptions import PackageNotFoundError
except ImportError:
    raise SystemExit(
        "python-docx is required.  Install it with:\n"
        "    pip install python-docx"
    )


# ── Config ────────────────────────────────────────────────────────────────────

def default_db_path() -> str:
    """
    Resolves the DB path the same way the C# DatabaseSetupService does:
    reads MAKEDOC_DB env var and appends MAKEDOC.db.
    Falls back to a hard-coded dev path if the env var is not set.
    """
    db_dir = os.environ.get("MAKEDOC_DB")
    if db_dir:
        return os.path.join(db_dir, "MAKEDOC.db")
    # Fallback for direct dev use
    return r"C:\Users\skeye\BOOK2\MAKEDOC\db\MAKEDOC.db"


# ── Text extraction ───────────────────────────────────────────────────────────

def extract_text_from_blob(blob: bytes) -> str:
    """
    Parses a DOCX binary blob and returns its paragraph text, one per line.
    Skips empty paragraphs.  Raises ValueError if the bytes are not a valid DOCX.
    """
    try:
        doc = Document(BytesIO(blob))
    except (PackageNotFoundError, Exception) as exc:
        raise ValueError(f"Not a valid DOCX: {exc}") from exc

    lines = [p.text for p in doc.paragraphs if p.text.strip()]
    return "\n".join(lines)


# ── Main backfill ─────────────────────────────────────────────────────────────

def run_backfill(db_path: str, force: bool = False) -> None:
    if not os.path.exists(db_path):
        raise SystemExit(f"Database not found: {db_path}")

    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row

    # Verify PlainText column exists (migration must have run first)
    cols = {row[1] for row in conn.execute("PRAGMA table_info(Node)")}
    if "PlainText" not in cols:
        conn.close()
        raise SystemExit(
            "PlainText column not found on Node.\n"
            "Run fts_migrate.sql first:\n"
            "    sqlite3 MAKEDOC.db < db\\sql\\fts_migrate.sql"
        )

    where = "Content IS NOT NULL" if force else "Content IS NOT NULL AND PlainText IS NULL"
    rows = conn.execute(
        f"SELECT rowid, NodeID, Content FROM Node WHERE {where}"
    ).fetchall()

    total   = len(rows)
    ok      = 0
    skipped = 0

    print(f"Nodes to process: {total}")
    if total == 0:
        print("Nothing to do — all nodes already have PlainText extracted.")
        print("Use --force to re-extract everything.")
        conn.close()
        return

    # Fetch ALL blobs first, extract text, then apply all UPDATEs in one pass.
    # This avoids holding a read cursor open while also writing, which can
    # confuse SQLite's page cache on databases with large BLOB overflow pages.
    extractions: list[tuple[str, str, str]] = []   # (node_id, title, plain_text)

    for row in rows:
        node_id = row["NodeID"]
        title   = row["Title"] or ""
        blob    = bytes(row["Content"])
        try:
            text = extract_text_from_blob(blob)
            extractions.append((node_id, title, text))
            print(f"  OK  {node_id}")
            ok += 1
        except ValueError as exc:
            skipped += 1
            print(f"  SKIP {node_id}: {exc}")

    # Apply UPDATEs.  The node_fts_after_update trigger fires on each UPDATE
    # and inserts the new row into node_fts automatically — no manual FTS
    # insert needed here.
    for node_id, title, text in extractions:
        conn.execute(
            "UPDATE Node SET PlainText = ? WHERE NodeID = ?",
            (text, node_id)
        )

    conn.commit()
    print(f"\nExtraction complete: {ok} updated, {skipped} skipped.")
    print("FTS5 index updated via triggers.")

    conn.close()


# ── Entry point ───────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Populate Node.PlainText from DOCX blobs and rebuild the FTS5 index."
    )
    parser.add_argument(
        "--db",
        default=default_db_path(),
        help="Path to MAKEDOC.db  (default: resolved from MAKEDOC_DB env var)"
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Re-extract text even for nodes that already have PlainText set"
    )
    args = parser.parse_args()

    print(f"Database: {args.db}")
    run_backfill(args.db, force=args.force)


if __name__ == "__main__":
    main()
