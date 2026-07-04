"""
Repairs assembled DOCX files where text-type SDTs contain paragraph elements
instead of inline runs — a structure violation that causes Word to refuse to open.

Usage:
    python repair_docx.py input.docx [output.docx]
    python repair_docx.py folder/     <- repairs all .docx files in-place
"""

import sys, os, zipfile, shutil, re

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"

def find_matching_close(content, open_tag, close_tag, start):
    """Return the index just after the matching close_tag, given depth counting."""
    depth = 0
    i = start
    while i < len(content):
        o = content.find(open_tag, i)
        c = content.find(close_tag, i)
        if c == -1:
            return -1
        if o != -1 and o < c:
            depth += 1
            i = o + len(open_tag)
        else:
            depth -= 1
            end = c + len(close_tag)
            if depth == 0:
                return end
            i = end
    return -1

def fix_sdt_content(xml_bytes):
    content = xml_bytes.decode("utf-8")
    fix_count = 0
    result = []
    i = 0

    while i < len(content):
        sdt_start = content.find('<w:sdt>', i)
        if sdt_start == -1:
            result.append(content[i:])
            break

        result.append(content[i:sdt_start])

        # Find the matching </w:sdt>
        sdt_end = find_matching_close(content, '<w:sdt>', '</w:sdt>', sdt_start)
        if sdt_end == -1:
            result.append(content[sdt_start:])
            break

        sdt_block = content[sdt_start:sdt_end]

        # Is this a text-type SDT?
        is_text = bool(re.search(r'<w:text\s*/?>', sdt_block))

        if is_text:
            sc_match = re.search(r'(<w:sdtContent>)(.*?)(</w:sdtContent>)',
                                  sdt_block, re.DOTALL)
            if sc_match and '<w:p' in sc_match.group(2):
                inner = sc_match.group(2)
                runs = []
                for p_match in re.finditer(r'<w:p(?:\s[^>]*)?>.*?</w:p>', inner, re.DOTALL):
                    p_content = p_match.group(0)
                    # Remove pPr block
                    p_content = re.sub(r'<w:pPr>.*?</w:pPr>', '', p_content, flags=re.DOTALL)
                    # Grab run-level elements
                    run_elements = re.findall(
                        r'<w:r(?:\s[^>]*)?>.*?</w:r>'
                        r'|<w:ins(?:\s[^>]*)?>.*?</w:ins>'
                        r'|<w:bookmarkStart[^/]*/>'
                        r'|<w:bookmarkEnd[^/]*/>',
                        p_content, re.DOTALL)
                    runs.extend(run_elements)

                new_inner = ''.join(runs)
                sdt_block = (sdt_block[:sc_match.start(2)] +
                             new_inner +
                             sdt_block[sc_match.end(2):])
                fix_count += 1

        result.append(sdt_block)
        i = sdt_end

    return ''.join(result).encode('utf-8'), fix_count


def repair_docx(input_path, output_path=None):
    if output_path is None:
        output_path = input_path

    print(f"Repairing: {os.path.basename(input_path)}")
    tmp_path = input_path + ".tmp_repair"

    try:
        with zipfile.ZipFile(input_path, 'r') as zin, \
             zipfile.ZipFile(tmp_path, 'w', zipfile.ZIP_DEFLATED) as zout:

            for item in zin.infolist():
                data = zin.read(item.filename)

                if item.filename == 'word/document.xml':
                    fixed, n = fix_sdt_content(data)
                    if n:
                        print(f"  Fixed {n} text-type SDT(s) with illegal paragraph content")
                    else:
                        print("  No SDT paragraph-wrapping issues detected")
                    data = fixed

                zout.writestr(item, data)

        shutil.move(tmp_path, output_path)
        print(f"  -> Saved: {os.path.basename(output_path)}")
        return True

    except Exception as e:
        import traceback
        print(f"  ERROR: {e}")
        traceback.print_exc()
        if os.path.exists(tmp_path):
            os.remove(tmp_path)
        return False


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    target = sys.argv[1]

    if os.path.isdir(target):
        files = [os.path.join(target, f) for f in os.listdir(target)
                 if f.lower().endswith('.docx') and not f.startswith('~$')]
        print(f"Found {len(files)} DOCX file(s) in folder.")
        ok = sum(1 for f in files if repair_docx(f))
        print(f"\nDone. Repaired {ok}/{len(files)} files.")
    else:
        out = sys.argv[2] if len(sys.argv) > 2 else None
        repair_docx(target, out)


if __name__ == '__main__':
    main()
