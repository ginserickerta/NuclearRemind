# Map the C90 PUA codepoints (U+F700-F71A, used by ThaiTextCare's ThaiFontAdjuster) onto
# the Thai positional variants these Cadson Demak fonts ALREADY contain (.small = raised
# tone / lowered under-vowel, .narrow = left-shifted for ascender bases). Unity's legacy
# Text ignores the OpenType GSUB features that normally select those variants, so we make
# them reachable through plain cmap lookups instead:
#
#   default 0E48-4C  -> .small   (raised — C90 assumes the default tone clears upper vowels)
#   F70A-0E top.low  -> original (low — used when no upper vowel follows the base)
#   F705-09 lowleft  -> .narrow  (low + left for ascender bases ป ฝ ฟ ฬ)
#   F713-17 top.left -> synthesized composite: .small shifted left by the .narrow delta
#   F701-04/F710-12  -> .narrow upper vowels
#   F718-1A          -> .small under vowels (dropped below descender bases ฎ ฏ)
#   F700/F70F        -> plain ฐ/ญ (no descless variant exists; same look as today)
#
# Also renames the family (+" NRM") — OFL forbids shipping modified fonts under the
# Reserved Font Name.
import sys, os, glob
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

TONES = [0x0E48, 0x0E49, 0x0E4A, 0x0E4B, 0x0E4C]


def set_cmap(font, cp, glyph_name):
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[cp] = glyph_name


def add_left_high(font, cp, small, narrow):
    """Composite: the raised .small tone shifted left by the same delta as .narrow."""
    glyf, hmtx = font["glyf"], font["hmtx"]
    dx = glyf[narrow].xMin - glyf[font.getBestCmap()[cp]].xMin
    name = f"uni{cp:04X}.leftHigh"
    if name in glyf.glyphs:
        return name
    comp = GlyphComponent()
    comp.glyphName = small
    comp.x, comp.y = int(dx), 0
    comp.flags = 0x0004 | 0x0002  # ROUND_XY_TO_GRID | ARGS_ARE_XY_VALUES
    g = Glyph()
    g.numberOfContours = -1
    g.components = [comp]
    glyf[name] = g  # fontTools appends to the shared glyph order itself
    aw, _ = hmtx[small]
    hmtx[name] = (aw, glyf[small].xMin + int(dx))
    return name


def rename_family(font, suffix=" NRM"):
    for rec in font["name"].names:
        if rec.nameID not in (1, 3, 4, 6, 16):
            continue
        s = rec.toUnicode()
        if suffix.strip() in s:
            continue
        if rec.nameID == 6:  # PostScript name: no spaces allowed
            parts = s.split("-", 1)
            s = parts[0] + suffix.replace(" ", "") + ("-" + parts[1] if len(parts) > 1 else "")
        else:
            s = s + suffix
        rec.string = s


def patch(path):
    font = TTFont(path)
    cmap = font.getBestCmap()
    if 0x0E48 not in cmap:
        print(f"  SKIP (no Thai): {os.path.basename(path)}")
        return
    if 0xF701 in cmap:
        print(f"  SKIP (already patched): {os.path.basename(path)}")
        return
    glyphs = set(font.getGlyphOrder())

    def variant(cp, suffix):
        n = cmap[cp] + suffix
        return n if n in glyphs else None

    missing = []

    # tones: F70A-0E = original low · F705-09 = .narrow · F713-17 = leftHigh · default -> .small
    for i, cp in enumerate(TONES):
        orig = cmap[cp]
        small, narrow = variant(cp, ".small"), variant(cp, ".narrow")
        set_cmap(font, 0xF70A + i, orig)
        set_cmap(font, 0xF705 + i, narrow or orig)
        if small and narrow:
            set_cmap(font, 0xF713 + i, add_left_high(font, cp, small, narrow))
        else:
            set_cmap(font, 0xF713 + i, small or orig)
            missing.append(f"{cp:04X}")
        if small:
            set_cmap(font, cp, small)  # raised becomes the default, as C90 expects

    # upper vowels + mai han akat / nikhahit / mai tai khu -> .narrow
    for cp, pua in [(0x0E34, 0xF701), (0x0E35, 0xF702), (0x0E36, 0xF703), (0x0E37, 0xF704),
                    (0x0E31, 0xF710), (0x0E4D, 0xF711), (0x0E47, 0xF712)]:
        set_cmap(font, pua, variant(cp, ".narrow") or cmap[cp])

    # under vowels -> .small (dropped below descenders)
    for i, cp in enumerate([0x0E38, 0x0E39, 0x0E3A]):
        set_cmap(font, 0xF718 + i, variant(cp, ".small") or cmap[cp])

    # descless ฐ / ญ — no such variants in these fonts; alias to the normal glyphs
    set_cmap(font, 0xF700, cmap[0x0E10])
    set_cmap(font, 0xF70F, cmap[0x0E0D])

    rename_family(font)
    font.save(path)
    note = f" (no .small/.narrow for {','.join(missing)})" if missing else ""
    print(f"  PATCHED: {os.path.basename(path)}{note}")


if __name__ == "__main__":
    for p in sorted(glob.glob(os.path.join(sys.argv[1], "*.ttf"))):
        patch(p)
