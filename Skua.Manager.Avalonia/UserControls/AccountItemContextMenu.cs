using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Data;
using CommunityToolkit.Mvvm.DependencyInjection;
using Material.Icons;
using Material.Icons.Avalonia;
using Skua.Core.Interfaces.ViewModels;
using Skua.Core.Utils;
using Skua.Manager.Avalonia.ViewModels;

namespace Skua.Manager.Avalonia.UserControls;

public class AccountItemContextMenu : ContextMenu
{
    public AccountItemContextMenu()
    {
        Opened += (s, e) =>
        {
            if (PlacementTarget is Control target && target.DataContext is IAccountItemViewModel vm)
            {
                DataContext = vm;
            }
        };

        Items.Add(CreateBoundMenuItem("Start", "StartCommand", MaterialIconKind.PlayCircleOutline, vm => vm.StartCommand.Execute(null)));
        Items.Add(CreateBoundMenuItem("Start With Script", "StartWithScriptCommand", MaterialIconKind.ScriptTextPlayOutline, vm => vm.StartWithScriptCommand.Execute(null)));

        MenuItem startInNewLauncher = new() { Header = "Start in New Launcher", Icon = new MaterialIcon { Kind = MaterialIconKind.ApplicationOutline, Width = 14, Height = 14 } };
        startInNewLauncher.Click += (s, e) => StartInLauncher(AccountManagerViewModel.NewLauncherTarget);
        Items.Add(startInNewLauncher);

        _startInLauncherItem = new() { Header = "Start in Launcher", Icon = new MaterialIcon { Kind = MaterialIconKind.ApplicationImport, Width = 14, Height = 14 } };
        Items.Add(_startInLauncherItem);
        Opened += (s, e) => PopulateLauncherSubMenu();
        Items.Add(new Separator());
        Items.Add(CreateBoundMenuItem("Edit Account", "EditCommand", MaterialIconKind.AccountEditOutline, vm => vm.EditCommand.Execute(null)));
        Items.Add(CreateBoundMenuItem("Edit Tags", "AddTagsCommand", MaterialIconKind.Tag, vm => vm.AddTagsCommand.Execute(null)));
        Items.Add(CreateBoundMenuItem("Add to Group", "AddToGroupCommand", MaterialIconKind.AccountMultiplePlus, vm => vm.AddToGroupCommand.Execute(null)));
        Items.Add(new Separator());
        Items.Add(CreateBoundMenuItem("Remove", "RemoveCommand", MaterialIconKind.DeleteOutline, vm => vm.RemoveCommand.Execute(null)));
    }

    private readonly MenuItem _startInLauncherItem;

    private void PopulateLauncherSubMenu()
    {
        _startInLauncherItem.Items.Clear();
        List<LauncherHostInfo> hosts = LauncherRegistry.GetLiveHosts();
        if (hosts.Count == 0)
        {
            _startInLauncherItem.Items.Add(new MenuItem { Header = "No launcher open", IsEnabled = false });
            return;
        }

        foreach (LauncherHostInfo host in hosts)
        {
            int pid = host.Pid;
            MenuItem hostItem = new() { Header = host.DisplayName };
            hostItem.Click += (s, e) => StartInLauncher(pid);
            _startInLauncherItem.Items.Add(hostItem);
        }
    }

    private void StartInLauncher(int launcherTarget)
    {
        IAccountItemViewModel? account = GetAccount();
        if (account == null)
            return;
        Ioc.Default.GetService<AccountManagerViewModel>()?.StartAccountInLauncher(account, launcherTarget);
    }

    private IAccountItemViewModel? GetAccount()
    {
        if (DataContext is IAccountItemViewModel vm)
            return vm;
        if (PlacementTarget is Control target && target.DataContext is IAccountItemViewModel targetVm)
            return targetVm;
        return null;
    }

    private MenuItem CreateBoundMenuItem(string header, string commandPath, MaterialIconKind iconKind, Action<IAccountItemViewModel>? clickFallback = null)
    {
        MenuItem item = new() { Header = header };
        item.Bind(MenuItem.CommandProperty, new Binding(commandPath));
        if (clickFallback != null)
        {
            item.Click += (s, e) =>
            {
                if (item.Command == null)
                {
                    var account = GetAccount();
                    if (account != null)
                        clickFallback(account);
                }
            };
        }
        item.Icon = new MaterialIcon { Kind = iconKind, Width = 14, Height = 14 };
        return item;
    }
}
