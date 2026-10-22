using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Models;
using Skua.Core.Models.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skua.App.Avalonia.ViewModels.CoreBotsOptions;

/// <summary>
/// Per-class enhancement profiles. Every class starts with the CoreAdvanced recommendation (ClassEnhancePresets.json);
/// custom ones are saved to the CBO file as "ClassEnh[class]: Base|WeaponForge|WeaponAwe|Cape|Helm"
/// and replace the built-in library in CoreAdvanced.SmartEnhance.
/// </summary>
public partial class CBOClassEnhanceViewModel : ObservableObject, IManageCBOptions
{
    private const string KeyPrefix = "ClassEnh[";
    private const string DefaultKey = "*";

    public CBOClassEnhanceViewModel(IScriptInventory inventory, IScriptPlayer player, IScriptManager manager, IFileDialogService fileDialog, IDialogService dialogService)
    {
        _inventory = inventory;
        _player = player;
        _manager = manager;
        _fileDialog = fileDialog;
        _dialogService = dialogService;

        AddEntry(new ClassEnhanceEntry(DefaultKey, "Default (classes without a custom profile)", new("Lucky", "None", "None", "None", "None")));
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ClassEnhancePresets.json");
        Dictionary<string, ClassEnhanceProfile> presets = stream is null ? new() : JsonSerializer.Deserialize<Dictionary<string, ClassEnhanceProfile>>(stream) ?? new();
        foreach ((string name, ClassEnhanceProfile preset) in presets)
            AddEntry(new ClassEnhanceEntry(name, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name), preset));

        ApplyFilter();
    }

    private readonly IScriptInventory _inventory;
    private readonly IScriptPlayer _player;
    private readonly IScriptManager _manager;
    private readonly IFileDialogService _fileDialog;
    private readonly IDialogService _dialogService;
    private readonly List<ClassEnhanceEntry> _entries = new();

    public static List<string> BaseEnhancements { get; } = new() { "Lucky", "Fighter", "Thief", "Hybrid", "Wizard", "Healer", "SpellBreaker" };
    public static List<string> WeaponForgeSpecials { get; } = new() { "None", "Forge", "Lacerate", "Smite", "Valiance", "Arcanas_Concerto", "Acheron", "Elysium", "Praxis", "Dauntless", "Ravenous" };
    public static List<string> WeaponAweSpecials { get; } = new() { "None", "Spiral_Carve", "Awe_Blast", "Health_Vamp", "Mana_Vamp", "Powerword_Die" };
    public static List<string> CapeSpecials { get; } = new() { "None", "Forge", "Absolution", "Avarice", "Vainglory", "Penitence", "Lament" };
    public static List<string> HelmSpecials { get; } = new() { "None", "Forge", "Vim", "Examen", "Anima", "Pneuma", "Hearty" };

    public ObservableCollection<ClassEnhanceEntry> Entries { get; } = new();

    [ObservableProperty]
    private ClassEnhanceEntry? _selectedEntry;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _onlyOwned;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnOnlyOwnedChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private void RefreshInventory()
    {
        foreach (InventoryItem item in _inventory.Items?.Where(i => i.Category == ItemCategory.Class) ?? Enumerable.Empty<InventoryItem>())
        {
            ClassEnhanceEntry entry = GetOrAdd(item.Name.ToLowerInvariant());
            entry.Display = item.Name;
            entry.Owned = true;
        }
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Enhance()
    {
        if (_manager.ScriptRunning)
        {
            _dialogService.ShowMessageBox("Stop the running script before enhancing.", "Enhance");
            return;
        }

        string? currentClass = _player.CurrentClass?.Name;
        if (string.IsNullOrEmpty(currentClass))
        {
            _dialogService.ShowMessageBox("Login and equip a class first.", "Enhance");
            return;
        }

        // Same priority as SmartEnhance: custom profile, then enabled Default, then the class recommendation
        ClassEnhanceEntry? classEntry = Find(currentClass.ToLowerInvariant());
        ClassEnhanceEntry entry = classEntry is { Enabled: true } || !_entries[0].Enabled ? classEntry ?? _entries[0] : _entries[0];

        string scriptPath = Path.Combine(ClientFileSources.SkuaScriptsDIR, "ClassEnhanceTemp.cs");
        File.WriteAllText(scriptPath, $@"//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreFarms.cs
//cs_include Scripts/CoreAdvanced.cs
using Skua.Core.Interfaces;

public class ClassEnhanceTemp
{{
    public void ScriptMain(IScriptInterface bot)
    {{
        string map = bot.Map.FullName;
        new CoreAdvanced().EnhanceEquippedWithProfile(""{entry.Serialize()}"");
        if (bot.Map.FullName != map)
            CoreBots.Instance.Join(map);
    }}
}}");
        _manager.SetLoadedScript(scriptPath);
        await _manager.StartScript();
    }

    [RelayCommand]
    private void Export()
    {
        string? path = _fileDialog.Save("JSON Files (*.json)|*.json");
        if (string.IsNullOrEmpty(path))
            return;

        Dictionary<string, ClassEnhanceProfile> profiles = _entries.Where(e => e.Enabled).ToDictionary(e => e.Key, e => e.ToProfile());
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
                GetOrAdd(key.ToLowerInvariant()).Apply(profile);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Could not import profiles: {ex.Message}", "Import");
        }
    }

    public StringBuilder Save(StringBuilder builder)
    {
        foreach (ClassEnhanceEntry entry in _entries.Where(e => e.Enabled))
            builder.AppendLine($"{KeyPrefix}{entry.Key}]: {entry.Serialize()}");
        return builder;
    }

    public void SetValues(Dictionary<string, string> values)
    {
        foreach (ClassEnhanceEntry entry in _entries)
            entry.Enabled = false;

        foreach ((string key, string value) in values)
        {
            if (!key.StartsWith(KeyPrefix) || !key.EndsWith(']'))
                continue;

            string[] parts = value.Split('|');
            if (parts.Length == 5)
                GetOrAdd(key[KeyPrefix.Length..^1]).Apply(new ClassEnhanceProfile(parts[0], parts[1], parts[2], parts[3], parts[4]));
        }
        ApplyFilter();
    }

    private ClassEnhanceEntry? Find(string key) => _entries.FirstOrDefault(e => e.Key == key);

    private ClassEnhanceEntry GetOrAdd(string key)
    {
        ClassEnhanceEntry? entry = Find(key);
        if (entry is null)
        {
            entry = new ClassEnhanceEntry(key, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key), new("Lucky", "None", "None", "None", "None"));
            AddEntry(entry);
        }
        return entry;
    }

    private void AddEntry(ClassEnhanceEntry entry)
    {
        entry.PropertyChanged += OnEntryChanged;
        _entries.Add(entry);
    }

    // Only one profile can be active: enabling one disables the others
    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ClassEnhanceEntry.Enabled) || sender is not ClassEnhanceEntry { Enabled: true } enabled)
            return;

        foreach (ClassEnhanceEntry entry in _entries.Where(x => x != enabled && x.Enabled))
            entry.Enabled = false;
    }

    private void ApplyFilter()
    {
        ClassEnhanceEntry? selected = SelectedEntry;
        Entries.Clear();
        foreach (ClassEnhanceEntry entry in _entries
            .Where(e => e.Key == DefaultKey || ((!OnlyOwned || e.Owned) && e.Display.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Key != DefaultKey)
            .ThenBy(e => e.Display, StringComparer.OrdinalIgnoreCase))
            Entries.Add(entry);
        SelectedEntry = selected is not null && Entries.Contains(selected) ? selected : Entries[0];
    }
}

