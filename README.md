# Configuration Manager for My Winter Car
A specialized version of the BepInEx Configuration Manager, tailored for **My Winter Car**.

This tool provides an easy way for users to configure plugin settings in-game without needing to create a custom GUI. It automatically supports all BepInEx plugin configurations, including keyboard shortcuts.

The manager is accessed in-game by pressing **F6** (default). This can be changed in the configuration file or within the manager itself.

![Configuration manager](Screenshot.PNG)

### Key Features for My Winter Car:
- **Forced Mouse Usage:** The mouse cursor is automatically enabled and unlocked whenever the manager window is open.
- **Input & Camera Blocking:** Prevents the player camera and other mouse-look scripts from moving while the menu is open.
- **Robust Input Fields:** Numerical and text inputs require **Enter** to commit, preventing accidental changes and handling different regional decimal formats.
- **Lightweight:** Optimized for MWC's Mono environment with all unnecessary overhead removed.

## Installation
- Ensure you have **BepInEx 5** installed in your My Winter Car directory.
- Download the `ConfigurationManager.dll`.
- Place the `.dll` into your `BepInEx\plugins` folder.
- Start the game and press **F6** to open the menu.

## Changelog
### v18.4.4
- **Game Pausing Reverted:** The game no longer pauses when the manager is open to avoid physics issues. Only camera and player movement are now blocked.
- **Improved Camera & Input Blocking:**
  - Mouse position is "frozen" while the GUI is open to prevent the game from reacting to mouse movements.
  - Enhanced camera disabling logic: Actively disables all camera scripts and controllers while the menu is open.
  - Comprehensive input blocking for mouse axes and buttons to prevent interaction leakage.
- **Keybind Cleanup:** Removed hardcoded F1 and F5 keybinds. **F6** is now the sole default keybind (customizable).
- **Better Input Fields:**
  - Numerical and text input fields now require pressing **Enter** to commit changes, preventing accidental updates while typing.
  - Added visual error feedback (red text) for invalid input formats.
  - Automatically handles decimal separator differences (comma vs. dot).
- **Bug Fixes:** 
  - Fixed an issue where the camera would unlock after 1 second.
  - Fixed UI sync issue where reset values didn't update visually in the editor.
  - Resolved Harmony initialization errors on certain Unity 5.0 versions.

### v18.4.1
- **Specialized for My Winter Car:** Optimized for MWC's Unity version and Mono environment.
- **Mouse Fix:** Forced cursor visibility and unlocked state while the GUI is open.
- **Cleanup:** Removed IL2CPP support and other redundant BepInEx 6 features to reduce overhead.
- **Project Refactor:** Simplified codebase and project structure for easier maintenance and specialized builds.

## License
This project is licensed under the **GNU Lesser General Public License v3.0 (LGPL-3.0)**. 

- **Original Creator:** [MarC0 / ManlyMarco](https://github.com/ManlyMarco)
- **Original Project:** [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)

You can find a copy of the license in the [LICENSE](LICENSE) file. This version is a fork modified specifically for My Winter Car.

---

## Developer Info
ConfigurationManager will automatically display all settings from your plugin's `Config`. All metadata (e.g. description, value range) will be used by ConfigurationManager to display the settings to the user.

### Basic Usage
```c#
// Simple boolean toggle
var enabled = Config.Bind("General", "Enabled", true, "Enable or disable the plugin");

// String input
var name = Config.Bind("General", "Name", "Player", "Your name");

// Int/Float input (Requires Enter to commit in this version)
var speed = Config.Bind("Physics", "Speed", 10f, "Movement speed");

// Keybinds (KeyboardShortcut)
var myShortcut = Config.Bind("General", "My Shortcut", new KeyboardShortcut(KeyCode.G), "Press this to do magic");
```

### Advanced Config Types
You can use `AcceptableValueRange` to create sliders or `AcceptableValueList` for dropdowns.

```c#
// Slider (0 to 100)
var volume = Config.Bind("Audio", "Volume", 50f, new ConfigDescription("Volume level", new AcceptableValueRange<float>(0f, 100f)));

// Dropdown (Enum or List)
public enum MyEnum { First, Second, Third }
var selection = Config.Bind("General", "Selection", MyEnum.First, "Select an option");

// Manual Dropdown
var choice = Config.Bind("General", "Choice", "Option 1", new ConfigDescription("Pick one", new AcceptableValueList<string>("Option 1", "Option 2", "Option 3")));
```

### Custom Editors
You can provide a `CustomDrawer` in `ConfigurationManagerAttributes` to replace the default UI for a setting:
```c#
Config.Bind("Section", "Key", "Value", new ConfigDescription("Desc", null, new ConfigurationManagerAttributes { CustomDrawer = MyDrawer }));

static void MyDrawer(BepInEx.Configuration.ConfigEntryBase entry)
{
    GUILayout.Label(entry.BoxedValue.ToString(), GUILayout.ExpandWidth(true));
    if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
    {
        entry.BoxedValue = entry.DefaultValue;
    }
}
```