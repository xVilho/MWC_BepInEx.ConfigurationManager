// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ConfigurationManager.Utilities;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace ConfigurationManager
{
    /// <summary>
    /// An easy way to let user configure how a plugin behaves without the need to make your own GUI. The user can change any of the settings you expose, even keyboard shortcuts.
    /// https://github.com/ManlyMarco/BepInEx.ConfigurationManager
    /// </summary>
    [BepInPlugin(GUID, "Configuration Manager", Constants.Version)]
    [Browsable(false)]
    public class ConfigurationManager : BaseUnityPlugin
    {
        internal static ConfigurationManager Instance;
        internal static Vector3 _frozenMousePos;
        internal static bool _isRenderingGui;
        /// <summary>
        /// GUID of this plugin
        /// </summary>
        public const string GUID = "com.bepis.bepinex.configurationmanager";

        /// <summary>
        /// Version constant
        /// </summary>
        public const string Version = Constants.Version;

        internal static new ManualLogSource Logger;
        private static SettingFieldDrawer _fieldDrawer;

        private static readonly Color _advancedSettingColor = new Color(1f, 0.95f, 0.67f, 1f);
        private const int WindowId = -68;

        private const string SearchBoxName = "searchBox";
        private bool _focusSearchBox;
        private string _searchString = string.Empty;

        /// <summary>
        /// Event fired every time the manager window is shown or hidden.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<bool>> DisplayingWindowChanged;

        /// <summary>
        /// Disable the hotkey check used by config manager. If enabled you have to set <see cref="DisplayingWindow"/> to show the manager.
        /// </summary>
        public bool OverrideHotkey;

        private bool _displayingWindow;

        private string _modsWithoutSettings;

        private List<SettingEntryBase> _allSettings;
        private List<PluginSettingsData> _filteredSetings = new List<PluginSettingsData>();

        internal Rect SettingWindowRect { get; private set; }
        private bool _windowWasMoved;

        /// <summary>
        /// Window is visible and is blocking the whole screen. This is true until the user moves the window, which lets it run while user interacts with the game.
        /// </summary>
        public bool IsWindowFullscreen => DisplayingWindow && !_windowWasMoved;

        private bool _tipsPluginHeaderWasClicked, _tipsWindowWasMoved;

        private Rect _screenRect;
        private Vector2 _settingWindowScrollPos;
        private int _tipsHeight;

        private CursorLockMode _previousCursorLockState;
        private bool _previousCursorVisible;

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }

        private static PropertyInfo _screenShowCursor;
        private static PropertyInfo _pmGuiLockCursor, _pmGuiHideCursor;
        private static FieldInfo _pmGuiLockCursorField, _pmGuiHideCursorField;

        private readonly ConfigEntry<bool> _showAdvanced;
        private readonly ConfigEntry<bool> _showKeybinds;
        private readonly ConfigEntry<bool> _showSettings;
        private readonly ConfigEntry<KeyboardShortcut> _keybind;
        private readonly ConfigEntry<bool> _hideSingleSection;
        private readonly ConfigEntry<bool> _pluginConfigCollapsedDefault;
        private bool _showDebug;

        /// <inheritdoc />
        public ConfigurationManager()
        {
            Instance = this;
            Logger = base.Logger;
            _fieldDrawer = new SettingFieldDrawer(this);

            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _showKeybinds = Config.Bind("Filtering", "Show keybinds", true);
            _showSettings = Config.Bind("Filtering", "Show settings", true);
            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F6),
                new ConfigDescription("The shortcut used to toggle the config manager window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));
            _hideSingleSection = Config.Bind("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));
            _pluginConfigCollapsedDefault = Config.Bind("General", "Plugin collapsed default", true, new ConfigDescription("If set to true plugins will be collapsed when opening the configuration manager window"));
        }

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("Configuration Manager: Initializing patches...");
            try
            {
                var harmony = new Harmony(GUID);
                
                // One-pass assembly scan for all required types
                var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    }).ToList();

                var pmGuiType = allTypes.FirstOrDefault(t => t.Name == "PlayMakerGUI");
                var cInputType = allTypes.FirstOrDefault(t => t.Name == "cInput");
                var mouseLookTypes = new[] { "MouseLook", "SmoothMouseLook", "SimpleSmoothMouseLook" };

                // Apply standard patches
                harmony.PatchAll(typeof(CursorPatch));
                harmony.PatchAll(typeof(ScreenPatch));
                harmony.PatchAll(typeof(CursorSetCursorPatch));
                harmony.PatchAll(typeof(InputPatch));
                harmony.PatchAll(typeof(MousePositionPatch));
                
                // Apply PlayMakerGUI patches
                if (pmGuiType != null)
                {
                    _pmGuiLockCursor = pmGuiType.GetProperty("LockCursor", BindingFlags.Static | BindingFlags.Public);
                    _pmGuiHideCursor = pmGuiType.GetProperty("HideCursor", BindingFlags.Static | BindingFlags.Public);
                    _pmGuiLockCursorField = pmGuiType.GetField("LockCursor", BindingFlags.Static | BindingFlags.Public);
                    _pmGuiHideCursorField = pmGuiType.GetField("HideCursor", BindingFlags.Static | BindingFlags.Public);
                    PlayMakerGUIPatch.Patch(harmony, pmGuiType);
                    Logger.LogInfo("Configuration Manager: Patched PlayMakerGUI");
                }

                InputPatch.PatchCInput(harmony, cInputType);

                // Apply MouseLook patches
                var methods = new[] { "Update", "LateUpdate", "FixedUpdate" };
                foreach (var type in allTypes)
                {
                    if (type.Name.Contains("MouseLook") || type.Name == "MainCamera")
                    {
                        foreach (var methodName in methods)
                        {
                            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (method != null) harmony.Patch(method, new HarmonyMethod(typeof(MouseLookPatch), nameof(MouseLookPatch.Prefix)));
                        }
                    }
                }
                
                Logger.LogInfo("Configuration Manager: Initialization complete.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply Harmony patches: " + ex);
            }
        }

        private List<Behaviour> _disabledCameraComponents = new List<Behaviour>();

        /// <summary>
        /// Is the config manager main window displayed on screen
        /// </summary>
        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value) return;
                _displayingWindow = value;

                SettingFieldDrawer.ClearCache();

                if (_displayingWindow)
                {
                    CalculateWindowRect();
                    BuildSettingList();
                    _previousCursorLockState = Cursor.lockState;
                    _previousCursorVisible = Cursor.visible;
                    _frozenMousePos = Input.mousePosition;
                    SetUnlockCursor(CursorLockMode.None, true);

                    try
                    {
                        var objectsToSearch = new List<GameObject>();
                        if (Camera.main != null) objectsToSearch.Add(Camera.main.gameObject);
                        
                        var playerGo = GameObject.Find("PLAYER");
                        if (playerGo != null) objectsToSearch.Add(playerGo);

                        var typesToDisable = new[] { "MouseLook", "SmoothMouseLook", "SimpleSmoothMouseLook", "MainCamera" };
                        
                        foreach (var go in objectsToSearch)
                        {
                            var behaviours = go.GetComponentsInChildren<Behaviour>(true);
                            foreach (var comp in behaviours)
                            {
                                if (comp != null && comp.enabled && typesToDisable.Contains(comp.GetType().Name))
                                {
                                    comp.enabled = false;
                                    _disabledCameraComponents.Add(comp);
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Logger.LogDebug("Failed to disable camera components: " + ex.Message); }
                }
                else
                {
                    foreach (var comp in _disabledCameraComponents)
                    {
                        if (comp != null) comp.enabled = true;
                    }
                    _disabledCameraComponents.Clear();

                    SetUnlockCursor(_previousCursorLockState, _previousCursorVisible);
                }

                DisplayingWindowChanged?.Invoke(this, new ValueChangedEventArgs<bool>(value));
            }
        }

        /// <summary>
        /// Register a custom setting drawer for a given type. The action is ran in OnGui in a single setting slot.
        /// Do not use any Begin / End layout methods, and avoid raising height from standard.
        /// </summary>
        public static void RegisterCustomSettingDrawer(Type settingType, Action<SettingEntryBase> onGuiDrawer)
        {
            if (settingType == null) throw new ArgumentNullException(nameof(settingType));
            if (onGuiDrawer == null) throw new ArgumentNullException(nameof(onGuiDrawer));

            if (SettingFieldDrawer.SettingDrawHandlers.ContainsKey(settingType))
                Logger.LogWarning("Tried to add a setting drawer for type " + settingType.FullName + " while one already exists.");
            else
                SettingFieldDrawer.SettingDrawHandlers[settingType] = onGuiDrawer;
        }

        /// <summary>
        /// Rebuild the setting list. Use to update the config manager window if config settings were removed or added while it was open.
        /// </summary>
        public void BuildSettingList()
        {
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings, _showDebug);

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();

            BuildFilteredSettingList();
        }

        private void BuildFilteredSettingList()
        {
            IEnumerable<SettingEntryBase> results = _allSettings;

            var searchStrings = SearchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (searchStrings.Length > 0)
            {
                results = results.Where(x => ContainsSearchString(x, searchStrings));
            }
            else
            {
                if (!_showAdvanced.Value)
                    results = results.Where(x => x.IsAdvanced != true);
                if (!_showKeybinds.Value)
                    results = results.Where(x => !IsKeyboardShortcut(x));
                if (!_showSettings.Value)
                    results = results.Where(x => x.IsAdvanced == true || IsKeyboardShortcut(x));
            }

            var settingsAreCollapsed = _pluginConfigCollapsedDefault.Value;

            var nonDefaultCollpasingStateByPluginName = new HashSet<string>();
            foreach (var pluginSetting in _filteredSetings)
            {
                if (pluginSetting.Collapsed != settingsAreCollapsed)
                {
                    nonDefaultCollpasingStateByPluginName.Add(pluginSetting.Info.Name);
                }
            }

            _filteredSetings = results
                .GroupBy(x => x.PluginInfo)
                .Select(pluginSettings =>
                {
                    var originalCategoryOrder = pluginSettings.Select(x => x.Category).Distinct().ToList();

                    var categories = pluginSettings
                        .GroupBy(x => x.Category)
                        .OrderBy(x => originalCategoryOrder.IndexOf(x.Key))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { Name = x.Key, Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList() });

                    var website = Utils.GetWebsite(pluginSettings.First().PluginInstance);

                    return new PluginSettingsData
                    {
                        Info = pluginSettings.Key,
                        Categories = categories.ToList(),
                        Collapsed = nonDefaultCollpasingStateByPluginName.Contains(pluginSettings.Key.Name) ? !settingsAreCollapsed : settingsAreCollapsed,
                        Website = website
                    };
                })
                .OrderBy(x => x.Info.Name)
                .ToList();
        }

        private static bool IsKeyboardShortcut(SettingEntryBase x)
        {
            return x.SettingType == typeof(KeyboardShortcut) || x.SettingType == typeof(KeyCode);
        }

        private static bool ContainsSearchString(SettingEntryBase setting, string[] searchStrings)
        {
            var combinedSearchTarget = setting.PluginInfo.Name + "\n" +
                                       setting.PluginInfo.GUID + "\n" +
                                       setting.DispName + "\n" +
                                       setting.Category + "\n" +
                                       setting.Description + "\n" +
                                       setting.DefaultValue + "\n" +
                                       setting.Get();

            return searchStrings.All(s => combinedSearchTarget.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private void CalculateWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 560 ? Screen.height : Screen.height - 100;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            SettingWindowRect = new Rect(offsetX, offsetY, width, height);

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            LeftColumnWidth = Mathf.RoundToInt(SettingWindowRect.width / 2.5f);
            RightColumnWidth = (int)SettingWindowRect.width - LeftColumnWidth - 115;

            _windowWasMoved = false;
        }

        private void OnGUI()
        {
            try
            {
                if (DisplayingWindow)
                {
                    _isRenderingGui = true;
                    SetUnlockCursor(CursorLockMode.None, true);

                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        DisplayingWindow = false;
                        _isRenderingGui = false;
                        Event.current.Use();
                        return;
                    }

                    Vector2 mousePosition = Input.mousePosition;
                    mousePosition.y = Screen.height - mousePosition.y;

                    // If the window hasn't been moved by the user yet, block the whole screen
                    if (!_windowWasMoved)
                    {
                        // Use a transparent style for the background button
                        if (GUI.Button(_screenRect, string.Empty, GUIStyle.none))
                        {
                            if (!SettingWindowRect.Contains(mousePosition))
                            {
                                DisplayingWindow = false;
                                _isRenderingGui = false;
                                return;
                            }
                        }
                    }

                    var newRect = GUILayout.Window(WindowId, SettingWindowRect, (GUI.WindowFunction)SettingsWindow, "Plugin / mod settings");

                    // Clear focus if we clicked inside the window but not on a specific control
                    if (Event.current.type == EventType.MouseDown && SettingWindowRect.Contains(mousePosition))
                    {
                        if (GUIUtility.hotControl == 0)
                        {
                            GUI.FocusControl(null);
                        }
                    }
                    _isRenderingGui = false;
                }
            }
            catch (Exception ex)
            {
                _isRenderingGui = false;
                Logger.LogError("Error in OnGUI: " + ex);
            }
        }

        private static void DrawTooltip(Rect area)
        {
            string tooltip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tooltip))
            {
                var style = GUI.skin.box.CreateCopy();
                style.wordWrap = true;
                style.alignment = TextAnchor.MiddleCenter;

                GUIContent content = new GUIContent(tooltip);

                const int width = 400;
                var height = style.CalcHeight(content, 400) + 10;

                var mousePosition = Event.current.mousePosition;

                var x = mousePosition.x + width > area.width
                    ? area.width - width
                    : mousePosition.x;

                var y = mousePosition.y + 25 + height > area.height
                    ? mousePosition.y - height
                    : mousePosition.y + 25;

                Rect position = new Rect(x, y, width, height);
                ImguiUtils.DrawContolBackground(position, Color.black);
                style.Draw(position, content, -1);
            }
        }

        private void SettingsWindow(int id)
        {
            try
            {
                DrawWindowHeader();

                _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true);

                var scrollPosition = _settingWindowScrollPos.y;
                var scrollHeight = SettingWindowRect.height;

                GUILayout.BeginVertical();
                {
                    if (string.IsNullOrEmpty(SearchString))
                    {
                        DrawTips();

                        if (_tipsHeight == 0 && Event.current.type == EventType.Repaint)
                            _tipsHeight = (int)GUILayoutUtility.GetLastRect().height;
                    }

                    var currentHeight = _tipsHeight;

                    foreach (var plugin in _filteredSetings)
                    {
                        var visible = plugin.Height == 0 || currentHeight + plugin.Height >= scrollPosition && currentHeight <= scrollPosition + scrollHeight;

                        if (visible)
                        {
                            try
                            {
                                DrawSinglePlugin(plugin);
                            }
                            catch (ArgumentException)
                            {
                                // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                            }

                            if (plugin.Height == 0 && Event.current.type == EventType.Repaint)
                                plugin.Height = (int)GUILayoutUtility.GetLastRect().height;
                        }
                        else
                        {
                            try
                            {
                                GUILayout.Space(plugin.Height);
                            }
                            catch (ArgumentException)
                            {
                                // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                            }
                        }

                        currentHeight += plugin.Height;
                    }

                    if (_showDebug)
                    {
                        GUILayout.Space(10);
                        GUILayout.Label("Plugins with no options available: " + _modsWithoutSettings);
                    }
                    else
                    {
                        // Always leave some space in case there's a dropdown box at the very bottom of the list
                        GUILayout.Space(70);
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndScrollView();

                if (!SettingFieldDrawer.DrawCurrentDropdown())
                    DrawTooltip(SettingWindowRect);

                GUI.DragWindow();
            }
            catch (Exception ex)
            {
                GUILayout.Label("CRASH IN SettingsWindow: " + ex.Message);
                Logger.LogError("SettingsWindow crash: " + ex);
            }
        }

        private void DrawTips()
        {
            var tip = !_tipsPluginHeaderWasClicked ? "Tip: Click plugin names to expand. Click setting and group names to see their descriptions." :
                !_tipsWindowWasMoved ? "Tip: You can drag this window to move it. It will stay open while you interact with the game." : null;

            if (tip != null)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(tip);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawWindowHeader()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUI.enabled = SearchString == string.Empty;

                var newVal = GUILayout.Toggle(_showSettings.Value, "Normal settings");
                if (_showSettings.Value != newVal)
                {
                    _showSettings.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUILayout.Toggle(_showKeybinds.Value, "Keyboard shortcuts");
                if (_showKeybinds.Value != newVal)
                {
                    _showKeybinds.Value = newVal;
                    BuildFilteredSettingList();
                }

                var origColor = GUI.color;
                GUI.color = _advancedSettingColor;
                newVal = GUILayout.Toggle(_showAdvanced.Value, "Advanced settings");
                if (_showAdvanced.Value != newVal)
                {
                    _showAdvanced.Value = newVal;
                    BuildFilteredSettingList();
                }
                GUI.color = origColor;

                GUI.enabled = true;

                GUILayout.Space(8);

                newVal = GUILayout.Toggle(_showDebug, "Debug info");
                if (_showDebug != newVal)
                {
                    _showDebug = newVal;
                    BuildSettingList();
                }

                if (GUILayout.Button("Open Log"))
                {
                    try { Utils.OpenLog(); }
                    catch (SystemException ex) { Logger.Log(LogLevel.Message | LogLevel.Error, ex.Message); }
                }

                GUILayout.Space(8);

                if (GUILayout.Button("Close"))
                {
                    DisplayingWindow = false;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUILayout.Label("Search: ", GUILayout.ExpandWidth(false));

                GUI.SetNextControlName(SearchBoxName);
                SearchString = GUILayout.TextField(SearchString, GUILayout.ExpandWidth(true));

                if (_focusSearchBox)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(SearchBoxName);
                    _focusSearchBox = false;
                }

                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                    SearchString = string.Empty;

                GUILayout.Space(8);

                if (GUILayout.Button(_pluginConfigCollapsedDefault.Value ? "Expand All" : "Collapse All", GUILayout.ExpandWidth(false)))
                {
                    var newValue = !_pluginConfigCollapsedDefault.Value;
                    _pluginConfigCollapsedDefault.Value = newValue;
                    foreach (var plugin in _filteredSetings)
                        plugin.Collapsed = newValue;

                    _tipsPluginHeaderWasClicked = true;
                }
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// String currently entered into the search box
        /// </summary>
        public string SearchString
        {
            get => _searchString;
            private set
            {
                if (value == null)
                    value = string.Empty;

                if (_searchString == value)
                    return;

                _searchString = value;

                BuildFilteredSettingList();
            }
        }

        private void DrawSinglePlugin(PluginSettingsData plugin)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            var categoryHeader = _showDebug ?
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}", null, "GUID: " + plugin.Info.GUID) :
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}");

            var isSearching = !string.IsNullOrEmpty(SearchString);

            {
                var hasWebsite = plugin.Website != null;
                if (hasWebsite)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(29); // Same as the URL button to keep the plugin name centered
                }

                if (SettingFieldDrawer.DrawPluginHeader(categoryHeader, plugin.Collapsed && !isSearching) && !isSearching)
                {
                    _tipsPluginHeaderWasClicked = true;
                    plugin.Collapsed = !plugin.Collapsed;
                }

                if (hasWebsite)
                {
                    var origColor = GUI.color;
                    GUI.color = Color.gray;
                    if (GUILayout.Button(new GUIContent("URL", null, plugin.Website), GUI.skin.label, GUILayout.ExpandWidth(false)))
                        Utils.OpenWebsite(plugin.Website);
                    GUI.color = origColor;
                    GUILayout.EndHorizontal();
                }
            }

            if (isSearching || !plugin.Collapsed)
            {
                foreach (var category in plugin.Categories)
                {
                    if (!string.IsNullOrEmpty(category.Name))
                    {
                        if (plugin.Categories.Count > 1 || !_hideSingleSection.Value)
                            SettingFieldDrawer.DrawCategoryHeader(category.Name);
                    }

                    foreach (var setting in category.Settings)
                    {
                        DrawSingleSetting(setting);
                        GUILayout.Space(2);
                    }
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawSingleSetting(SettingEntryBase setting)
        {
            GUILayout.BeginHorizontal();
            {
                try
                {
                    DrawSettingName(setting);
                    _fieldDrawer.DrawSettingValue(setting);
                    DrawDefaultButton(setting);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"Failed to draw setting {setting.DispName} - {ex}");
                    GUILayout.Label("Failed to draw this field, check log for details.");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSettingName(SettingEntryBase setting)
        {
            if (setting.HideSettingName) return;

            var origColor = GUI.color;
            if (setting.IsAdvanced == true)
                GUI.color = _advancedSettingColor;

            GUILayout.Label(new GUIContent(setting.DispName.TrimStart('!'), null, setting.Description),
                GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));

            GUI.color = origColor;
        }

        private static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;

            object defaultValue = setting.DefaultValue;
            if (defaultValue != null || setting.SettingType.IsClass)
            {
                GUILayout.Space(5);
                if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                    setting.Set(defaultValue);
            }
        }

        private void Start()
        {
            _screenShowCursor = typeof(Screen).GetProperty("showCursor", BindingFlags.Static | BindingFlags.Public);

            try
            {
                var pmGuiType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .FirstOrDefault(t => t.Name == "PlayMakerGUI");

                if (pmGuiType != null)
                {
                    _pmGuiLockCursor = pmGuiType.GetProperty("LockCursor", BindingFlags.Static | BindingFlags.Public);
                    _pmGuiHideCursor = pmGuiType.GetProperty("HideCursor", BindingFlags.Static | BindingFlags.Public);
                    _pmGuiLockCursorField = pmGuiType.GetField("LockCursor", BindingFlags.Static | BindingFlags.Public);
                    _pmGuiHideCursorField = pmGuiType.GetField("HideCursor", BindingFlags.Static | BindingFlags.Public);

                    PlayMakerGUIPatch.Patch(new Harmony(GUID + ".pmgui"), pmGuiType);
                }
            }
            catch (Exception ex) { Logger.LogDebug("Failed to find PlayMakerGUI: " + ex.Message); }

            // Check if user has permissions to write config files to disk
            try { Config.Save(); }
            catch (IOException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Failed to write to config directory, expect issues!\nError message:" + ex.Message); }
            catch (UnauthorizedAccessException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Permission denied to write to config directory, expect issues!\nError message:" + ex.Message); }
        }

        private void Update()
        {
            try
            {
                bool toggle = _keybind.Value.IsDown();
                if (!toggle && OverrideHotkey) toggle = true;

                if (toggle)
                {
                    DisplayingWindow = !DisplayingWindow;
                }

                if (DisplayingWindow)
                {
                    SetUnlockCursor(CursorLockMode.None, true);

                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        if (SettingFieldDrawer.SettingKeyboardShortcut)
                            SettingFieldDrawer.CancelSettingShortcut();
                        else
                            DisplayingWindow = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Update: " + ex);
            }
        }

        private void DisableCameraComponents()
        {
            try
            {
                var objectsToSearch = new List<GameObject>();
                if (Camera.main != null) objectsToSearch.Add(Camera.main.gameObject);
                var playerGo = GameObject.Find("PLAYER");
                if (playerGo != null) objectsToSearch.Add(playerGo);

                foreach (var go in objectsToSearch)
                {
                    var behaviours = go.GetComponentsInChildren<Behaviour>(true);
                    foreach (var comp in behaviours)
                    {
                        if (comp == null || !comp.enabled) continue;
                        string name = comp.GetType().Name;
                        if (name.Contains("MouseLook") || name == "MainCamera")
                        {
                            comp.enabled = false;
                            _disabledCameraComponents.Add(comp);
                        }
                    }
                }
            }
            catch { }
        }

        private void LateUpdate()
        {
            if (DisplayingWindow)
            {
                SetUnlockCursor(CursorLockMode.None, true);
            }
        }

        private void SetUnlockCursor(CursorLockMode lockState, bool cursorVisible)
        {
            try
            {
                Cursor.lockState = lockState;
                Cursor.visible = cursorVisible;
#pragma warning disable CS0618 // Type or member is obsolete
                Screen.lockCursor = lockState != CursorLockMode.None;
#pragma warning restore CS0618 // Type or member is obsolete
                _screenShowCursor?.SetValue(null, cursorVisible, null);

                if (lockState == CursorLockMode.None && cursorVisible)
                {
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    _pmGuiLockCursor?.SetValue(null, false, null);
                    _pmGuiHideCursor?.SetValue(null, false, null);
                    _pmGuiLockCursorField?.SetValue(null, false);
                    _pmGuiHideCursorField?.SetValue(null, false);
                }
            }
            catch { }
        }

        private sealed class PluginSettingsData
        {
            public BepInPlugin Info;
            public List<PluginSettingsGroupData> Categories;
            public int Height;
            public string Website;

            private bool _collapsed;
            public bool Collapsed
            {
                get => _collapsed;
                set
                {
                    _collapsed = value;
                    Height = 0;
                }
            }

            public sealed class PluginSettingsGroupData
            {
                public string Name;
                public List<SettingEntryBase> Settings;
            }
        }
    }

    [HarmonyPatch(typeof(Cursor))]
    internal static class CursorPatch
    {
        [HarmonyPatch(nameof(Cursor.visible), MethodType.Setter)]
        [HarmonyPrefix]
        public static void PrefixVisible(ref bool value)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                value = true;
        }

        [HarmonyPatch(nameof(Cursor.lockState), MethodType.Setter)]
        [HarmonyPrefix]
        public static void PrefixLockState(ref CursorLockMode value)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                value = CursorLockMode.None;
        }
    }

    [HarmonyPatch(typeof(Screen))]
    internal static class ScreenPatch
    {
        [HarmonyPatch("lockCursor", MethodType.Setter)]
        [HarmonyPrefix]
        public static void PrefixLockCursor(ref bool value)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                value = false;
        }

        [HarmonyPatch("showCursor", MethodType.Setter)]
        [HarmonyPrefix]
        public static void PrefixShowCursor(ref bool value)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                value = true;
        }
    }

    [HarmonyPatch(typeof(Cursor))]
    internal static class CursorSetCursorPatch
    {
        [HarmonyPatch(nameof(Cursor.SetCursor), typeof(Texture2D), typeof(Vector2), typeof(CursorMode))]
        [HarmonyPrefix]
        public static void Prefix(ref Texture2D __0)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                __0 = null;
        }
    }

    [HarmonyPatch(typeof(Input))]
    internal static class MousePositionPatch
    {
        [HarmonyPatch(nameof(Input.mousePosition), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool Prefix(ref Vector3 __result)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
            {
                if (!ConfigurationManager._isRenderingGui)
                {
                    __result = ConfigurationManager._frozenMousePos;
                    return false;
                }
            }
            return true;
        }
    }

    internal static class PlayMakerGUIPatch
    {
        public static void Patch(Harmony harmony, Type pmGuiType)
        {
            try
            {
                var lockCursorSetter = pmGuiType.GetProperty("LockCursor", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod();
                if (lockCursorSetter != null)
                {
                    harmony.Patch(lockCursorSetter, new HarmonyMethod(typeof(PlayMakerGUIPatch), nameof(PrefixLockCursor)));
                }

                var hideCursorSetter = pmGuiType.GetProperty("HideCursor", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod();
                if (hideCursorSetter != null)
                {
                    harmony.Patch(hideCursorSetter, new HarmonyMethod(typeof(PlayMakerGUIPatch), nameof(PrefixHideCursor)));
                }
            }
            catch (Exception ex) { ConfigurationManager.Logger.LogDebug("Failed to patch PlayMakerGUI: " + ex.Message); }
        }

        public static void PrefixLockCursor(ref bool value)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                value = false;
        }

        public static void PrefixHideCursor(ref bool value)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                value = false;
        }
    }

    [HarmonyPatch(typeof(Input))]
    internal static class InputPatch
    {
        [HarmonyPatch(nameof(Input.GetAxis))]
        [HarmonyPrefix]
        public static bool PrefixGetAxis(string axisName, ref float __result)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
            {
                if (axisName != null && axisName.StartsWith("Mouse"))
                {
                    __result = 0f;
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(nameof(Input.GetAxisRaw))]
        [HarmonyPrefix]
        public static bool PrefixGetAxisRaw(string axisName, ref float __result)
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
            {
                if (axisName != null && axisName.StartsWith("Mouse"))
                {
                    __result = 0f;
                    return false;
                }
            }
            return true;
        }

        public static void PatchCInput(Harmony harmony, Type cInputType)
        {
            try
            {
                var getAxis = cInputType.GetMethod("GetAxis", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
                if (getAxis != null) harmony.Patch(getAxis, new HarmonyMethod(typeof(InputPatch), nameof(PrefixGetAxis)));

                var getAxisRaw = cInputType.GetMethod("GetAxisRaw", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
                if (getAxisRaw != null) harmony.Patch(getAxisRaw, new HarmonyMethod(typeof(InputPatch), nameof(PrefixGetAxisRaw)));
            }
            catch { }
        }
    }

    internal static class MouseLookPatch
    {
        public static bool Prefix()
        {
            if (ConfigurationManager.Instance != null && ConfigurationManager.Instance.DisplayingWindow)
                return false;
            return true;
        }
    }
}
