#!/usr/bin/env python3
"""
fix_word_tags.py  —  Merge template {tags} split across Word XML runs.

Word's spell checker fragments {tag} markers into multiple <w:r> runs with
<w:proofErr> elements wedged between them, breaking template substitution.
This script repairs all affected parts of a .docx file in place.

Usage (single file):
    python fix_word_tags.py clause.docx

Usage (directory, all .docx files):
    python fix_word_tags.py --dir path\to\clauses
    python fix_word_tags.py --dir path\to\clauses --recursive

PowerShell one-liner:
    python fix_word_tags.py --dir ".\docs" --recursive --no-backup

Exit codes: 0 = success, 1 = error
"""

import argparse
import os
import re
import shutil
import sys
import zipfile


# Word XML parts that can contain body text (and therefore template tags)
WORD_TEXT_PARTS = re.compile(
    r'^word/(document|header\d*|footer\d*|footnotes|endnotes|comments)\.xml$'
)


def fix_xml(xml_bytes: bytes) -> tuple[bytes, int]:
    """
    Remove proofErr elements and merge {tag} runs split across multiple <w:r>s.

    Word produces this pattern when the spell checker doesn't recognise a tag:

        <w:r><w:t>{</w:t></w:r>
        <w:proofErr w:type="spellStart"/>
        <w:r><w:t>lineitem</w:t></w:r>
        <w:proofErr w:type="spellEnd"/>
        <w:r><w:t>}</w:t></w:r>

    After the fix, those five elements become one clean run:

        <w:r><w:t>{lineitem}</w:t></w:r>

    The <w:rPr> block (font, size, colour, etc.) from the opening run is
    preserved in the merged output so formatting is not lost.

    Returns (fixed_bytes, number_of_merges_performed).
    """
    text = xml_bytes.decode('utf-8')

    # Step 1 — strip all proofErr elements (they only encode spell-check state)
    text = re.sub(r'<w:proofErr[^/]*/>', '', text)

    # Step 2 — merge runs whose combined text forms {identifier}.
    #
    # A "run" in OOXML is:  <w:r [attrs]> <w:rPr>…</w:rPr>? <w:t [attrs]>TEXT</w:t> </w:r>
    #
    # We match:
    #   • An opening run whose <w:t> text is exactly "{"
    #   • One or more middle runs whose <w:t> text contains only tag-name chars
    #     (letters, digits, underscore, hyphen, dot — no braces or angle brackets)
    #   • A closing run whose <w:t> text is exactly "}"
    #
    # The rPr from the opening run is kept; rPrs from middle/closing runs are discarded.

    RUN_OPEN  = r'<w:r(?:\s[^>]*)?>(<w:rPr>.*?</w:rPr>)?<w:t(?:[^>]*)>\{</w:t></w:r>'
    RUN_WORD  = r'<w:r(?:\s[^>]*)?>(?:<w:rPr>.*?</w:rPr>)?<w:t(?:[^>]*)>([\w\-\.]+)</w:t></w:r>'
    RUN_CLOSE = r'<w:r(?:\s[^>]*)?>(?:<w:rPr>.*?</w:rPr>)?<w:t(?:[^>]*)>}</w:t></w:r>'

    pattern = RUN_OPEN + r'(' + RUN_WORD + r')+' + RUN_CLOSE

    fixes = [0]

    def _merge(m: re.Match) -> str:
        fixes[0] += 1
        rpr = m.group(1) or ''   # <w:rPr>…</w:rPr> from the opening run, or ''

        # Collect all <w:t> text values from the entire match.
        # They will be: ['{', 'part1', 'part2', ..., '}']
        # (w:rPr blocks never contain w:t, so this is safe)
        all_texts = re.findall(r'<w:t(?:[^>]*)>(.*?)</w:t>', m.group(0), re.DOTALL)

        # Drop the leading '{' and trailing '}' — join the rest
        tag_name = ''.join(all_texts[1:-1])

        if rpr:
            return f'<w:r>{rpr}<w:t>{{{tag_name}}}</w:t></w:r>'
        else:
            return f'<w:r><w:t>{{{tag_name}}}</w:t></w:r>'

    text = re.sub(pattern, _merge, text, flags=re.DOTALL)

    return text.encode('utf-8'), fixes[0]


def fix_docx(path: str, backup: bool = True) -> int:
    """
    Fix split {tag} runs in a single .docx file.

    The file is modified in place. Returns the total number of tags merged.
    Raises on I/O or zip errors.
    """
    if backup:
        shutil.copy2(path, path + '.bak')

    with zipfile.ZipFile(path, 'r') as zin:
        names = zin.namelist()
        contents = {n: zin.read(n) for n in names}

    total = 0
    for name in names:
        if WORD_TEXT_PARTS.match(name):
            fixed_bytes, n = fix_xml(contents[name])
            if n:
                contents[name] = fixed_bytes
                total += n
                print(f'  [{n} fix(es)] {name}')

    if total:
        tmp = path + '.tmp'
        try:
            with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as zout:
                for name, data in contents.items():
                    zout.writestr(name, data)
            os.replace(tmp, path)
        except Exception:
            if os.path.exists(tmp):
                os.remove(tmp)
            raise

    return total


def collect_docx(root: str, recursive: bool) -> list[str]:
    paths = []
    if recursive:
        for dirpath, _, filenames in os.walk(root):
            for f in filenames:
                if f.lower().endswith('.docx'):
                    paths.append(os.path.join(dirpath, f))
    else:
        for f in os.listdir(root):
            if f.lower().endswith('.docx'):
                paths.append(os.path.join(root, f))
    return sorted(paths)


def main() -> int:
    parser = argparse.ArgumentParser(
        description='Merge {tag} template markers split across Word XML runs.'
    )
    parser.add_argument('files', nargs='*', metavar='FILE',
                        help='.docx file(s) to fix')
    parser.add_argument('--dir', metavar='DIR',
                        help='Directory of .docx files to fix')
    parser.add_argument('--recursive', '-r', action='store_true',
                        help='Recurse into subdirectories (with --dir)')
    parser.add_argument('--no-backup', action='store_true',
                        help='Skip creating .docx.bak backup files')
    args = parser.parse_args()

    if args.dir and args.files:
        print('Error: specify either FILE arguments or --dir, not both.')
        return 1

    if args.dir:
        targets = collect_docx(args.dir, args.recursive)
        if not targets:
            print(f'No .docx files found in {args.dir}')
            return 0
    elif args.files:
        targets = args.files
    else:
        parser.print_help()
        return 1

    backup = not args.no_backup
    grand_total = 0
    errors = 0

    for path in targets:
        print(f'{path}')
        try:
            n = fix_docx(path, backup=backup)
            if n == 0:
                print('  (no split tags found)')
            else:
                grand_total += n
        except Exception as e:
            print(f'  ERROR: {e}')
            errors += 1

    print(f'\nDone — {grand_total} tag(s) merged across {len(targets)} file(s).')
    if errors:
        print(f'{errors} file(s) had errors.')
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())