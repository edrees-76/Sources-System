import pypdf, sys, re, json, os

sys.stdout.reconfigure(encoding='utf-8')

PDF_PATH = r'd:\Sources-System\Sources-System-Project\Resources\References\14724519.pdf'
OUTPUT_JSON = r'd:\Sources-System\Sources-System-Project\Resources\References\gamma_constants_index.json'
ISSUES_JSON = r'd:\Sources-System\Sources-System-Project\Resources\References\gamma_constants_extraction_issues.json'

ELEMENTS = [
    'H', 'He', 'Li', 'Be', 'B', 'C', 'N', 'O', 'F', 'Ne',
    'Na', 'Mg', 'Al', 'Si', 'P', 'S', 'Cl', 'Ar', 'K', 'Ca',
    'Sc', 'Ti', 'V', 'Cr', 'Mn', 'Fe', 'Co', 'Ni', 'Cu', 'Zn',
    'Ga', 'Ge', 'As', 'Se', 'Br', 'Kr', 'Rb', 'Sr', 'Y', 'Zr',
    'Nb', 'Mo', 'Tc', 'Ru', 'Rh', 'Pd', 'Ag', 'Cd', 'In', 'Sn',
    'Sb', 'Te', 'I', 'Xe', 'Cs', 'Ba', 'La', 'Ce', 'Pr', 'Nd',
    'Pm', 'Sm', 'Eu', 'Gd', 'Tb', 'Dy', 'Ho', 'Er', 'Tm', 'Yb',
    'Lu', 'Hf', 'Ta', 'W', 'Re', 'Os', 'Ir', 'Pt', 'Au', 'Hg',
    'Tl', 'Pb', 'Bi', 'Po', 'At', 'Rn', 'Fr', 'Ra', 'Ac', 'Th',
    'Pa', 'U', 'Np', 'Pu', 'Am', 'Cm', 'Bk', 'Cf', 'Es', 'Fm',
    'Md', 'No', 'Lr'
]
elem_pat = '|'.join(sorted(ELEMENTS, key=lambda x: -len(x)))

# Pattern for nuclide symbol (Case-sensitive for element symbols to prevent 'h', 'm', 's', 'd' confusion):
NUC_RE = re.compile(rf'\b([0-9]{{1,3}}m?[1-2]?(?:{elem_pat})m?[1-2]?)\b')

# Pattern for half-life: e.g. 53.4d, 5.3y, 2.6h, 15.0m, 7.1s, 1.28+9y, 7.2+5y, 312.7d, 1600y
HL_RE = re.compile(r'\b([0-9]+(?:\.[0-9]+)?(?:[eE\+\-][0-9]+)?\s*[ydhms]|[\d\.]+\+[0-9]+[ydhms])\b', re.IGNORECASE)

# Specific Gamma Constant Γ in (mSv/h)/MBq at 1m: scientific notation with negative exponent between -1 and -15
# e.g. 9.292-6, 3.697-4, 7.671-5, 1.017-4, 8.07-12, 1.597-4
GAMMA_RE = re.compile(r'([0-9]+\.[0-9]{2,4}[\-][0-9]{1,2}|[0-9]{4}[\-][0-9]{1,2})')

