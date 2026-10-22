import re, sys, json
src = open(sys.argv[1], encoding='utf-8').read().splitlines()
FORGE_W = {"Forge","Lacerate","Smite","Valiance","Arcanas_Concerto","Acheron","Elysium","Praxis","Dauntless","Ravenous"}
AWE_W = {"Spiral_Carve","Awe_Blast","Health_Vamp","Mana_Vamp","Powerword_Die"}

def library(start_sig):
    i = next(n for n, l in enumerate(src) if start_sig in l)
    depth = 0; out = {}; labels = []; body = {}; started = False
    stmt = ""
    for l in src[i:]:
        depth += l.count('{') - l.count('}')
        if '{' in l: started = True
        s = l.strip()
        m = re.match(r'case "([^"]+)":', s)
        if m:
            if body: labels, body = [], {}
            labels.append(m.group(1)); continue
        if s.startswith('default:'): labels, body = [], {}
        stmt += " " + s
        if s.endswith(';') or s.endswith('{') or s.endswith('}'):
            for var, enum in (("type","EnhancementType"),("c","CapeSpecial"),("w","WeaponSpecial"),("h","HelmSpecial")):
                name = {"type":"type","c":"cSpecial","w":"wSpecial","h":"hSpecial"}[var]
                if re.search(rf'\b{name}\s*=(?!=)', stmt) and var not in body:
                    mm = re.search(rf'{enum}\.(\w+)', stmt)
                    if mm: body[var] = mm.group(1)
            stmt = ""
        if s.startswith('break;') and labels:
            for lab in labels:
                out.setdefault(lab, dict(body))
            labels, body = [], {}
        if started and depth <= 0: break
    return out

forge = library("bool ForgeEnhancementLibrary()")
awe = library("void AweEnhancementLibrary()")
presets = {}
for name in sorted(set(forge) | set(awe)):
    if name.lower() == "placeholder": continue
    f, a = forge.get(name, {}), awe.get(name, {})
    w = [x for x in (f.get("w"), a.get("w")) if x]
    presets[name] = {
        "Base": f.get("type") or a.get("type") or "Lucky",
        "WeaponForge": next((x for x in w if x in FORGE_W), "None"),
        "WeaponAwe": next((x for x in w if x in AWE_W), "None"),
        "Cape": f.get("c", "None"),
        "Helm": f.get("h", "None"),
    }
json.dump(presets, open(sys.argv[2], 'w', encoding='utf-8'), indent=1)
print(len(forge), len(awe), len(presets))
