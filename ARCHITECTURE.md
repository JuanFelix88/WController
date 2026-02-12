# WController Architecture

This document provides a comprehensive overview of WController's technical architecture, code organization, and implementation details.

## 🏗️ High-Level Architecture

WController is built as a Windows Forms application using .NET Framework 4.8. The application follows a layered architecture with clear separation of concerns.

```
┌─────────────────────────────────────────────────┐
│                 Presentation Layer              │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────┐ │
│  │  MainForm   │  │ SearchForm  │  │ Rename   │ │
│  │             │  │             │  │ Window   │ │
│  └─────────────┘  └─────────────┘  └──────────┘ │
├─────────────────────────────────────────────────┤
│                 Business Logic Layer            │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────┐ │
│  │   Window    │  │    File     │  │ Desktop  │ │
│  │ Management  │  │  Indexing   │  │ Helper   │ │
│  └─────────────┘  └─────────────┘  └──────────┘ │
├─────────────────────────────────────────────────┤
│                 Utility Layer                   │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────┐ │
│  │   Colors    │  │    Text     │  │   Icon   │ │
│  │   Helper    │  │  Processing │  │ Helper   │ │
│  └─────────────┘  └─────────────┘  └──────────┘ │
├─────────────────────────────────────────────────┤
│                 System Integration              │
│          Windows API (User32, GDI32)           │
└─────────────────────────────────────────────────┘
```

## 📁 Code Organization

### Core Components

#### MainForm.cs (1,019 lines)
**Purpose**: Primary application window and main entry point for user interactions.

**Key Responsibilities**:
- Window enumeration and display
- Hotkey registration and handling
- UI composition and theming
- Window manipulation (minimize, restore, focus)
- Integration with other forms

**Important Methods**:
```csharp
// Window enumeration
private void RefreshWindowList()
private bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)

// Hotkey management
private void RegisterGlobalHotkeys()
private void UnregisterGlobalHotkeys()

// Window operations
private void MinimizeWindow(IntPtr hWnd)
private void RestoreWindow(IntPtr hWnd)
private void SetWindowFocus(IntPtr hWnd)
```

**Windows API Integration**:
- `EnumWindows`: Enumerate all top-level windows
- `GetWindowText`: Retrieve window titles
- `RegisterHotKey`: Register global hotkeys
- `ShowWindow`: Control window visibility states
- `SetForegroundWindow`: Bring windows to foreground

#### SearchItemsForm.cs (392 lines)
**Purpose**: Advanced search interface for finding windows and items.

**Key Features**:
- Real-time search with filtering
- Keyboard navigation
- Integration with file indexing
- Search result highlighting

**Search Algorithm**:
```csharp
// Fuzzy search implementation
private List<SearchResult> PerformSearch(string query)
{
    // 1. Exact matches (highest priority)
    // 2. Starts-with matches
    // 3. Contains matches
    // 4. Fuzzy matches (character sequence matching)
}
```

#### RenameWindow.cs (79 lines)
**Purpose**: Dialog for renaming window titles.

**Functionality**:
- Custom window title assignment
- Input validation
- Integration with window management system

#### FileIndexes.cs (123 lines)
**Purpose**: File system indexing for enhanced search capabilities.

**Key Features**:
- Background file scanning
- Index persistence
- Fast lookup operations
- File type filtering

### Utility Classes

#### Util/Colors.cs
**Purpose**: Color management and Windows theme integration.

```csharp
public static class Colors
{
    public static Color GetAccentColor()
    public static Color GetSecondaryColor()
    public static Color GetBackgroundColor()
}
```

#### Util/IconHelper.cs
**Purpose**: Icon extraction and management for windows and files.

```csharp
public static class IconHelper
{
    public static Icon ExtractWindowIcon(IntPtr hWnd)
    public static Icon GetFileIcon(string filePath)
    public static Bitmap GetIconBitmap(Icon icon, Size size)
}
```

#### Util/Resizer.cs
**Purpose**: Window resizing and layout management utilities.

#### Util/Text.cs
**Purpose**: Text processing and formatting utilities.

### ShowDesktopHelper.cs (19 lines)
**Purpose**: Desktop management functionality.

**Features**:
- Show desktop functionality
- Desktop state management
- Integration with Windows shell

## 🔧 Technical Implementation Details

### Window Management System

#### Window Enumeration Process
```csharp
// 1. Call EnumWindows with callback
EnumWindows(EnumWindowsProc, IntPtr.Zero);

// 2. Filter windows in callback
private bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)
{
    if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
    {
        // Extract window information
        var windowInfo = new WindowInfo
        {
            Handle = hWnd,
            Title = GetWindowTitle(hWnd),
            ProcessId = GetWindowProcessId(hWnd),
            Icon = ExtractWindowIcon(hWnd)
        };
        
        // Add to window list
        _windows.Add(windowInfo);
    }
    
    return true; // Continue enumeration
}
```

#### Hotkey Registration System
```csharp
// Register global hotkeys
private void RegisterHotkeys()
{
    // Ctrl+Alt+W: Show/Hide main window
    RegisterHotKey(this.Handle, HOTKEY_MAIN, MOD_CONTROL | MOD_ALT, Keys.W);
    
    // Ctrl+Alt+S: Open search window
    RegisterHotKey(this.Handle, HOTKEY_SEARCH, MOD_CONTROL | MOD_ALT, Keys.S);
}

// Handle hotkey messages
protected override void WndProc(ref Message m)
{
    if (m.Msg == WM_HOTKEY)
    {
        int hotkeyId = m.WParam.ToInt32();
        HandleHotkey(hotkeyId);
    }
    
    base.WndProc(ref m);
}
```