def normalize_text_line(raw_line):
    line = raw_line.strip()
    if not line:
        return ""
    
    # 1. Replace obvious OCR characters in digit context (safeguard Ir and In)
    line = re.sub(r'\bI([0-9])', r'1\g<1>', line)
    line = re.sub(r'([0-9])I(?![rn]\b|[0-9])', r'\g<1>1', line)
    line = re.sub(r'([0-9]+)\.Id\b', r'\g<1>.1d', line)
    line = re.sub(r'([0-9]+)\.Ih\b', r'\g<1>.1h', line)
    line = re.sub(r'([0-9]+)\.Im\b', r'\g<1>.1m', line)
    line = re.sub(r'([0-9]+)\.Is\b', r'\g<1>.1s', line)
    line = re.sub(r'([0-9]+)\.Iy\b', r'\g<1>.1y', line)
    
    # Replace dashes and dashes in numbers
    line = line.replace('—', '-').replace('–', '-').replace('^', '-')
    
    # Iodine OCR fixes (1231 -> 123I, 1251 -> 125I, 1311 -> 131I, etc. when followed by half-life)
    line = re.sub(r'\b(1[1-3][0-9])1\s+([0-9]+(?:\.[0-9]+)?[ydhms])', r'\g<1>I \g<2>', line)
    
    # Radium & Iridium OCR fixes
    line = line.replace('226 R a', '226Ra').replace('226 Ra', '226Ra').replace('223 Ra', '223Ra')
    line = line.replace('190mlr', '190mIr').replace('190m lr', '190mIr').replace('194mlr', '194mIr')
    
    # Letter 'O' inside half life numbers (e.g. 74 Od -> 74.0d)
    line = line.replace('74 Od', '74.0d').replace('15 Od', '15.0d').replace('25 Od', '25.0d')
    line = line.replace('74 0d', '74.0d').replace('15 0d', '15.0d').replace('25 0d', '25.0d')
    
    # Specific known OCR corruptions from 1982 printout
    line = line.replace('lie,', '11C').replace('lie ', '11C ').replace('150;', '15O').replace('150 ', '15O ')
    line = line.replace('28Ali?,', '28Al').replace('28Ali?', '28Al')
    line = line.replace('38C1', '38Cl')
    line = line.replace('65N;', '65Ni')
    line = line.replace('U/lh', '14.1h').replace('U/1h', '14.1h').replace('14 1h', '14.1h')
    line = line.replace('9 lh', '9.1h').replace('9 1h', '9.1h')
    line = line.replace('11 8d', '11.8d').replace('11 3h', '11.3h')
    line = line.replace('- l,8ti', '1.8h').replace('- 1,8ti', '1.8h').replace(',!.8h', '1.8h')
    line = line.replace('2 6y', '2.6y').replace('2 44+5y', '2.44+5y').replace('2 41+4y', '2.41+4y')
    line = line.replace('2 6+6y', '2.6+6y').replace('3 7+5y', '3.7+5y').replace('7 04+8y', '7.04+8y').replace('l.6+7y', '1.6+7y')
    line = line.replace('2l5Po', '215Po').replace('392O', '215Po')
    line = line.replace('6 Oh', '6.0h').replace('80 3d', '80.3d')
    line = line.replace('138 4d', '138.4d').replace('312 7d', '312.7d')
    
    # Fix space between mass number and element: e.g. "45 Ca" -> "45Ca", "27 Mg" -> "27Mg", "43 K" -> "43K", "31 Si" -> "31Si"
    line = re.sub(rf'\b([0-9]{{1,3}}m?[1-2]?)\s+({elem_pat})\b', r'\g<1>\g<2>', line)
    
    # Fix space between number and half life unit: e.g. "53.4 d" -> "53.4d"
    line = re.sub(r'([0-9]+(?:\.[0-9]+)?(?:[eE\+\-][0-9]+)?)\s+([ydhms])\b', r'\g<1>\g<2>', line)
    
    # Fix space in 0.xxx decimals where dot became a space: e.g. "0 679" -> "0.679", "0 667" -> "0.667"
    line = re.sub(r'\b0\s+([0-9]{2,4})\b', r'0.\g<1>', line)
    
    # Fix commas and symbols in gamma constants: e.g. ",1787-4" -> "0.1787-4", "2<358-4" -> "2.358-4", "2 478-4" -> "2.478-4", "1 877-4" -> "1.877-4"
    line = re.sub(r'([0-9]+)\s+([0-9]{3,4}\-[0-9]{1,2})', r'\g<1>.\g<2>', line)
    line = re.sub(r'([0-9]+)[<,]([0-9]{3,4}\-[0-9]{1,2})', r'\g<1>.\g<2>', line)
    line = re.sub(r',([0-9]{3,4}\-[0-9]{1,2})', r'0.\g<1>', line)
    line = re.sub(r'([0-9]+\.[0-9]+)\s*[\-\^]\s*([0-9]+)', r'\g<1>-\g<2>', line)
    line = line.replace('2.887-^t', '2.887-4').replace('6.258-tf', '6.258-4')
    line = line.replace('3.590^13.517', '3.590-4 3.517').replace('3.590^1', '3.590-4').replace('3.590-13.517', '3.590-4 3.517')
    line = line.replace('1 306-7', '1.306-7').replace('1.448^', '1.448-4')
    line = line.replace('4.57774', '4.577-4')
    line = line.replace('1653-5', '1.653-5')
    line = line.replace('8 145-6', '8.145-6').replace('8145-6', '8.145-6')
    line = line.replace('3 274-6', '3.274-6')
    
    return line

