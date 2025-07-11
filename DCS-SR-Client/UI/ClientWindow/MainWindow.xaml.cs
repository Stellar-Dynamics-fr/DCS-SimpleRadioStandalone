﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using MahApps.Metro.Controls;
using NLog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    //
    // private void InitToolTips()
    // {
    //     ExternalAWACSModePassword.ToolTip = ToolTips.ExternalAWACSModePassword;
    //     ExternalAWACSModeName.ToolTip = ToolTips.ExternalAWACSModeName;
    //     ConnectExternalAWACSMode.ToolTip = ToolTips.ExternalAWACSMode;
    // }
    //

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public MainWindow()
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        InitializeComponent();

        // Initialize images/icons
        Images.Init();

        // Initialise sounds
        Sounds.Init();

        // Initialize password display
        this.Loaded += MainWindow_Loaded;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientX).DoubleValue;
        Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientY).DoubleValue;

        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised))
        {
            Hide();
            WindowState = WindowState.Minimized;

            Logger.Info("Started DCS SRS Client " + UpdaterChecker.VERSION + " minimized");
        }
        else
        {
            Logger.Info("Started DCS SRS Client " + UpdaterChecker.VERSION);
        }

        DataContext = new MainWindowViewModel();
        
        // S'abonner à l'événement de mise à jour du PasswordBox
        var viewModel = (MainWindowViewModel)DataContext;
        viewModel.PasswordBoxUpdateRequested += UpdatePasswordBoxFromViewModel;

        //TODO make this a singleton with a callback to check for updates
        // UpdaterChecker.Instance.CheckForUpdate(
        //     _globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates),
        //     result =>
        //     {
        //         if (result.UpdateAvailable)
        //         {
        //             var choice = MessageBox.Show(
        //                 $"{Common.Properties.Resources.MsgBoxUpdate1} {result.Branch} {Common.Properties.Resources.MsgBoxUpdate2} {result.Branch} {Common.Properties.Resources.MsgBoxUpdate3}\n\n{Common.Properties.Resources.MsgBoxUpdate4}",
        //                 Common.Properties.Resources.MsgBoxUpdateTitle, MessageBoxButton.YesNoCancel,
        //                 MessageBoxImage.Information);

        //             if (choice == MessageBoxResult.Yes)
        //             {
        //                 try
        //                 {
        //                     UpdaterChecker.Instance.LaunchUpdater(result.Beta);
        //                 }
        //                 catch (Exception)
        //                 {
        //                     MessageBox.Show($"{Common.Properties.Resources.MsgBoxUpdateFailed}",
        //                         Common.Properties.Resources.MsgBoxUpdateFailedTitle, MessageBoxButton.YesNoCancel,
        //                         MessageBoxImage.Information);

        //                     Process.Start(new ProcessStartInfo(result.Url)
        //                         { UseShellExecute = true });
        //                 }
        //             }
        //             else if (choice == MessageBoxResult.No)
        //             {
        //                 Process.Start(new ProcessStartInfo(result.Url)
        //                     { UseShellExecute = true });
        //             }
        //         }
        //     });


        //TODO move this
        //UpdatePresetsFolderLabel();

        CheckWindowVisibility();

        var args = Environment.GetCommandLineArgs();

        foreach (var arg in args)
            if (arg.StartsWith("-host="))
            {
                var address = arg.Replace("-host=", "");
                var context = ((MainWindowViewModel)DataContext);

                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(2000);

                    try
                    {
                        Logger.Info($"Received -host={address} argument, connecting to {address}");
                        context.ServerAddress = address;
                        context.Connect();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                });

                return;
            }
    }

    private void CheckWindowVisibility()
    {
        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.DisableWindowVisibilityCheck))
        {
            Logger.Info("Window visibility check is disabled, skipping");
            return;
        }

        var mainWindowVisible = false;
        var radioWindowVisible = false;
        var awacsWindowVisible = false;

        var mainWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientX).DoubleValue;
        var mainWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientY).DoubleValue;
        var radioWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioX).DoubleValue;
        var radioWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioY).DoubleValue;
        var awacsWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsX).DoubleValue;
        var awacsWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsY).DoubleValue;

        Logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");
        Logger.Info($"Checking window visibility for radio overlay {{X={radioWindowX},Y={radioWindowY}}}");
        Logger.Info($"Checking window visibility for AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}}");

        foreach (var screen in Screen.AllScreens)
        {
            Logger.Info(
                $"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

            if (screen.Bounds.Contains(mainWindowX, mainWindowY))
            {
                Logger.Info(
                    $"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                mainWindowVisible = true;
            }

            if (screen.Bounds.Contains(radioWindowX, radioWindowY))
            {
                Logger.Info(
                    $"Radio overlay {{X={radioWindowX},Y={radioWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                radioWindowVisible = true;
            }

            if (screen.Bounds.Contains(awacsWindowX, awacsWindowY))
            {
                Logger.Info(
                    $"AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                awacsWindowVisible = true;
            }
        }

        if (!mainWindowVisible)
        {
            MessageBox.Show(this,
                Properties.Resources.MsgBoxNotVisibleText,
                Properties.Resources.MsgBoxNotVisible,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Logger.Warn(
                $"Main client window outside visible area of monitors, resetting position ({mainWindowX},{mainWindowY}) to defaults");

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, 200);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, 200);

            Left = 200;
            Top = 200;
        }

        if (!radioWindowVisible)
        {
            MessageBox.Show(this,
                Properties.Resources.MsgBoxNotVisibleText,
                Properties.Resources.MsgBoxNotVisible,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Logger.Warn(
                $"Radio overlay window outside visible area of monitors, resetting position ({radioWindowX},{radioWindowY}) to defaults");

            EventBus.Instance.PublishOnUIThreadAsync(new CloseRadioOverlayMessage());

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, 300);
        }

        if (!awacsWindowVisible)
        {
            MessageBox.Show(this,
                Properties.Resources.MsgBoxNotVisibleText,
                Properties.Resources.MsgBoxNotVisible,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Logger.Warn(
                $"AWACS overlay window outside visible area of monitors, resetting position ({awacsWindowX},{awacsWindowY}) to defaults");

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsY, 300);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Se désabonner de l'événement
        var viewModel = (MainWindowViewModel)DataContext;
        if (viewModel != null)
        {
            viewModel.PasswordBoxUpdateRequested -= UpdatePasswordBoxFromViewModel;
        }

        ((MainWindowViewModel)DataContext).OnClosing();

        _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, Left);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, Top);

        //save window position
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized &&
            _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray)) Hide();

        base.OnStateChanged(e);
    }

    //private void UpdatePresetsFolderLabel()
    //{
    //    var presetsFolder = _globalSettings.GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;
    //    if (!string.IsNullOrWhiteSpace(presetsFolder))
    //    {
    //        PresetsFolderLabel.Content = Path.GetFileName(presetsFolder);
    //        PresetsFolderLabel.ToolTip = presetsFolder;
    //    }
    //    else
    //    {
    //        PresetsFolderLabel.Content = "(default)";
    //        PresetsFolderLabel.ToolTip = Directory.GetCurrentDirectory();
    //    }
    //}

    //private void PresetsFolderBrowseButton_Click(object sender, RoutedEventArgs e)
    //{
    //    var selectPresetsFolder = new FolderBrowserDialog();
    //    selectPresetsFolder.SelectedPath = PresetsFolderLabel.ToolTip.ToString();
    //    if (selectPresetsFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
    //    {
    //        _globalSettings.SetClientSetting(GlobalSettingsKeys.LastPresetsFolder, selectPresetsFolder.SelectedPath);
    //        UpdatePresetsFolderLabel();
    //    }
    //}

    //private void PresetsFolderResetButton_Click(object sender, RoutedEventArgs e)
    //{
    //    _globalSettings.SetClientSetting(GlobalSettingsKeys.LastPresetsFolder, string.Empty);
    //    UpdatePresetsFolderLabel();
    //}

    private void ToggleServerSettings_Click(object sender, RoutedEventArgs e)
    {

    }

    private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {

    }

    private void ExternalAWACSModePassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)DataContext;
        var passwordBox = (System.Windows.Controls.PasswordBox)sender;
        viewModel.EAMPassword = passwordBox.Password;
        
        // Mettre à jour le mot de passe actuel du PasswordBox dans le ViewModel
        viewModel.UpdateCurrentPasswordBoxValue(passwordBox.Password);
    }

    private void UpdatePasswordBoxFromViewModel()
    {
        var viewModel = (MainWindowViewModel)DataContext;
        var passwordBox = ExternalAWACSModePassword;
        
        if (viewModel.SaveEAMPassword)
        {
            // Si la sauvegarde est activée, charger le mot de passe sauvegardé
            passwordBox.Password = viewModel.EAMPassword ?? "";
        }
        // Si la sauvegarde est désactivée, ne pas modifier le contenu du PasswordBox
        // L'utilisateur peut continuer à utiliser le mot de passe saisi
        
        // Mettre à jour le mot de passe actuel du PasswordBox dans le ViewModel
        viewModel.UpdateCurrentPasswordBoxValue(passwordBox.Password);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Charger le mot de passe sauvegardé dans le PasswordBox seulement si l'option de sauvegarde est activée
        var viewModel = (MainWindowViewModel)DataContext;
        var passwordBox = ExternalAWACSModePassword;
        
        // Vérifier si l'option de sauvegarde est activée avant de charger le mot de passe
        if (viewModel.SaveEAMPassword)
        {
            passwordBox.Password = viewModel.EAMPassword ?? "";
        }
        // Si la sauvegarde n'est pas activée, laisser le PasswordBox vide
        // L'utilisateur devra saisir le mot de passe manuellement
        
        // Mettre à jour le mot de passe actuel du PasswordBox dans le ViewModel
        viewModel.UpdateCurrentPasswordBoxValue(passwordBox.Password);
    }

    private void ClientSettings_Loaded(object sender, RoutedEventArgs e)
    {

    }
}