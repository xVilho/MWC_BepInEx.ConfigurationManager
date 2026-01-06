## Configuration Manager for My Winter Car
A specialized version of the BepInEx Configuration Manager, tailored for **My Winter Car**.

This tool provides an easy way for users to configure plugin settings in-game without needing to create a custom GUI. It automatically supports all BepInEx plugin configurations, including keyboard shortcuts.

The manager is accessed in-game by pressing **F1** (default).

![Configuration manager](Screenshot.PNG)

### Key Features for My Winter Car:
- **Forced Mouse Usage:** The mouse cursor is automatically enabled and unlocked whenever the manager window is open, resolving input issues in My Winter Car.
- **Lightweight:** Stripped down unnecessary features like IL2CPP support to ensure maximum compatibility and performance for MWC's Mono environment.
- **Improved Input Handling:** Direct integration with Unity's legacy input and cursor systems to ensure reliable behavior in MWC's specific Unity version.

## Installation
- Ensure you have **BepInEx 5** installed in your My Winter Car directory.
- Download the `ConfigurationManager.dll`.
- Place the `.dll` into your `BepInEx\plugins` folder.
- Start the game and press **F1** to open the menu.

## Changelog
### v18.4.1 (MWC Edition)
- **Specialized for My Winter Car:** Optimized for MWC's Unity version and Mono environment.
- **Mouse Fix:** Forced cursor visibility and unlocked state while the GUI is open.
- **Cleanup:** Removed IL2CPP support and other redundant BepInEx 6 features to reduce overhead.
- **Project Refactor:** Simplified codebase and project structure for easier maintenance and specialized builds.

## Credits
- **Original Creator:** [MarC0 / ManlyMarco](https://github.com/ManlyMarco)
- **Original Project:** [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)
- This version is a fork modified specifically for My Winter Car.

---

## Developer Info
ConfigurationManager will automatically display all settings from your plugin's `Config`. All metadata (e.g. description, value range) will be used by ConfigurationManager to display the settings to the user.

### Customization
You can change how a setting is shown by using the `ConfigurationManagerAttributes` class. Copy [ConfigurationManagerAttributes.cs](ConfigurationManagerAttributes.cs) into your project to use it.

Example of overriding order and marking as advanced:
```c#
Config.Bind("X", "1", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 }));
```

### Custom Editors
You can provide a `CustomDrawer` in `ConfigurationManagerAttributes` to replace the default UI for a setting:
```c#
Config.Bind("Section", "Key", "Value", new ConfigDescription("Desc", null, new ConfigurationManagerAttributes { CustomDrawer = MyDrawer }));

static void MyDrawer(BepInEx.Configuration.ConfigEntryBase entry)
{
    GUILayout.Label(entry.BoxedValue.ToString(), GUILayout.ExpandWidth(true));
}
```