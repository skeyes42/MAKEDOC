"""
clause_search.py
────────────────
Personal CLI tool for searching and inspecting MAKEDOC clauses via FTS5.

SEARCH MODE  — find clauses matching a phrase:
    python clause_search.py "delivery schedule"
    python clause_search.py "liquidated damages" --limit 20
    python clause_search.py "inspection" --type Clause
    python clause_search.py "scope of work" --doctype DOC-001

VIEW MODE  — read the full text of a single node:
    python clause_search.py --node NL-0042

OPTIONS:
    --db PATH       Path to MAKEDOC.db (default: MAKEDOC_DB env var + MAKEDOC.db)
    --limit N       Max results to show (default: 15)
    --type TYPE     Filter by NodeType: Clause | Document | HeaderNode
    --doctype ID    Scope to a specific DocTypeID
    --node ID       View full text of a single node (skips phrase search)
    --no-color      Disable ANSI color output

Requirements:
    No extra packages — uses only the Python standard library.
"""

import argparse
import os
import re
import sqlite3
import textwrap


# ── ANSI colors ───────────────────────────────────────────────────────────────

RESET  = "\033[0m"
BOLD   = "\033[1m"
YELLOW = "\033[33m"
CYAN   = "\033[36m"
GREEN  = "\033[32m"
DIM    = "\033[2m"

USE_COLOR = True   # toggled by --no-color


def c(code: str, text: str) -> str:
    return f"{code}{text}{RESET}" if USE_COLOR else text


# ── DB path ───────────────────────────────────────────────────────────────────

def default_db_path() -> str:
    db_dir = os.environ.get("MAKEDOC_DB")
    if db_dir:
        return os.path.join(db_dir, "MAKEDOC.db")
    return r"C:\Users\skeye\BOOK2\MAKEDOC\db\MAKEDOC.db"


def open_db(db_path: str) -> sqlite3.Connection:
    if not os.path.exists(db_path):
        raise SystemExit(f"Database not found: {db_path}")
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


# ── Snippet helper ────────────────────────────────────────────────────────────