def clean_nuclide_name(sym):
    sym = sym.strip()
    sym = re.sub(r'^[^\w0-9]+', '', sym)
    sym = re.sub(r'[^\w0-9]+$', '', sym)
    if sym.startswith(('5I', '7I', '9I', '1I', '13I')) and not sym.startswith(('1In', '1Ir')):
        sym = sym.replace('I', '1', 1)
    if sym.endswith('C1'): sym = sym[:-2] + 'Cl'
    if sym.endswith('N;'): sym = sym[:-2] + 'Ni'
    if sym == 'lie': sym = '11C'
    if sym in ('150;', '150'): sym = '15O'
    if 'mlr' in sym: sym = sym.replace('mlr', 'mIr')
    if 'mln' in sym: sym = sym.replace('mln', 'mIn')
    if 'lr' in sym and not sym.endswith('Clr'): sym = sym.replace('lr', 'Ir')
    return sym

def clean_half_life_str(hl):
    hl = hl.strip().replace(' ', '').replace('!', '1').replace('l', '1').replace('I', '1').replace(',', '.')
    return hl

def parse_gamma_val(raw_g):
    s = raw_g.replace(' ', '').replace(',', '.')
    if re.match(r'^[0-9]{4}\-[0-9]+$', s):
        s = s[0] + '.' + s[1:]
    m = re.match(r'^([0-9]+\.?[0-9]*)[\-]([0-9]+)$', s)
    if m:
        mant = float(m.group(1))
        exp = int(m.group(2))
        val = mant * (10 ** -exp)
        formatted = f"{mant:.4f}E-{exp:02d}"
        return s, formatted, val
    try:
        val = float(s)
        return s, f"{val:.4e}", val
    except:
        return s, s, None

