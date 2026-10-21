# WController

A powerful Windows desktop application for advanced window management and control. WController (formerly WindowsController) provides enhanced window manipulation capabilities with a modern, intuitive interface.

Preview:
![WController Preview](./WController/Resources/example-01.gif)

## 🌟 Features

### Core Functionality
- **Window Management**: Advanced control over all open windows on your desktop
- **Smart Search**: Quickly find and switch between windows using intelligent search
- **Window Renaming**: Customize window titles for better organization
- **File Indexing**: Fast file and folder indexing for improved productivity
- **Desktop Helper**: Convenient desktop management utilities

### User Interface
- **Modern Design**: Clean, rounded interface with Windows accent color integration
- **Hotkey Support**: Global keyboard shortcuts for quick access (Ctrl+Alt combinations)
- **Responsive UI**: Smooth, composited window rendering for optimal performance
- **Custom Theming**: Adaptive colors that match your Windows theme

## 🚀 Quick Start

### System Requirements
- Windows 10/11 (x64 or x86)
- .NET Framework 4.8 or later
- Administrative privileges (for some window management features)

### Installation
1. Download the latest release from the [Releases page](../../releases)
2. Extract the ZIP file to your preferred location
3. Run `WController.exe`
4. Grant necessary permissions when prompted

### Basic Usage
1. **Launch WController**: Double-click the executable or use the desktop shortcut
2. **Search Windows**: Use the search functionality to quickly find open windows
3. **Manage Windows**: Right-click on windows in the list for management options
4. **Rename Windows**: Select a window and use the rename feature to customize its title
5. **Use Hotkeys**: Configure and use global hotkeys for instant access

## 🔧 Configuration

### Hotkeys
Global shortcuts work even when WController is hidden:
- **Alt+F1**: enter positional window navigation. Release Alt, use the arrow keys or type a window-title prefix to move the highlight, Enter to focus, or Esc to cancel.
- **Alt+Oemtilde**: open/cycle the window picker (the key's symbol depends on the keyboard layout).
- **Alt+Space**: open/cycle search.
- **Ctrl+Alt+G / Ctrl+Alt+P**: Google/prompts search, when the corresponding plugins are installed.

Positional navigation uses non-minimized application windows on the current virtual desktop, across monitors. It prioritizes windows overlapping the selected window's vertical span for Left/Right, or horizontal span for Up/Down, then chooses the nearest center. Diagonals are a fallback when no aligned candidate exists; navigation does not wrap at the edge. Overlapping windows remain eligible. The rounded, hollow border uses the Windows accent color with per-pixel alpha antialiasing and does not change application colors or focus until Enter. External focus changes, closing a window outside the mode, or minimizing the selection cancel the mode. Keyboard input is consumed by the mode, not sent to the selected application. Windows may restrict interception or activation of elevated applications and secure desktops.

Typing searches native Windows titles by prefix, ignoring case and diacritics; aliases/renames are not used. The first matching window in Z-order is highlighted. The query appears in a small top-left tab integrated into the border, with black or white text chosen for maximum WCAG contrast. Backspace removes the last character; Ctrl+Backspace clears the entire query and pending accent without leaving the mode or changing the selection. Clearing the query removes the tab. With no match, the previous selection remains. Arrow keys still navigate spatially; Enter confirms and Esc exits.

Unmodified Del requests normal window closure (`WM_CLOSE`), never process termination. After the window closes, the nearest remaining eligible window is highlighted and the mode continues; it exits if none remain. A confirmation dialog taking focus exits the mode so it can be answered normally. Applications may refuse the request. Hold-to-repeat is disabled for Del: release and press again to close another window or retry.

### Window Management
- **Minimize/Restore**: Control window states programmatically
- **Focus Management**: Bring windows to foreground intelligently
- **Window Detection**: Automatically detect and track new windows

## 🏗️ For Developers

If you're interested in contributing or building from source, check out our development documentation:

- [**BUILD.md**](BUILD.md) - Build instructions and development setup
- [**CONTRIBUTING.md**](CONTRIBUTING.md) - Contribution guidelines and best practices
- [**ARCHITECTURE.md**](ARCHITECTURE.md) - Technical architecture and code structure

## 📋 License

This project is open source. Please see the license file for more details.

## 🤝 Contributing

We welcome contributions! Please read our [Contributing Guidelines](CONTRIBUTING.md) before submitting pull requests.

### Quick Contribution Steps
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📞 Support

- **Issues**: Report bugs or request features on our [Issues page](../../issues)
- **Discussions**: Join community discussions in [Discussions](../../discussions)
- **Documentation**: Check our comprehensive documentation in the docs folder

## 🔄 Version History

See [Releases](../../releases) for detailed version history and changelog.

---

**WController** - Empowering Windows users with advanced window management capabilities.