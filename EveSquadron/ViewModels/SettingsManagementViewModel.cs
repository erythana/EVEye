using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using EveSquadron.DataRepositories.Interfaces;
using EveSquadron.Models;
using EveSquadron.Models.Enums;
using EveSquadron.Models.Helper;
using EveSquadron.Models.Options;
using EveSquadron.ViewModels.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReactiveUI;

namespace EveSquadron.ViewModels;

public class SettingsManagementViewModel : ViewModelBase, ISettingsManagementViewModel
{
    #region member fields

    private readonly IApplicationSettingDataRepository _applicationSettingDataRepository;
    private readonly ILogger<ISettingsManagementViewModel> _logger;
    private int _clipboardPolling;
    private bool _autoExport;
    private bool _showPortrait;
    private bool _alwaysOnTop;
    private Color _hoverColor;
    private string _exportFile;
    private GridRowSizeEnum _gridRowSize;
    private ThemeVariant _theme;
    
    private Dictionary<string, string> _settingsToSave;
    private bool _whitelistActive;

    #endregion

    #region constructor

    public SettingsManagementViewModel(IServiceProvider serviceProvider, IApplicationSettingDataRepository applicationSettingDataRepository, ILogger<ISettingsManagementViewModel> logger)
    {
        _applicationSettingDataRepository = applicationSettingDataRepository;
        _logger = logger;
        _settingsToSave = new Dictionary<string, string>();

        SaveApplicationSettingsCommand = ReactiveCommand.CreateFromTask(OnSaveApplicationSettingsCommand);
        OpenExportFilePickerCommand = ReactiveCommand.CreateFromTask(OnOpenAutoExportFolderPickerCommand);
        ClearExportFileCommand = ReactiveCommand.Create(OnClearExportFileCommand);
        
        MinimumClipboardPolling = AppConstants.MinimalClipboardPollingMs;
        MaximumClipboardPolling = AppConstants.MaximalClipboardPollingMs;
        AvailableGridRowSizes = Enum.GetValues<GridRowSizeEnum>();
        AvailableThemes = new List<ThemeVariant>
        {
            ThemeVariant.Default,
            ThemeVariant.Light,
            ThemeVariant.Dark
        };
        var colors = GetAllAvailableColors();
        AvailableHoverColors = colors;
        
        LoadAndSetSavedSettingsFromOptions(serviceProvider);
    }

    private async Task OnOpenAutoExportFolderPickerCommand()
    {
        try  //ReactiveCommand
        {
            var window = ApplicationHelper.GetMainWindow();
            if (window is null)
                return;
                
            var file = await GetCsvTargetFileFromSaveFilePickerAsync(window);

            var filePath = file?.Path.AbsolutePath;
            if (string.IsNullOrWhiteSpace(filePath) || !ExportFileHelper.IsValidExportFile(filePath))
                return;
            
            ExportFile = filePath;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Could not open folder picker dialog!");
            throw;
        }
    }

    private async Task OnSaveApplicationSettingsCommand()
    {
        try  //ReactiveCommand
        {
            var saveableSettings = GetListOfWriteableSettings();
            await _applicationSettingDataRepository.SaveApplicationSettings(saveableSettings);
            _settingsToSave.Clear();
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Could not save application settings!");
            throw;
        }
    }
    
    private void OnClearExportFileCommand()
    {
        ExportFile = "";
    }
    
    #endregion

    #region properties

    public IReactiveCommand SaveApplicationSettingsCommand { get; }
    public IReactiveCommand OpenExportFilePickerCommand { get; }
    public IReactiveCommand ClearExportFileCommand { get; }
    public int MinimumClipboardPolling { get; }
    public int MaximumClipboardPolling { get; }

    public int ClipboardPolling {
        get => _clipboardPolling;
        set
        {
            SetProperty(ref _clipboardPolling, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(ClipboardPolling), value.ToString());
        }
    }

    public bool AutoExport {
        get => _autoExport;
        set
        {
            SetProperty(ref _autoExport, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(AutoExport), value.ToString());
        }
    }

    public bool WhitelistActive {
        get => _whitelistActive;
        set
        {
            SetProperty(ref _whitelistActive, value);
            AddToSaveableSettings(StatusOptions.Section, nameof(WhitelistActive), value.ToString());
        }
    }

