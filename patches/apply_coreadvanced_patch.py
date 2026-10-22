import sys
p=sys.argv[1]
s=open(p,encoding='utf-8',newline='').read()
old_call="""        // If the item doesnt exist in the forge enh lib, or the player doesn't have the Forge enh unlocked, use Awe enh instead
        if (!ForgeEnhancementLibrary())
            AweEnhancementLibrary();
"""
new_call="""        // A custom profile from the CoreBots Options "Class Enhance" tab replaces the built-in library
        if (!ClassEnhanceProfile(className, ref type, ref cSpecial, ref hSpecial, ref wSpecial))
        {
            // If the item doesnt exist in the forge enh lib, or the player doesn't have the Forge enh unlocked, use Awe enh instead
            if (!ForgeEnhancementLibrary())
                AweEnhancementLibrary();
        }
"""
anchor="    public void SmartEnhance(string? className, bool ForceEnh = false)\n"
method='''    /// <summary>
    /// Resolves a "Base|WeaponForge|WeaponAwe|Cape|Helm" profile (CoreBots Options "Class Enhance" tab).
    /// Uses only the chosen specials: weapon takes the Forge one if unlocked, else the Awe one if unlocked;
    /// any special that isn't unlocked becomes None (base enhancement only).
    /// </summary>
    public bool ResolveEnhanceProfile(string profile, out EnhancementType type, out CapeSpecial cape, out HelmSpecial helm, out WeaponSpecial weapon)
    {
        cape = CapeSpecial.None;
        helm = HelmSpecial.None;
        weapon = WeaponSpecial.None;
        string[] parts = profile.Split('|');
        if (parts.Length != 5 || !Enum.TryParse(parts[0], out type))
        {
            type = EnhancementType.Lucky;
            return false;
        }

        if (Enum.TryParse(parts[1], out WeaponSpecial forge) && WeaponUnlocked(forge))
            weapon = forge;
        else if (Enum.TryParse(parts[2], out WeaponSpecial awe) && awe != WeaponSpecial.None && uAwe())
            weapon = awe;
        if (Enum.TryParse(parts[3], out CapeSpecial c) && CapeUnlocked(c))
            cape = c;
        if (Enum.TryParse(parts[4], out HelmSpecial h) && HelmUnlocked(h))
            helm = h;
        return true;

        bool WeaponUnlocked(WeaponSpecial s) => s switch
        {
            WeaponSpecial.Forge => uForgeWeapon(),
            WeaponSpecial.Lacerate => uLacerate(),
            WeaponSpecial.Smite => uSmite(),
            WeaponSpecial.Valiance => uValiance(),
            WeaponSpecial.Arcanas_Concerto => uArcanasConcerto(),
            WeaponSpecial.Acheron => uAcheron(),
            WeaponSpecial.Elysium => uElysium(),
            WeaponSpecial.Praxis => uPraxis(),
            WeaponSpecial.Dauntless => uDauntless(),
            WeaponSpecial.Ravenous => uRavenous(),
            _ => false
        };

        bool CapeUnlocked(CapeSpecial s) => s switch
        {
            CapeSpecial.Forge => uForgeCape(),
            CapeSpecial.Absolution => uAbsolution(),
            CapeSpecial.Avarice => uAvarice(),
            CapeSpecial.Vainglory => uVainglory(),
            CapeSpecial.Penitence => uPenitence(),
            CapeSpecial.Lament => uLament(),
            _ => false
        };

        bool HelmUnlocked(HelmSpecial s) => s switch
        {
            HelmSpecial.Forge => uForgeHelm(),
            HelmSpecial.Vim => uVim(),
            HelmSpecial.Examen => uExamen(),
            HelmSpecial.Anima => uAnima(),
            HelmSpecial.Pneuma => uPneuma(),
            HelmSpecial.Hearty => uHearty(),
            _ => false
        };
    }

    /// <summary>
    /// Enhances the equipped items with a profile (see <see cref="ResolveEnhanceProfile"/>), ignoring "Disable AutoEnhance".
    /// Used by the "Enhance" button of the CoreBots Options "Class Enhance" tab.
    /// </summary>
    public void EnhanceEquippedWithProfile(string profile)
    {
        if (!ResolveEnhanceProfile(profile, out EnhancementType type, out CapeSpecial cape, out HelmSpecial helm, out WeaponSpecial weapon))
        {
            Core.Logger($"Invalid enhance profile: {profile}");
            return;
        }

        Core.Logger($"Enhancing equipped items: {type}, weapon {weapon}, cape {cape}, helm {helm}");
        try
        {
            AutoEnhance(Bot.Inventory.Items.FindAll(i => i.Equipped && EnhanceableCatagories.Contains(i.Category)), type, cape, helm, weapon);
        }
        catch (Exception e)
        {
            AdvCrash(e);
        }
    }

    private bool ClassEnhanceProfile(string className, ref EnhancementType? type, ref CapeSpecial cape, ref HelmSpecial helm, ref WeaponSpecial weapon)
    {
        if (!Core.CBOString($"ClassEnh[{className.ToLower()}]", out string profile) && !Core.CBOString("ClassEnh[*]", out profile))
            return false;
        if (!ResolveEnhanceProfile(profile, out EnhancementType baseEnh, out cape, out helm, out weapon))
            return false;

        type = baseEnh;
        Core.Logger($"Class Enhance profile for {className}: {type}, weapon {weapon}, cape {cape}, helm {helm}");
        return true;
    }

'''
assert s.count(old_call)==1 and s.count(anchor)==1, "anchors not found"
s=s.replace(old_call,new_call).replace(anchor,method+anchor)
open(p,'w',encoding='utf-8',newline='').write(s)
