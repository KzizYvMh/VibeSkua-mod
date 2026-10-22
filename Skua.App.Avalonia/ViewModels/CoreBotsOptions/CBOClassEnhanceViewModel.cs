using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Models.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Skua.App.Avalonia.ViewModels.CoreBotsOptions;

/// <summary>
/// Per-class enhancement profiles, saved to the CBO file as "ClassEnh[Class]: Base|WeaponForge|WeaponAwe|Cape|Helm".
/// CoreAdvanced.SmartEnhance reads them and falls back Forge -> Awe -> base enhancement per slot.
/// </summary>
public partial class CBOClassEnhanceViewModel : ObservableObject, IManageCBOptions
{
    private const string KeyPrefix = "ClassEnh[";
    private const string DefaultKey = "*";

    public CBOClassEnhanceViewModel(IScriptInventory inventory, IFileDialogService fileDialog, IDialogService dialogService)
    {
        _inventory = inventory;
        _fileDialog = fileDialog;
        _dialogService = dialogService;
        Entries.Add(new ClassEnhanceEntry(DefaultKey, "Default (classes without a profile)"));
        _selectedEntry = Entries[0];
    }

    private readonly IScriptInventory _inventory;
    private readonly IFileDialogService _fileDialog;
    private readonly IDialogService _dialogService;

    public static List<string> BaseEnhancements { get; } = new() { "Lucky", "Fighter", "Thief", "Hybrid", "Wizard", "Healer", "SpellBreaker" };
    public static List<string> WeaponForgeSpecials { get; } = new() { "None", "Forge", "Lacerate", "Smite", "Valiance", "Arcanas_Concerto", "Acheron", "Elysium", "Praxis", "Dauntless", "Ravenous" };
    public static List<string> WeaponAweSpecials { get; } = new() { "None", "Spiral_Carve", "Awe_Blast", "Health_Vamp", "Mana_Vamp", "Powerword_Die" };
    public static List<string> CapeSpecials { get; } = new() { "None", "Forge", "Absolution", "Avarice", "Vainglory", "Penitence", "Lament" };
    public static List<string> HelmSpecials { get; } = new() { "None", "Forge", "Vim", "Examen", "Anima", "Pneuma", "Hearty" };

    public ObservableCollection<ClassEnhanceEntry> Entries { get; } = new();

    [ObservableProperty]
    private ClassEnhanceEntry? _selectedEntry;

    [RelayCommand]
    private void RefreshInventory()
    {
        IEnumerable<string> classes = _inventory.Items?.Where(i => i.Category == ItemCategory.Class).Select(i => i.Name) ?? Enumerable.Empty<string>();
        foreach (string name in classes)
            GetOrAdd(name);
        Sort();
    }

    [RelayCommand]
    private void Export()
    {
        string? path = _fileDialog.Save("JSON Files (*.json)|*.json");
        if (string.IsNullOrEmpty(path))
            return;

        Dictionary<string, ClassEnhanceProfile> profiles = Entries.Where(e => e.Enabled).ToDictionary(e => e.Key, e => e.ToProfile());
        File.WriteAllText(path, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
    }

    [RelayCommand]
    private void Import()
    {
        string? path = _fileDialog.OpenFile("JSON Files (*.json)|*.json");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            Dictionary<string, ClassEnhanceProfile>? profiles = JsonSerializer.Deserialize<Dictionary<string, ClassEnhanceProfile>>(File.ReadAllText(path));
            foreach ((string key, ClassEnhanceProfile profile) in profiles ?? new())
                GetOrAdd(key).Apply(profile);
            Sort();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Could not import profiles: {ex.Message}", "Import");
        }
    }

    public StringBuilder Save(StringBuilder builder)
    {
        foreach (ClassEnhanceEntry entry in Entries.Where(e => e.Enabled))
            builder.AppendLine($"{KeyPrefix}{entry.Key}]: {entry.Base}|{entry.WeaponForge}|{entry.WeaponAwe}|{entry.Cape}|{entry.Helm}");
        return builder;
    }

    public void SetValues(Dictionary<string, string> values)
    {
        foreach (ClassEnhanceEntry entry in Entries)
            entry.Enabled = false;

        foreach ((string key, string value) in values)
        {
            if (!key.StartsWith(KeyPrefix) || !key.EndsWith(']'))
                continue;

            string[] parts = value.Split('|');
            if (parts.Length != 5)
                continue;

            GetOrAdd(key[KeyPrefix.Length..^1]).Apply(new ClassEnhanceProfile(parts[0], parts[1], parts[2], parts[3], parts[4]));
        }
        Sort();
    }

    private ClassEnhanceEntry GetOrAdd(string key)
    {
        ClassEnhanceEntry? entry = Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new ClassEnhanceEntry(key, key);
            Entries.Add(entry);
        }
        return entry;
    }

    private void Sort()
    {
        List<ClassEnhanceEntry> sorted = Entries.OrderBy(e => e.Key != DefaultKey).ThenBy(e => e.Display, StringComparer.OrdinalIgnoreCase).ToList();
        ClassEnhanceEntry? selected = SelectedEntry;
        Entries.Clear();
        foreach (ClassEnhanceEntry entry in sorted)
            Entries.Add(entry);
        SelectedEntry = selected ?? Entries[0];
    }
}

public record ClassEnhanceProfile(string Base, string WeaponForge, string WeaponAwe, string Cape, string Helm);

public partial class ClassEnhanceEntry : ObservableObject
{
    public ClassEnhanceEntry(string key, string display)
    {
        Key = key;
        Display = display;
    }

    public string Key { get; }
    public string Display { get; }

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _base = "Lucky";

    [ObservableProperty]
    private string _weaponForge = "None";

    [ObservableProperty]
    private string _weaponAwe = "None";

    [ObservableProperty]
    private string _cape = "None";

    [ObservableProperty]
    private string _helm = "None";

    public ClassEnhanceProfile ToProfile() => new(Base, WeaponForge, WeaponAwe, Cape, Helm);

    public void Apply(ClassEnhanceProfile profile)
    {
        Base = CBOClassEnhanceViewModel.BaseEnhancements.Contains(profile.Base) ? profile.Base : "Lucky";
        WeaponForge = CBOClassEnhanceViewModel.WeaponForgeSpecials.Contains(profile.WeaponForge) ? profile.WeaponForge : "None";
        WeaponAwe = CBOClassEnhanceViewModel.WeaponAweSpecials.Contains(profile.WeaponAwe) ? profile.WeaponAwe : "None";
        Cape = CBOClassEnhanceViewModel.CapeSpecials.Contains(profile.Cape) ? profile.Cape : "None";
        Helm = CBOClassEnhanceViewModel.HelmSpecials.Contains(profile.Helm) ? profile.Helm : "None";
        Enabled = true;
    }
}
