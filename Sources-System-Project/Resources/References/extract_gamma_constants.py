import pypdf, sys, re, json, os, subprocess

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

NUC_RE = re.compile(rf'\b([0-9]{{1,3}}m?[1-2]?(?:{elem_pat})m?[1-2]?)\b')
HL_RE = re.compile(r'\b([0-9]+(?:\.[0-9]+)?(?:[eE\+\-][0-9]+)?\s*[ydhms]|[\d\.]+\+[0-9]+[ydhms])\b', re.IGNORECASE)
GAMMA_RE = re.compile(r'([0-9]+\.[0-9]{2,4}[\-][0-9]{1,2}|[0-9]{4}[\-][0-9]{1,2})')

# Verified manual corrections for known OCR split/character anomalies in ORNL/RSIC-45/R1 Table 2
KNOWN_CORRECTIONS = {
    ('133Ba', 44): {
        'half_life': '10.5y',
        'raw_gamma': '1.231-4',
        'fmt_gamma': '1.2310E-04',
        'val_gamma': 1.231e-4,
        't95': 0.715,
        'mu': 4.188
    },
    ('133mBa', 44): {
        'half_life': '1.6d',
        'raw_gamma': '3.372-5',
        'fmt_gamma': '3.3720E-05',
        'val_gamma': 3.372e-5,
        't95': 0.341,
        'mu': 8.792
    },
    ('125Sb', 31): {
        'half_life': '2.8y',
        'raw_gamma': '1.025-4',
        'fmt_gamma': '1.0250E-04',
        'val_gamma': 1.025e-4,
        't95': 1.696,
        'mu': 1.767
    },
    ('241Am', 74): {
        'half_life': '432.2y',
        'raw_gamma': '8.479-5',
        'fmt_gamma': '8.4790E-05',
        'val_gamma': 8.479e-5,
        't95': 0.011,
        'mu': 260.7
    },
    ('91Sr', 18): {
        'half_life': '9.5h',
        'raw_gamma': '1.116-4',
        'fmt_gamma': '1.1160E-04',
        'val_gamma': 1.116e-4,
        't95': 3.465,
        'mu': 0.865
    },
    ('91Mo', 23): {
        'half_life': '15.5m',
        'raw_gamma': '1.870-4',
        'fmt_gamma': '1.8700E-04',
        'val_gamma': 1.870e-4,
        't95': 1.804,
        'mu': 1.661
    },
    ('230Th', 67): {
        'half_life': '7.7+4y',
        'raw_gamma': '1.861-5',
        'fmt_gamma': '1.8610E-05',
        'val_gamma': 1.861e-5,
        't95': 0.004,
        'mu': 815.2
    },
    ('238U', 71): {
        'half_life': '4.47+9y',
        'raw_gamma': '1.763-5',
        'fmt_gamma': '1.7630E-05',
        'val_gamma': 1.763e-5,
        't95': 0.004,
        'mu': 722.5
    },
    ('200Tl', 61): {
        'half_life': '1.1d',
        'raw_gamma': '2.249-4',
        'fmt_gamma': '2.2490E-04',
        'val_gamma': 2.249e-4,
        't95': 3.380,
        'mu': 0.886
    },
    ('201Tl', 61): {
        'half_life': '3.0d',
        'raw_gamma': '2.372-5',
        'fmt_gamma': '2.3720E-05',
        'val_gamma': 2.372e-5,
        't95': 0.114,
        'mu': 26.211
    },
    ('202Tl', 62): {
        'half_life': '12.2d',
        'raw_gamma': '9.437-5',
        'fmt_gamma': '9.4370E-05',
        'val_gamma': 9.437e-5,
        't95': 1.319,
        'mu': 2.272
    },
    ('204Tl', 62): {
        'half_life': '3.8y',
        'raw_gamma': '3.014-7',
        'fmt_gamma': '3.0140E-07',
        'val_gamma': 3.014e-7,
        't95': 0.100,
        'mu': 30.056
    },
    ('207Tl', 62): {
        'half_life': '4.8m',
        'raw_gamma': '3.523-7',
        'fmt_gamma': '3.5230E-07',
        'val_gamma': 3.523e-7,
        't95': 3.442,
        'mu': 0.870
    },
    ('208Tl', 62): {
        'half_life': '3.1m',
        'raw_gamma': '4.497-4',
        'fmt_gamma': '4.4970E-04',
        'val_gamma': 4.497e-4,
        't95': 5.397,
        'mu': 0.555
    },
    ('209Tl', 62): {
        'half_life': '2.2m',
        'raw_gamma': '3.482-4',
        'fmt_gamma': '3.4820E-04',
        'val_gamma': 3.482e-4,
        't95': 4.248,
        'mu': 0.705
    },
    ('210Tl', 62): {
        'half_life': '1.3m',
        'raw_gamma': '4.575-4',
        'fmt_gamma': '4.5750E-04',
        'val_gamma': 4.575e-4,
        't95': 4.057,
        'mu': 0.738
    },
    ('249Cm', 75): {
        'half_life': '1.1h',
        'raw_gamma': '3.982-6',
        'fmt_gamma': '3.9820E-06',
        'val_gamma': 3.982e-6,
        't95': 2.02,
        'mu': None
    },
    ('248Cf', 75): {
        'half_life': '333.5d',
        'raw_gamma': '1.229-5',
        'fmt_gamma': '1.2290E-05',
        'val_gamma': 1.229e-5,
        't95': 0.002,
        'mu': 1271.0
    },
    ('246Cm', 75): {
        'half_life': '4750.0y',
        'raw_gamma': '1.551-5',
        'fmt_gamma': '1.5510E-05',
        'val_gamma': 1.551e-5,
        't95': 0.003,
        'mu': 1054.0
    },
    ('134mCs', 42): {
        'half_life': '2.9h',
        'raw_gamma': '9.040-5',
        'fmt_gamma': '9.0400E-05',
        'val_gamma': 9.040e-5,
        't95': 0.039,
        'mu': 75.989
    },
    ('159Gd', 51): {
        'half_life': '18.6h',
        'raw_gamma': '1.059-5',
        'fmt_gamma': '1.0590E-05',
        'val_gamma': 1.059e-5,
        't95': 0.827,
        'mu': 3.622
    },
    ('169Er', 53): {
        'half_life': '9.4d',
        'raw_gamma': '3.406-10',
        'fmt_gamma': '3.4060E-10',
        'val_gamma': 3.406e-10,
        't95': 0.065,
        'mu': 46.160
    },
    ('197mPt', 59): {
        'half_life': '1.6h',
        'raw_gamma': '1.931-5',
        'fmt_gamma': '1.9310E-05',
        'val_gamma': 1.931e-5,
        't95': 0.654,
        'mu': 4.581
    }
}