def run():
    reader = pypdf.PdfReader(PDF_PATH)
    extracted = []
    issues = []
    
    for page_idx in range(5, 76):
        page_num = page_idx + 1
        page = reader.pages[page_idx]
        
        layout = page.extract_text(extraction_mode="layout")
        lines = layout.split('\n')
        
        # Two columns split at character column 75
        col1 = [l[:75] if len(l) > 75 else l for l in lines]
        col2 = [l[75:] if len(l) > 75 else "" for l in lines]
        
        for col_idx, col_lines in enumerate([col1, col2]):
            # First pass: normalize lines
            norm_lines = [normalize_text_line(raw_l) for raw_l in col_lines]
                
            # Second pass: merge orphan headers (Nuclide on line N, Half-life on line N-1 or N+1)
            merged_lines = []
            k = 0
            while k < len(norm_lines):
                cur_l = norm_lines[k]
                if not cur_l:
                    k += 1
                    continue
                    
                n_m_cur = NUC_RE.search(cur_l)
                hl_m_cur = HL_RE.search(cur_l)
                
                # Case A: cur_l has HL + Gamma but no nuclide, and next line has Nuclide at start
                if k + 1 < len(norm_lines):
                    nxt_l = norm_lines[k+1]
                    n_m_nxt = NUC_RE.search(nxt_l)
                    hl_m_nxt = HL_RE.search(nxt_l)
                    if not n_m_cur and hl_m_cur and n_m_nxt and not hl_m_nxt:
                        # e.g. line 28: 15.0h 1368.0 ... line 29: 24Na ...
                        cur_l = n_m_nxt.group(1) + " " + cur_l
                        norm_lines[k+1] = ""
                    elif n_m_cur and not hl_m_cur and hl_m_nxt:
                        # e.g. line N: 27Mg ... line N+1: 9.5m ...
                        cur_l = cur_l + " " + nxt_l
                        norm_lines[k+1] = ""
                        
                merged_lines.append(cur_l)
                k += 1
                
            # Third pass: find all line indices where a nuclide begins
            nuclide_starts = []
            for idx, line in enumerate(merged_lines):
                if not line or any(h in line for h in ['TABLE 2', 'Specific Gamma', 'Units:', 'Nuclide Half-life', 'Nuclide', 'Prob.']) or re.match(r'^[0-9]+$', line):
                    continue
                n_m = NUC_RE.search(line)
                hl_m = HL_RE.search(line)
                if n_m and (hl_m or 'from ' in line):
                    nuclide_starts.append((idx, clean_nuclide_name(n_m.group(1)), clean_half_life_str(hl_m.group(1)) if hl_m else "See note", line))
            
            # Group lines into nuclide blocks
            for s_idx, (start_line_num, nuclide_sym, hl_val, header_line) in enumerate(nuclide_starts):
                end_line_num = nuclide_starts[s_idx + 1][0] if s_idx + 1 < len(nuclide_starts) else len(merged_lines)
                block_lines = [merged_lines[m_idx].strip() for m_idx in range(start_line_num, end_line_num) if merged_lines[m_idx].strip()]
                
                special_note = None
                for bl in block_lines:
                    if 'from ' in bl:
                        special_note = bl[bl.find('from '):].strip()
                        break
                        
                block_text = ' '.join(block_lines)
                
                # Search for specific gamma constant Γ
                norm_block = re.sub(r'([0-9]+\.[0-9]+)\s*[\-]\s*([0-9]+)', r'\1-\2', block_text)
                g_m = GAMMA_RE.search(norm_block)
                
                if g_m:
                    raw_gamma = g_m.group(1)
                    orig_g, fmt_g, val_g = parse_gamma_val(raw_gamma)
                    
                    # Extract T_95% and mu
                    t95 = None
                    mu = None
                    for bl in block_lines:
                        bl_norm = re.sub(r'([0-9]+\.[0-9]+)\s*[\-]\s*([0-9]+)', r'\1-\2', bl)
                        if raw_gamma in bl_norm:
                            after = bl_norm.split(raw_gamma)[1].strip().split()
                            if len(after) >= 1:
                                try: t95 = float(after[0].replace(',', '.'))
                                except: pass
                            if len(after) >= 2:
                                try: mu = float(after[1].replace(',', '.'))
                                except: pass
                                
                    # Extract individual photon lines (Energy in keV, Probability)
                    photons = []
                    for bl in block_lines:
                        pairs = re.findall(r'([0-9]+(?:\.[0-9]+)?)\s+([0-9]+\.[0-9]{3,4})', bl)
                        for e_str, p_str in pairs:
                            try:
                                e_f = float(e_str)
                                p_f = float(p_str)
                                if 1.0 <= e_f <= 15000.0 and 0.0 <= p_f <= 2.5:
                                    photons.append({"energy_kev": e_f, "probability": p_f})
                            except:
                                pass
                                
                    record = {
                        "nuclide_symbol": nuclide_sym,
                        "half_life": hl_val,
                        "specific_gamma_constant_raw": raw_gamma,
                        "specific_gamma_constant_formatted": fmt_g,
                        "specific_gamma_constant_value": val_g,
                        "unit": "(mSv/h)/MBq at 1 meter",
                        "lead_thickness_95_percent_cm": t95,
                        "linear_attenuation_coeff_cm_minus_1": mu,
                        "photon_emissions_count": len(photons),
                        "page_number": page_num,
                        "notes": special_note,
                        "source_reference": "ORNL/RSIC-45/R1 (Table 2, May 1982)",
                        "raw_lines": block_lines
                    }
                    extracted.append(record)
                else:
                    # Ignore pure photon continuation blocks without nuclide symbol
                    if nuclide_sym and len(nuclide_sym) >= 2:
                        issues.append({
                            "page_number": page_num,
                            "column": col_idx + 1,
                            "nuclide_symbol": nuclide_sym,
                            "half_life": hl_val,
                            "raw_lines": block_lines,
                            "reason": "Specific gamma constant Γ could not be extracted with full confidence from block"
                        })

    # Deduplicate extracted by nuclide_symbol and page_number
    dedup = []
    seen = set()
    for e in extracted:
        key = (e['nuclide_symbol'], e['page_number'])
        if key not in seen:
            seen.add(key)
            dedup.append(e)
            
    os.makedirs(os.path.dirname(OUTPUT_JSON), exist_ok=True)
    with open(OUTPUT_JSON, 'w', encoding='utf-8') as f:
        json.dump(dedup, f, indent=2, ensure_ascii=False)
        
    with open(ISSUES_JSON, 'w', encoding='utf-8') as f:
        json.dump(issues, f, indent=2, ensure_ascii=False)
        
    print(f"Extraction Completed:")
    print(f"  Successfully extracted nuclides: {len(dedup)}")
    print(f"  Cases routed to issues file: {len(issues)}")
    print(f"  JSON output file: {OUTPUT_JSON}")
    print(f"  Issues file: {ISSUES_JSON}")

if __name__ == '__main__':
    run()
