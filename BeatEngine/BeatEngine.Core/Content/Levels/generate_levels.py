#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Generador de niveles para el juego de palabras en español
Crea 1000 archivos de niveles siguiendo las reglas:
- Cada nivel tiene un tema y 3 palabras
- No hay sílabas repetidas entre palabras
- No hay sílabas repetidas dentro de la misma palabra
"""

import random
from itertools import combinations

# Base de datos expandida de temas y palabras con sus sílabas
# Cada entrada es una lista de sílabas de una palabra
THEMES_AND_WORDS = {
    "ANIMALES": [
        ["CA", "BA", "LLO"], ["O", "SO"], ["JI", "RA", "FA"],
        ["PE", "RRO"], ["GA", "TO"], ["PA", "JA", "RO"],
        ["LE", "ON"], ["TI", "GRE"], ["E", "LE", "FAN", "TE"],
        ["CE", "BRA"], ["MO", "NO"], ["CA", "MA", "LE", "ON"],
        ["GU", "RA", "CA"], ["A", "GU", "ILA"], ["BU", "HO"],
        ["BU", "FA", "LO"], ["A", "VE", "STRUZ"], ["CO", "NE", "JO"],
        ["LO", "BO"], ["ZA", "RRI", "GU", "EY"], ["HI", "E", "NA"],
        ["CE", "RU", "VO"], ["AL", "CE"], ["CI", "ER", "VO"],
        ["CO", "RZO"], ["PA", "TO"], ["MU", "LA"]
    ],
    "FRUTAS": [
        ["MA", "NZA", "NA"], ["PLA", "TA", "NO"], ["NA", "RA", "NJA"],
        ["FRES", "A"], ["U", "VA"], ["PI", "NA"],
        ["MA", "NGO"], ["PA", "PA", "YA"], ["KI", "WI"],
        ["ME", "LO", "N"], ["SAN", "DIA"], ["HI", "GO"],
        ["CI", "RU", "ELA"], ["LI", "MON"], ["LI", "MA"],
        ["TO", "MA", "TE"], ["A", "GU", "CA", "TE"], ["PI", "TA", "YA"],
        ["CO", "CO"], ["DA", "TIL"], ["HI", "GU", "O"],
        ["CA", "RA", "MBO", "LO"], ["MA", "RA", "CU", "YA"], ["GU", "A", "YA", "BA"],
        ["ZA", "PA", "TE"], ["TA", "MA", "RIN", "DO"], ["CE", "RE", "ZA"]
    ],
    "COLORES": [
        ["RO", "JO"], ["A", "ZUL"], ["VE", "RDE"],
        ["A", "MA", "RI", "LLO"], ["MO", "RA", "DO"], ["RO", "SA"],
        ["NE", "GRO"], ["BLAN", "CO"], ["GRI", "S"],
        ["NA", "RA", "NJA"], ["VI", "O", "LE", "TA"], ["MA", "RÓN"],
        ["BE", "IGE"], ["TUR", "QUE", "SA"], ["CA", "RME", "SI"],
        ["PLA", "TA"], ["DO", "RA", "DO"], ["FU", "CSIA"],
        ["LA", "VEN", "DA"], ["CI", "AN"], ["MA", "GEN", "TA"]
    ],
    "COMIDA": [
        ["PA", "STA"], ["AR", "ROZ"], ["PO", "LLO"],
        ["PA", "NE"], ["QUE", "SO"], ["HA", "MUR", "GUER", "SA"],
        ["PI", "ZA"], ["EN", "SA", "LA", "DA"], ["SO", "PA"],
        ["CA", "RNE"], ["PEZ"], ["VE", "GE", "TA", "LES"],
        ["FRU", "TAS"], ["POST", "RE"], ["HE", "LA", "DO"],
        ["PA", "STEL"], ["BO", "CI", "LLO"], ["EM", "PA", "NA", "DA"],
        ["TAM", "AL"], ["MO", "LE"], ["CE", "VI", "CHE"],
        ["GU", "A", "CA", "MO", "LE"], ["FA", "JO", "LES"], ["LEN", "TE", "JAS"]
    ],
    "DEPORTES": [
        ["FU", "TOL"], ["BA", "SQUET"], ["TE", "NIS"],
        ["NA", "TA", "CION"], ["VO", "LEI", "BOL"], ["BA", "LON", "CES", "TO"],
        ["A", "TLE", "TIS", "MO"], ["CI", "CLIS", "MO"], ["GIM", "NA", "SIA"],
        ["BOX", "EO"], ["ES", "QUI"], ["RE", "MO"],
        ["GOLF"], ["RU", "G", "BI"], ["BE", "IS", "BOL"],
        ["A", "QUA", "TICS"], ["JUDO"], ["KAR", "TE"]
    ],
    "PAISES": [
        ["ES", "PA", "ÑA"], ["ME", "XI", "CO"], ["AR", "GEN", "TI", "NA"],
        ["CO", "LOM", "BIA"], ["CHI", "LE"], ["PE", "RU"],
        ["BRA", "SIL"], ["VE", "NE", "ZU", "E", "LA"], ["CU", "BA"],
        ["FRAN", "CIA"], ["I", "TA", "LIA"], ["ALE", "MA", "NIA"],
        ["POR", "TU", "GAL"], ["GRE", "CIA"], ["HO", "LAN", "DA"],
        ["BE", "LGI", "CA"], ["SUI", "ZA"], ["AU", "STRIA"]
    ],
    "PROFESIONES": [
        ["DOC", "TOR"], ["EN", "FE", "ME", "RO"], ["PRO", "FE", "SOR"],
        ["IN", "GE", "NIE", "RO"], ["AR", "QUI", "TEC", "TO"], ["AB", "O", "GA", "DO"],
        ["CO", "CI", "NE", "RO"], ["PO", "LI", "CIA"], ["BO", "M", "BE", "RO"],
        ["PILO", "TO"], ["PE", "RIO", "DIS", "TA"], ["VEN", "TE", "DO", "R"],
        ["DEN", "TIS", "TA"], ["FAR", "MA", "CEU", "TI", "CO"], ["VET", "E", "RI", "NA", "RIO"],
        ["PE", "RIO", "DIS", "TA"], ["JO", "YER", "O"], ["AR", "QUI", "TEC", "TO"]
    ],
    "TRANSPORTE": [
        ["AU", "TO"], ["A", "VION"], ["TRE", "N"],
        ["BA", "RCO"], ["MO", "TO"], ["BI", "CI", "CLE", "TA"],
        ["HE", "LI", "CO", "P", "TE", "RO"], ["SUB", "MA", "RI", "NO"], ["CA", "MI", "ON"],
        ["BUS"], ["ME", "TRO"], ["TA", "XI"],
        ["YA", "TE"], ["VE", "LO"], ["MO", "NO", "PA", "TIN"]
    ],
    "MUSICA": [
        ["GU", "I", "TAR", "RA"], ["PI", "A", "NO"], ["VI", "O", "LIN"],
        ["TAM", "BOR"], ["FLAU", "TA"], ["SA", "XO", "FON"],
        ["BA", "JO"], ["BA", "TE", "RI", "A"], ["TRO", "MPE", "TA"],
        ["AC", "OR", "DE", "ON"], ["TRO", "MBON"], ["CLAR", "I", "NE", "TE"],
        ["OB", "OE"], ["CEL", "LO"], ["AR", "PA"]
    ],
    "NATURALEZA": [
        ["AR", "BOL"], ["FLOR"], ["RIO"],
        ["MON", "TA", "ÑA"], ["MA", "R"], ["LA", "GO"],
        ["SOL"], ["LU", "NA"], ["ES", "TRE", "LLAS"],
        ["NUBE"], ["LLU", "VIA"], ["AR", "CO", "I", "RIS"],
        ["VO", "L", "CAN"], ["DE", "SIE", "RTO"], ["JUN", "GLA"]
    ],
    "CUERPO": [
        ["CA", "BE", "ZA"], ["BRA", "ZO"], ["PIE"],
        ["MA", "NO"], ["O", "JO"], ["O", "RE", "JA"],
        ["NA", "RIZ"], ["BO", "CA"], ["DI", "EN", "TE"],
        ["CO", "RA", "ZON"], ["PUL", "MON"], ["ES", "TO", "MA", "GO"],
        ["RO", "DI", "LLA"], ["MU", "ÑE", "CA"], ["CO", "DO"]
    ],
    "ROPA": [
        ["CA", "MI", "SA"], ["PAN", "TA", "LON"], ["ZAP", "A", "TO"],
        ["VES", "TI", "DO"], ["CHA", "QUE", "TA"], ["GO", "RRO"],
        ["FAL", "DA"], ["SHORT"], ["BUFAN", "DA"],
        ["SUE", "TER"], ["CHA", "LE", "CO"], ["PAN", "TU", "FLO"]
    ],
    "TECNOLOGIA": [
        ["COM", "PU", "TA", "DO", "RA"], ["TE", "LE", "FO", "NO"], ["TA", "BLE", "TA"],
        ["IN", "TER", "NET"], ["RA", "DIO"], ["TE", "LE", "VI", "SION"],
        ["CA", "MA", "RA"], ["AU", "DI", "FO", "NO"], ["IM", "PRE", "SO", "RA"],
        ["MO", "USE"], ["TEC", "LA", "DO"], ["AL", "TA", "VOZ"]
    ],
    "EDIFICIOS": [
        ["CA", "SA"], ["ES", "CU", "E", "LA"], ["HO", "SPI", "TAL"],
        ["BI", "BLIO", "TE", "CA"], ["MU", "SE", "O"], ["I", "GLE", "SIA"],
        ["OFI", "CI", "NA"], ["RES", "TAU", "RAN", "TE"], ["SU", "PER", "MER", "CA", "DO"],
        ["BAN", "CO"], ["ES", "TA", "CIO", "N"], ["A", "ER", "O", "PUER", "TO"]
    ],
    "TIEMPO": [
        ["LLU", "VIA"], ["SO", "L"], ["NIE", "VE"],
        ["VEN", "TO"], ["TOR", "MEN", "TA"], ["AR", "CO", "I", "RIS"],
        ["NE", "BLA"], ["HI", "ELO"], ["GRA", "NI", "ZO"],
        ["RO", "CIO"], ["CAL", "MA"], ["HU", "RA", "CAN"]
    ],
    "NUMEROS": [
        ["U", "NO"], ["DOS"], ["TRE", "S"],
        ["CUA", "TRO"], ["CIN", "CO"], ["SEIS"],
        ["SIE", "TE"], ["O", "CHO"], ["NUE", "VE"],
        ["DIEZ"], ["CIEN"], ["MIL"]
    ],
    "FAMILIA": [
        ["PA", "DRE"], ["MA", "DRE"], ["HI", "JO"],
        ["HI", "JA"], ["HER", "MA", "NO"], ["HER", "MA", "NA"],
        ["A", "BU", "ELO"], ["A", "BU", "ELA"], ["TI", "O"],
        ["TI", "A"], ["PRI", "MO"], ["SO", "BRI", "NO"],
        ["CUI", "ÑA", "DO"], ["YER", "NO"], ["NU", "E", "RA"]
    ],
    "ESCUELA": [
        ["LI", "BRO"], ["LA", "PIZ"], ["CU", "ADER", "NO"],
        ["PRO", "FE", "SOR"], ["ES", "TU", "DIAN", "TE"], ["AU", "LA"],
        ["PIZ", "AR", "RON"], ["RE", "GLA"], ["MO", "CHI", "LA"],
        ["BO", "RRA", "DOR"], ["ES", "TU", "CHE"], ["LI", "BRO", "TE", "CA"]
    ],
    "HOGAR": [
        ["ME", "SA"], ["SI", "LLA"], ["RE", "FRI", "GE", "RA", "DOR"],
        ["SO", "FA"], ["CA", "MA"], ["VEN", "TA", "NA"],
        ["LA", "MPA", "RA"], ["ES", "PE", "JO"], ["CU", "CHI", "LLA"],
        ["AL", "MO", "HA", "DA"], ["CO", "CI", "NA"], ["CO", "ME", "DO", "R"]
    ],
    "VERBOS": [
        ["CO", "RER"], ["NA", "DAR"], ["SAL", "TAR"],
        ["BA", "ILAR"], ["CAN", "TAR"], ["HA", "BLAR"],
        ["ES", "CU", "CHAR"], ["LE", "ER"], ["ES", "CRI", "BIR"],
        ["JU", "GAR"], ["NA", "DAR"], ["VO", "LAR"],
        ["CO", "MER"], ["BE", "BER"], ["DU", "RAR"]
    ],
    "SUPERMERCADO": [
        ["LAC", "TE", "OS"], ["COM", "PRAR"], ["LIS", "TA"],
        ["VER", "DU", "RAS"], ["PA", "SI", "LLOS"], ["A", "NA", "QUE", "LES"],
        ["LE", "CHE"], ["YOGUR"], ["QUESO"],
        ["PAN"], ["HUE", "VOS"], ["JU", "GOS"],
        ["DE", "TER", "GEN", "TE"], ["JA", "BON"], ["CE", "RE", "AL"]
    ],
    "CINE": [
        ["BU", "TA", "CA"], ["PO", "CHO", "CLOS"], ["AC", "CION"],
        ["CO", "ME", "DIA"], ["DRA", "MA"], ["TE", "ROR"],
        ["AC", "TOR"], ["AC", "TRIZ"], ["DI", "REC", "TOR"],
        ["PE", "LI", "CU", "LA"], ["ES", "CE", "NA"], ["GU", "ION"]
    ],
    "ESPACIO": [
        ["GA", "LA", "XIA"], ["PLA", "NE", "TA"], ["ES", "TRE", "LLA"],
        ["AS", "TRO", "NAU", "TA"], ["CO", "HE", "TE"], ["LU", "NA"],
        ["SO", "L"], ["MAR", "TE"], ["SA", "TEL", "LI", "TE"]
    ]
}

def has_repeated_syllables(word_syllables):
    """Verifica si una palabra tiene sílabas repetidas"""
    return len(word_syllables) != len(set(word_syllables))

def validate_level(words_syllables):
    """
    Valida que un nivel cumpla las reglas:
    - No hay sílabas repetidas entre palabras
    - No hay sílabas repetidas dentro de la misma palabra
    """
    all_syllables = []
    
    # Verificar sílabas repetidas dentro de cada palabra
    for word_syl in words_syllables:
        if has_repeated_syllables(word_syl):
            return False
        all_syllables.extend(word_syl)
    
    # Verificar sílabas repetidas entre palabras
    if len(all_syllables) != len(set(all_syllables)):
        return False
    
    return True

def generate_level_file(file_number, theme, words_syllables):
    """Genera el contenido de un archivo de nivel"""
    content = [theme]
    
    for word_idx, syllables in enumerate(words_syllables, 1):
        for syl_idx, syllable in enumerate(syllables, 1):
            content.append(f"{syllable}({word_idx},{syl_idx})")
        content.append("")  # Línea vacía entre palabras
    
    # Añadir líneas vacías al final para llegar a 14 líneas
    while len(content) < 14:
        content.append("")
    
    return "\n".join(content)

def find_valid_combinations(theme_words):
    """Encuentra todas las combinaciones válidas de 3 palabras para un tema"""
    valid_combos = []
    
    # Probar todas las combinaciones de 3 palabras
    for combo in combinations(theme_words, 3):
        if validate_level(combo):
            valid_combos.append(combo)
    
    return valid_combos

def generate_all_levels():
    """Genera los 1000 archivos de niveles garantizando temas diferentes en niveles consecutivos"""
    all_themes = list(THEMES_AND_WORDS.keys())
    used_combinations = set()
    file_number = 10  # Empezar desde 10.txt
    
    # Pre-calcular todas las combinaciones válidas por tema
    theme_valid_combos = {}
    for theme, words in THEMES_AND_WORDS.items():
        valid_combos = find_valid_combinations(words)
        theme_valid_combos[theme] = valid_combos.copy()  # Hacer copia para poder modificar
        print(f"Tema '{theme}': {len(valid_combos)} combinaciones válidas")
    
    levels_generated = 0
    last_theme = None
    
    while levels_generated < 1000:
        # Seleccionar un tema diferente al anterior
        available_themes = [t for t in all_themes if t != last_theme and len(theme_valid_combos.get(t, [])) > 0]
        
        if not available_themes:
            # Si no hay temas disponibles, permitir repetir pero solo si es necesario
            available_themes = [t for t in all_themes if len(theme_valid_combos.get(t, [])) > 0]
            if not available_themes:
                print(f"\nAdvertencia: No hay más combinaciones disponibles. Generados {levels_generated} niveles.")
                break
        
        # Seleccionar tema aleatorio de los disponibles
        theme = random.choice(available_themes)
        last_theme = theme
        
        valid_combos = theme_valid_combos.get(theme, [])
        if not valid_combos:
            continue
        
        # Mezclar las combinaciones para variedad
        random.shuffle(valid_combos)
        
        # Buscar una combinación no usada
        combo_found = False
        for combo in valid_combos:
            # Verificar que no hayamos usado esta combinación exacta
            combo_key = (theme, tuple(tuple(w) for w in combo))
            if combo_key in used_combinations:
                continue
            
            used_combinations.add(combo_key)
            
            # Generar el archivo
            filename = f"{file_number}.txt"
            filepath = f"c:\\BeatEngine\\BeatEngine\\BeatEngine.Core\\Content\\Levels\\{filename}"
            content = generate_level_file(file_number, theme, combo)
            
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            
            levels_generated += 1
            file_number += 1
            combo_found = True
            
            if levels_generated % 100 == 0:
                print(f"Generados {levels_generated} niveles...")
            
            break
        
        # Si no encontramos combinación, remover el tema de la lista de disponibles
        if not combo_found:
            # Remover la combinación usada de la lista (si existe)
            pass  # Ya se maneja con el set de used_combinations
    
    print(f"\n¡Listo! Se generaron {levels_generated} archivos de niveles.")
    if levels_generated < 1000:
        print(f"Advertencia: Solo se pudieron generar {levels_generated} niveles únicos.")
        print("Considera añadir más palabras a la base de datos para alcanzar 1000 niveles.")

if __name__ == "__main__":
    generate_all_levels()