def normalize_text_line(raw_line):
    line = raw_line.strip()
    if not line:
        return ""
    
    # 1. Normalize Thallium OCR errors (T1 or TI attached to mass numbers like 200T1, 201TI -> 200Tl, 201Tl)
    line = re.sub(r'\b([0-9]{1,3}m?[1-2]?)T[1I]\b', r'\g<1>Tl', line)
    
    # 2. Normalize leading exclamation mark in mass numbers (e.g. !09Cd -> 109Cd, !29Te -> 129Te, !34mCs -> 134mCs, !33mXe -> 133mXe, !97mHg -> 197mHg)
    line = re.sub(r'!([0-9]{2,3}m?[1-2]?[a-zA-Z])', r'1\g<1>', line)
    line = line.replace('!29Te', '129Te').replace('!09Cd', '109Cd').replace('!33mXe', '133mXe')
    
    # 3. Replace obvious OCR characters in digit context (safeguard Ir and In)
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
    line = line.replace('3 Od', '3.0d').replace('3 0d', '3.0d')
    line = line.replace('I.Id', '1.1d').replace('l.ld', '1.1d').replace('l.lh', '1.1h')
    
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
    line = line.replace('2.249-1', '2.249-4').replace('2.249^1', '2.249-4')
    line = line.replace('9.437- •5', '9.437-5').replace('9.437- 5', '9.437-5')
    line = line.replace('8.479- 5', '8.479-5').replace('8.479- •5', '8.479-5')
    line = line.replace('2.142- 5', '2.142-5').replace('4 950- •5', '4.950-5').replace('4.950- 5', '4.950-5')
    line = line.replace('1861-5', '1.861-5')
    
    return line

def clean_nuclide_name(sym):
    sym = sym.strip()
    sym = re.sub(r'^[^\w0-9]+', '', sym)
    sym = re.sub(r'[^\w0-9]+$', '', sym)
    # T1 / TI to Tl
    sym = re.sub(r'^([0-9]{1,3}m?[1-2]?)T[1I]$', r'\g<1>Tl', sym)
    if sym.startswith(('5I', '7I', '9I', '1I', '13I')) and not sym.startswith(('1In', '1Ir')):
        sym = sym.replace('I', '1', 1)
    if sym.endswith('C1'): sym = sym[:-2] + 'Cl'
    if sym.endswith('N;'): sym = sym[:-2] + 'Ni'
    if sym == 'lie': sym = '11C'
    if sym in ('150;', '150'): sym = '15O'
    if sym == '09Cd': sym = '109Cd'
    if sym == '29Te': sym = '129Te'
    if sym == '33mXe': sym = '133mXe'
    if sym == '97mHg': sym = '197mHg'
    if sym == '95mPt': sym = '195mPt'
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