    public bool ShowPortrait {
        get => _showPortrait;
        set
        {
            SetProperty(ref _showPortrait, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(ShowPortrait), value.ToString());
        }
    }

    public bool AlwaysOnTop {
        get => _alwaysOnTop;
        set
        {
            SetProperty(ref _alwaysOnTop, value);
            AddToSaveableSettings(StatusOptions.Section, nameof(AlwaysOnTop), value.ToString());
        }
    }

    public Color HoverColor {
        get => _hoverColor;
        set
        {
            SetProperty(ref _hoverColor, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(HoverColor), value.ToString());
        }
    }

    public string ExportFile {
        get => _exportFile;
        set
        {
            SetProperty(ref _exportFile, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(ExportFile), value);
        }
    }

    public GridRowSizeEnum GridRowSize {
        get => _gridRowSize;
        set
        {
            SetProperty(ref _gridRowSize, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(GridRowSize), value.ToString());
        }
    }

    public ThemeVariant Theme {
        get => _theme;
        set
        {
            SetProperty(ref _theme, value);
            AddToSaveableSettings(EveSquadronOptions.Section, nameof(Theme), value.ToString());
        }
    }

    public IEnumerable<ThemeVariant> AvailableThemes { get; set; }
    public IEnumerable<Color> AvailableHoverColors { get; set; }
    public IEnumerable<GridRowSizeEnum> AvailableGridRowSizes { get; set; }

    #endregion

    #region helper methods
    
    private static IEnumerable<Color> GetAllAvailableColors()
    {
        var properties = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var colors = properties.Where(x => x.PropertyType == typeof(Color)).Select(x => (Color)x.GetValue(null)!);
        return colors;
    }
    
    private void LoadAndSetSavedSettingsFromOptions(IServiceProvider serviceProvider)
    {
        var eveSquadronOptions = ResolveOptionsFromType<EveSquadronOptions>();
        var statusOptions = ResolveOptionsFromType<StatusOptions>();
        
        ClipboardPolling = int.TryParse(eveSquadronOptions.Value.ClipboardPolling, out var polling) ? polling : AppConstants.DefaultClipboardPollingMs;
        HoverColor = SettingConversionHelper.StringToColorConverter(eveSquadronOptions.Value.HoverColor);
        Theme = SettingConversionHelper.StringToThemeConverter(eveSquadronOptions.Value.Theme);
        ExportFile = eveSquadronOptions.Value.ExportFile;
        AutoExport = bool.TryParse(eveSquadronOptions.Value.AutoExport, out var autoExport) && autoExport;
        ShowPortrait = bool.TryParse(eveSquadronOptions.Value.ShowPortrait, out var showPortrait) && showPortrait;
        AlwaysOnTop = bool.TryParse(statusOptions.Value.AlwaysOnTop, out var alwaysOnTop) && alwaysOnTop;
        WhitelistActive = bool.TryParse(statusOptions.Value.WhitelistActive, out var whitelistActive) && whitelistActive;

        GridRowSize = Enum.TryParse(eveSquadronOptions.Value.GridRowSize, out GridRowSizeEnum rowSize)
            ? rowSize
            : AppConstants.DefaultGridRowSize;
        
        IOptions<T> ResolveOptionsFromType<T>() where T : class => (IOptions<T>)serviceProvider.GetService(typeof(IOptions<T>))!;
    }

    private void AddToSaveableSettings(string sectionTarget, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        
        if (!_settingsToSave.TryAdd($"{sectionTarget}:{name}", value))
            _settingsToSave[$"{sectionTarget}:{name}"] = value;
    }
    
    private IEnumerable<ConfigurationValue> GetListOfWriteableSettings() => 
        _settingsToSave.Select(x => new ConfigurationValue {Name = x.Key, Value = x.Value});

    private async static Task<IStorageFile?> GetCsvTargetFileFromSaveFilePickerAsync(TopLevel topLevel) => await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        { 
            ShowOverwritePrompt = false,
            Title = "Save into CSV File",
            DefaultExtension = "*.csv",
            SuggestedFileName = "EveSquadron-Export.csv",
            FileTypeChoices = new List<FilePickerFileType>{ new("CSV")
            {
                Patterns = new[]{"*.csv"},
                MimeTypes = new[]{"text/*"},
            }}
            
        });
    
    #endregion
}