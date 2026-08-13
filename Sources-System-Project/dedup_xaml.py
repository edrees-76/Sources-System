
import sys
import re

def deduplicate_xaml(file_path):
    print(f"Processing {file_path}...")
    with open(file_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    header = []
    # Find start of keys
    start_idx = 0
    for i, line in enumerate(lines):
        if "<system:String" in line:
            start_idx = i
            break
    
    header = lines[:start_idx]
    
    body_lines = lines[start_idx:]
    # Remove existing closing tag if present
    body_lines = [l for l in body_lines if "</ResourceDictionary>" not in l]

    key_pattern = re.compile(r'x:Key="(.*?)"')
    
    seen_keys = {}
    
    # First pass: find last occurrence of each key
    for i, line in enumerate(body_lines):
        match = key_pattern.search(line)
        if match:
            key = match.group(1)
            seen_keys[key] = i
        
    final_lines = []
    for i, line in enumerate(body_lines):
        match = key_pattern.search(line)
        if match:
            key = match.group(1)
            if seen_keys[key] == i:
                final_lines.append(line)
        else:
            # Keep comments and empty lines
            final_lines.append(line)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(header)
        f.writelines(final_lines)
        if not final_lines[-1].strip().endswith("</ResourceDictionary>"):
            f.write("</ResourceDictionary>\n")
    print(f"Done deduplicating {file_path}")

if __name__ == "__main__":
    deduplicate_xaml(sys.argv[1])