public record ClassEnhanceProfile(string Base, string WeaponForge, string WeaponAwe, string Cape, string Helm);

public partial class ClassEnhanceEntry : ObservableObject
{
    public ClassEnhanceEntry(string key, string display, ClassEnhanceProfile preset)
    {
        Key = key;
        _display = display;
        _preset = preset;
        Load(preset);
    }

    private readonly ClassEnhanceProfile _preset;
    private bool _loading;

    public string Key { get; }

    [ObservableProperty]
    private string _display;

    [ObservableProperty]
    private bool _owned;

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

    // Unchecking a custom profile goes back to the recommended one; editing any slot makes it custom.
    partial void OnEnabledChanged(bool value)
    {
        if (!value)
            Load(_preset);
    }

    partial void OnBaseChanged(string value) => MarkCustom();

    partial void OnWeaponForgeChanged(string value) => MarkCustom();

    partial void OnWeaponAweChanged(string value) => MarkCustom();

    partial void OnCapeChanged(string value) => MarkCustom();

    partial void OnHelmChanged(string value) => MarkCustom();

    private void MarkCustom()
    {
        if (!_loading)
            Enabled = true;
    }

    public ClassEnhanceProfile ToProfile() => new(Base, WeaponForge, WeaponAwe, Cape, Helm);

    public string Serialize() => $"{Base}|{WeaponForge}|{WeaponAwe}|{Cape}|{Helm}";

    public void Apply(ClassEnhanceProfile profile)
    {
        Load(profile);
        Enabled = true;
    }

    private void Load(ClassEnhanceProfile profile)
    {
        _loading = true;
        Base = Pick(CBOClassEnhanceViewModel.BaseEnhancements, profile.Base, "Lucky");
        WeaponForge = Pick(CBOClassEnhanceViewModel.WeaponForgeSpecials, profile.WeaponForge, "None");
        WeaponAwe = Pick(CBOClassEnhanceViewModel.WeaponAweSpecials, profile.WeaponAwe, "None");
        Cape = Pick(CBOClassEnhanceViewModel.CapeSpecials, profile.Cape, "None");
        Helm = Pick(CBOClassEnhanceViewModel.HelmSpecials, profile.Helm, "None");
        _loading = false;

        static string Pick(List<string> options, string value, string fallback) => options.Contains(value) ? value : fallback;
    }
}