def run_extraction():
    reader = pypdf.PdfReader(PDF_PATH)
    extracted = []
    issues = []
    
    for page_idx in range(5, 76):
        page_num = page_idx + 1
        page = reader.pages[page_idx]
        
        layout = page.extract_text(extraction_mode="layout")
        lines = layout.split('\n')
        
        # RESTORE THE ORIGINAL FIXED COLUMN SPLIT AT 75
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
                        cur_l = n_m_nxt.group(1) + " " + cur_l
                        norm_lines[k+1] = ""
                    elif n_m_cur and not hl_m_cur and hl_m_nxt:
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
                        
                # Extract individual photon lines (Energy in keV, Probability)
                photons = []
                for bl in block_lines:
                    for match in re.finditer(r'([0-9]+(?:\.[0-9]+)?)\s+([0-9]+\.[0-9]{3,4})', bl):
                        if match.start() and bl[match.start() - 1] == '-':
                            continue
                        e_str, p_str = match.groups()
                        try:
                            e_f = float(e_str)
                            p_f = float(p_str)
                            if 1.0 <= e_f <= 15000.0 and 0.0 <= p_f <= 2.5:
                                photons.append({"energy_kev": e_f, "probability": p_f})
                        except:
                            pass

                # Check known overrides first
                key = (nuclide_sym, page_num)
                if key in KNOWN_CORRECTIONS:
                    ov = KNOWN_CORRECTIONS[key]
                    record = {
                        "nuclide_symbol": nuclide_sym,
                        "half_life": ov['half_life'],
                        "specific_gamma_constant_raw": ov['raw_gamma'],
                        "specific_gamma_constant_formatted": ov['fmt_gamma'],
                        "specific_gamma_constant_value": ov['val_gamma'],
                        "unit": "(mSv/h)/MBq at 1 meter",
                        "lead_thickness_95_percent_cm": ov['t95'],
                        "linear_attenuation_coeff_cm_minus_1": ov['mu'],
                        "photon_emissions_count": len(photons),
                        "page_number": page_num,
                        "notes": special_note,
                        "source_reference": "ORNL/RSIC-45/R1 (Table 2, May 1982)",
                        "raw_lines": block_lines
                    }
                    extracted.append(record)
                    continue

                block_text = ' '.join(block_lines)
                
                # Search for specific gamma constant Γ
                norm_block = re.sub(r'([0-9]+\.[0-9]+)\s*[\-]\s*([0-9]+)', r'\1-\2', block_text)
                g_m = GAMMA_RE.search(norm_block)
                
                if g_m:
                    raw_gamma = g_m.group(1)
                    orig_g, fmt_g, val_g = parse_gamma_val(raw_gamma)
                    
                    # Reject suspicious leading zero formats or merged column prefixes (e.g. 0791.xxxx or 0xxx)
                    if re.match(r'^0[0-9]+', raw_gamma) or re.match(r'^[0-9]{2,}\.[0-9]+[\-]', raw_gamma):
                        issues.append({
                            "page_number": page_num,
                            "column": col_idx + 1,
                            "nuclide_symbol": nuclide_sym,
                            "half_life": hl_val,
                            "raw_lines": block_lines,
                            "extracted_gamma_raw": raw_gamma,
                            "extracted_gamma_value": val_g,
                            "reason": "قيمة مشبوهة تبدأ بصفر بادئ أو أرقام مدمجة من أعمدة مجاورة - تتطلب مراجعة يدوية"
                        })
                        continue

                    # Strict Physical Sanity Check: 1e-12 <= val_g <= 1e-2
                    if val_g is None or val_g < 1.0e-12 or val_g > 1.0e-2:
                        issues.append({
                            "page_number": page_num,
                            "column": col_idx + 1,
                            "nuclide_symbol": nuclide_sym,
                            "half_life": hl_val,
                            "raw_lines": block_lines,
                            "extracted_gamma_raw": raw_gamma,
                            "extracted_gamma_value": val_g,
                            "reason": "قيمة خارج النطاق الفيزيائي المنطقي - يُشتبه بانكسار سطر"
                        })
                        continue

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
        
    print(f"Extraction Completed Successfully:")
    print(f"  Successfully extracted nuclides: {len(dedup)}")
    print(f"  Cases routed to issues file: {len(issues)}")

if __name__ == '__main__':
    run_extraction()
