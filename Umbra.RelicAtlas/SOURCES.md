# API references

Inspected on 21 September 2026. Built against installed Umbra **3.1.18.0** and
Dalamud **15.0.3.5**, API 15, on .NET 10.

- [Official custom-plugin documentation](https://github.com/una-xiv/Umbra.SamplePlugin): DLL loading through Umbra's Plugins settings, host assembly forwarding, and the widget constructor/attribute contract.
- [StandardToolbarWidget](https://github.com/una-xiv/umbra/blob/cbd00524952aa1c0386a47126f05721191f80033/Umbra/src/Toolbar/Widgets/System/Types/StandardToolbarWidget.cs): text, secondary label, icon, tooltip, progress and configuration APIs.
- [WidgetRegistry](https://github.com/una-xiv/umbra/blob/cbd00524952aa1c0386a47126f05721191f80033/Umbra/src/Toolbar/Widgets/System/WidgetRegistry.cs): custom widget discovery.
- [PluginLoadContext](https://github.com/una-xiv/umbra/blob/cbd00524952aa1c0386a47126f05721191f80033/Umbra/src/Plugins/PluginLoadContext.cs): forwarded references and version checks.
- [Plugins settings](https://github.com/una-xiv/umbra/blob/cbd00524952aa1c0386a47126f05721191f80033/Umbra/src/Windows/Library/Settings/Modules/SettingsWindowPluginsModule.cs): Install from file workflow and restart requirement.

The companion implements the supported widget API with new code. It ships no
Umbra, Dalamud, game or drawing-library binaries. Umbra is AGPL-3.0-or-later; the
extension uses the same license, and a copy of that license is included.