def make_snippet(text: str, phrase: str, width: int = 120) -> str:
    """
    Returns a short excerpt from *text* centred on the first occurrence of
    *phrase* (case-insensitive).  The matching words are wrapped in brackets
    when color is disabled, or highlighted in yellow when enabled.
    """
    if not text:
        return ""

    # Find first hit (case-insensitive)
    pattern = re.compile(re.escape(phrase), re.IGNORECASE)
    match   = pattern.search(text)

    if match:
        start  = max(0, match.start() - width // 3)
        end    = min(len(text), match.end() + width * 2 // 3)
        excerpt = ("…" if start > 0 else "") + text[start:end] + ("…" if end < len(text) else "")
    else:
        excerpt = text[:width] + ("…" if len(text) > width else "")

    # Highlight matches in the excerpt
    def highlight(m: re.Match) -> str:
        return c(YELLOW, m.group(0)) if USE_COLOR else f"[{m.group(0)}]"

    return pattern.sub(highlight, excerpt)


# ── DocType lookup (for a node) ───────────────────────────────────────────────

def get_doctypes_for_node(conn: sqlite3.Connection, node_id: str) -> list[str]:
    """
    Returns the DocTypeIDs that reference this node via NodeHierarchy
    (either as parent or child).
    """
    rows = conn.execute(
        """
        SELECT DISTINCT DocTypeID FROM NodeHierarchy
        WHERE ParentNodeID = ? OR ChildNodeID = ?
        ORDER BY DocTypeID
        """,
        (node_id, node_id)
    ).fetchall()
    return [r["DocTypeID"] for r in rows]


# ── SEARCH MODE ───────────────────────────────────────────────────────────────

def cmd_search(conn: sqlite3.Connection, phrase: str, args: argparse.Namespace) -> None:
    # Build the FTS MATCH expression.
    # Wrap multi-word phrases in double-quotes for exact-phrase matching.
    words  = phrase.strip().split()
    match_expr = f'"{phrase}"' if len(words) > 1 else phrase

    # Base query joining FTS results back to Node
    sql = """
        SELECT
            n.NodeID,
            n.NodeType,
            n.Title,
            n.PlainText,
            n.IsUserClause,
            nf.rank
        FROM node_fts nf
        JOIN Node n ON nf.NodeID = n.NodeID
        WHERE node_fts MATCH ?
    """
    params: list = [match_expr]

    if args.type:
        sql += " AND n.NodeType = ?"
        params.append(args.type)

    if args.doctype:
        sql += """
            AND n.NodeID IN (
                SELECT DISTINCT ChildNodeID FROM NodeHierarchy WHERE DocTypeID = ?
                UNION
                SELECT DISTINCT ParentNodeID FROM NodeHierarchy WHERE DocTypeID = ?
            )
        """
        params += [args.doctype, args.doctype]

    sql += " ORDER BY nf.rank LIMIT ?"
    params.append(args.limit)

    try:
        rows = conn.execute(sql, params).fetchall()
    except sqlite3.OperationalError as exc:
        raise SystemExit(
            f"Search failed: {exc}\n"
            "Make sure fts_migrate.sql has been applied and fts_backfill.py has been run."
        )


    if not rows:
        print(f"No results for: {phrase!r}")
        return

    print(c(BOLD, f"\n{len(rows)} result(s) for: {phrase!r}\n"))
    print(c(DIM, "─" * 72))

    for row in rows:
        node_id   = row["NodeID"]
        node_type = row["NodeType"]
        title     = row["Title"] or "(no title)"
        is_user   = row["IsUserClause"]
        plain     = row["PlainText"] or ""

        doctypes  = get_doctypes_for_node(conn, node_id)
        dt_str    = ", ".join(doctypes) if doctypes else "—"
        uc_badge  = c(GREEN, " [UC]") if is_user else ""

        print(f"{c(BOLD+CYAN, node_id)}{uc_badge}  {c(DIM, node_type)}")
        print(f"  {c(BOLD, title)}")
        print(f"  {c(DIM, 'DocTypes:')} {dt_str}")
        if plain:
            snippet = make_snippet(plain, phrase)
            print(f"  {snippet}")
        print(c(DIM, "─" * 72))

    print(c(DIM, f"\nTip: view full clause text with:  python clause_search.py --node <NodeID>"))


# ── VIEW MODE ─────────────────────────────────────────────────────────────────

def cmd_view(conn: sqlite3.Connection, node_id: str) -> None:
    row = conn.execute(
        "SELECT * FROM Node WHERE NodeID = ?", (node_id,)
    ).fetchone()

    if not row:
        raise SystemExit(f"Node not found: {node_id}")

    title     = row["Title"] or "(no title)"
    node_type = row["NodeType"]
    is_user   = row["IsUserClause"]
    derived   = row["DerivedFrom"]
    plain     = row["PlainText"]
    has_blob  = row["Content"] is not None

    doctypes  = get_doctypes_for_node(conn, node_id)
    dt_str    = ", ".join(doctypes) if doctypes else "—"

    print()
    print(c(BOLD + CYAN, node_id))
    print(c(BOLD, title))
    print(c(DIM, "─" * 72))
    print(f"  Type       : {node_type}")
    print(f"  User Clause: {'Yes' if is_user else 'No'}")
    print(f"  Derived From: {derived or '—'}")
    print(f"  Has DOCX Blob: {'Yes' if has_blob else 'No'}")
    print(f"  DocTypes   : {dt_str}")
    print(c(DIM, "─" * 72))

    if plain:
        print()
        # Wrap long lines for readability
        for line in plain.splitlines():
            if line.strip():
                print(textwrap.fill(line, width=80, subsequent_indent="  "))
            else:
                print()
    else:
        print(c(DIM, "(No plain text extracted — run fts_backfill.py to populate)"))

    print()


# ── Entry point ───────────────────────────────────────────────────────────────

def main() -> None:
    global USE_COLOR

    parser = argparse.ArgumentParser(
        description="Search MAKEDOC clauses via FTS5, or view a single node."
    )

    parser.add_argument(
        "phrase",
        nargs="?",
        help="Phrase to search for (wrap multi-word phrases in quotes)"
    )
    parser.add_argument(
        "--node",
        metavar="ID",
        help="View full text of a single node instead of searching"
    )
    parser.add_argument(
        "--db",
        default=default_db_path(),
        help="Path to MAKEDOC.db"
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=15,
        help="Max search results (default: 15)"
    )
    parser.add_argument(
        "--type",
        metavar="NODETYPE",
        help="Filter by NodeType: Clause | Document | HeaderNode"
    )
    parser.add_argument(
        "--doctype",
        metavar="ID",
        help="Scope results to a specific DocTypeID"
    )
    parser.add_argument(
        "--no-color",
        action="store_true",
        help="Disable ANSI color output"
    )

    args = parser.parse_args()

    if args.no_color:
        USE_COLOR = False

    if not args.node and not args.phrase:
        parser.print_help()
        raise SystemExit(1)

    conn = open_db(args.db)

    try:
        if args.node:
            cmd_view(conn, args.node)
        else:
            cmd_search(conn, args.phrase, args)
    finally:
        conn.close()


if __name__ == "__main__":
    main()
