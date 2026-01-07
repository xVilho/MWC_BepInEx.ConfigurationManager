Configuration Manager for My Winter Car
Version 18.4.4
------------------------------------------
A specialized version of the BepInEx Configuration Manager, tailored for My Winter Car.

This tool provides an easy way for users to configure plugin settings in-game 
without needing to create a custom GUI. It automatically supports all BepInEx 
plugin configurations, including keyboard shortcuts.

The manager is accessed in-game by pressing F6 (default). This can be changed 
in the configuration file or within the manager itself.

Key Features for My Winter Car:
-------------------------------
- Forced Mouse Usage: The mouse cursor is automatically enabled and unlocked whenever the manager window is open.
- Input & Camera Blocking: Prevents the player camera and other mouse-look scripts from moving while the menu is open.
- Robust Input Fields: Numerical and text inputs require Enter to commit, preventing accidental changes and handling different regional decimal formats.
- Lightweight: Optimized for MWC's Mono environment with all unnecessary overhead removed.

Installation:
-------------
1. Ensure you have BepInEx 5 installed in your My Winter Car directory.
2. Place ConfigurationManager.dll into your BepInEx\plugins folder.
3. Start the game and press F6 to open the menu.

License:
--------
This project is licensed under the GNU Lesser General Public License v3.0 (LGPL-3.0).

Original Creator: MarC0 / ManlyMarco (https://github.com/ManlyMarco)
Original Project: BepInEx.ConfigurationManager (https://github.com/BepInEx/BepInEx.ConfigurationManager)

This version is a fork modified specifically for My Winter Car.
Source Code: https://github.com/xVilho/MWC_BepInEx.ConfigurationManager

Changelog:
----------
v18.4.4
- Game Pausing Reverted: The game no longer pauses when the manager is open to avoid physics issues. Only camera and player movement are now blocked.
- Improved Camera & Input Blocking:
  - Mouse position is "frozen" while the GUI is open to prevent the game from reacting to mouse movements.
  - Enhanced camera disabling logic: Actively disables all camera scripts and controllers while the menu is open.
  - Comprehensive input blocking for mouse axes and buttons to prevent interaction leakage.
- Keybind Cleanup: Removed hardcoded F1 and F5 keybinds. F6 is now the sole default keybind (customizable).
- Better Input Fields:
  - Numerical and text input fields now require pressing Enter to commit changes, preventing accidental updates while typing.
  - Added visual error feedback (red text) for invalid input formats.
  - Automatically handles decimal separator differences (comma vs. dot).
- Bug Fixes: 
  - Fixed an issue where the camera would unlock after 1 second.
  - Fixed UI sync issue where reset values didn't update visually in the editor.
  - Resolved Harmony initialization errors on certain Unity 5.0 versions.

v18.4.1 (MWC Edition)
- Specialized for My Winter Car: Optimized for MWC's Unity version and Mono environment.
- Mouse Fix: Forced cursor visibility and unlocked state while the GUI is open.
- Cleanup: Removed IL2CPP support and other redundant BepInEx 6 features to reduce overhead.
- Project Refactor: Simplified codebase and project structure for easier maintenance and specialized builds.