### UI Architecture

#### Custom Rendering and Theming
```csharp
// Composited window rendering for smooth appearance
protected override CreateParams CreateParams
{
    get
    {
        CreateParams cp = base.CreateParams;
        cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
        return cp;
    }
}

// Rounded window corners
private void ApplyRoundedRegion(int radius)
{
    Rectangle bounds = this.ClientRectangle;
    using (GraphicsPath path = GetRoundedPath(bounds, radius))
    {
        this.Region = new Region(path);
    }
}
```

#### Dynamic Color Integration
```csharp
// Windows accent color integration
public static Color GetWindowsAccentColor()
{
    // Note: Currently hardcoded to dark theme colors
    // TODO: Implement actual Windows registry reading
    return Color.FromArgb(32, 32, 32);
    
    // Commented implementation for registry access:
    // var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
    // var value = key.GetValue("ColorizationColor");
    // return ParseColorFromDword((int)value);
}
```

### File Indexing System

#### Index Structure
```csharp
public class FileIndex
{
    public string FullPath { get; set; }
    public string Name { get; set; }
    public string Extension { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
    public string[] Keywords { get; set; }
}
```

#### Indexing Process
1. **Scan Directory Tree**: Recursively traverse file system
2. **Extract Metadata**: File name, size, modification date
3. **Generate Keywords**: Tokenize file names for search
4. **Build Index**: Create searchable data structure
5. **Persist Index**: Save to disk for fast startup

### Search Algorithm

#### Multi-tier Search Strategy
```csharp
public List<SearchResult> Search(string query)
{
    var results = new List<SearchResult>();
    
    // Tier 1: Exact matches (weight: 1.0)
    results.AddRange(FindExactMatches(query));
    
    // Tier 2: Prefix matches (weight: 0.8)
    results.AddRange(FindPrefixMatches(query));
    
    // Tier 3: Contains matches (weight: 0.6)
    results.AddRange(FindContainsMatches(query));
    
    // Tier 4: Fuzzy matches (weight: 0.4)
    results.AddRange(FindFuzzyMatches(query));
    
    // Sort by relevance score
    return results.OrderByDescending(r => r.Score).ToList();
}
```

## 🧵 Threading and Performance

### UI Thread Management
- **Main Thread**: All UI operations and Windows API calls
- **Background Threads**: File indexing and long-running operations
- **Thread Safety**: Proper use of `Invoke` for cross-thread UI updates

### Performance Considerations

#### Window Enumeration Optimization
- **Caching**: Cache window list and update incrementally
- **Filtering**: Early filtering of irrelevant windows
- **Lazy Loading**: Load window icons on-demand

#### Memory Management
- **Dispose Pattern**: Proper disposal of GDI resources
- **Icon Caching**: Reuse extracted icons
- **Event Cleanup**: Unregister event handlers and hotkeys

## 🔒 Security and Permissions

### Required Permissions
- **Window Access**: Read window information and titles
- **Global Hotkeys**: Register system-wide keyboard shortcuts
- **Process Information**: Access process details for window association
- **File System**: Read file system for indexing (optional)

### Security Considerations
- **Input Validation**: Validate all user inputs and file paths
- **Handle Validation**: Verify window handles before API calls
- **Error Handling**: Graceful handling of permission denied scenarios
- **Process Isolation**: Respect process boundaries and access controls

## 🔌 Extensibility Points

### Adding New Window Operations
```csharp
public interface IWindowOperation
{
    string Name { get; }
    bool CanExecute(WindowInfo window);
    void Execute(WindowInfo window);
}

// Example implementation
public class CloseWindowOperation : IWindowOperation
{
    public string Name => "Close Window";
    
    public bool CanExecute(WindowInfo window) => window.Handle != IntPtr.Zero;
    
    public void Execute(WindowInfo window)
    {
        PostMessage(window.Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }
}
```

### Adding New Search Providers
```csharp
public interface ISearchProvider
{
    string Name { get; }
    Task<List<SearchResult>> SearchAsync(string query);
}
```

### Custom Hotkey Actions
```csharp
public interface IHotkeyAction
{
    string Name { get; }
    Keys KeyCombination { get; }
    void Execute();
}
```

## 📊 Data Flow

### Typical User Interaction Flow
```
1. User Input (Hotkey/Click)
    ↓
2. Event Handler (MainForm)
    ↓
3. Business Logic (Window Manager)
    ↓
4. Windows API Call (User32/GDI32)
    ↓
5. Result Processing
    ↓
6. UI Update (Form Refresh)
```

### Search Operation Flow
```
1. Search Query Input
    ↓
2. Query Processing (Text normalization)
    ↓
3. Multi-tier Search Execution
    ↓
4. Result Ranking and Filtering
    ↓
5. UI Display with Highlighting
```

## 🚀 Future Architecture Improvements

### Potential Enhancements
1. **Plugin System**: Modular architecture for extensions
2. **Configuration Management**: Centralized settings system
3. **Async/Await Pattern**: More extensive use of async operations
4. **MVVM Pattern**: Separation of UI and business logic
5. **Dependency Injection**: Better testability and modularity
6. **Unit Testing Framework**: Comprehensive test coverage
7. **Logging System**: Structured logging for debugging and monitoring

### Performance Optimizations
1. **Window Handle Caching**: Reduce API calls for known windows
2. **Incremental Indexing**: Update index instead of full rebuild
3. **Virtual UI**: Virtualize large lists for better performance
4. **Background Processing**: Move more operations to background threads

---

This architecture documentation provides a foundation for understanding and extending WController. For specific implementation details, refer to the source code and inline documentation.