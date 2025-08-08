# WController API Reference

This document provides a comprehensive reference for the WController public APIs and interfaces.

## 📋 Table of Contents

- [Core Classes](#core-classes)
- [Data Models](#data-models)
- [Utility Classes](#utility-classes)
- [Windows API Wrappers](#windows-api-wrappers)
- [Extension Points](#extension-points)

## 🏗️ Core Classes

### MainForm Class

The primary application window and main entry point for user interactions.

#### Constructor
```csharp
public MainForm()
```
Initializes the main form with default settings and registers global hotkeys.

#### Public Methods

##### RefreshWindowList()
```csharp
public void RefreshWindowList()
```
Refreshes the list of available windows by enumerating all top-level windows.

**Usage:**
```csharp
var mainForm = new MainForm();
mainForm.RefreshWindowList();
```

##### ShowSearchWindow()
```csharp
public void ShowSearchWindow()
```
Displays the search window for finding windows and files.

##### ToggleMainWindow()
```csharp
public void ToggleMainWindow()
```
Shows or hides the main window based on its current visibility state.

#### Public Properties

##### CurrentWindowList
```csharp
public List<WindowInfo> CurrentWindowList { get; }
```
Gets the current list of windows displayed in the main interface.

### SearchItemsForm Class

Advanced search interface for finding windows and items.

#### Constructor
```csharp
public SearchItemsForm()
public SearchItemsForm(List<WindowInfo> windows)
```
Initializes the search form, optionally with a pre-populated window list.

#### Public Methods

##### PerformSearch(string query)
```csharp
public List<SearchResult> PerformSearch(string query)
```
Executes a search query against windows and indexed files.

**Parameters:**
- `query`: The search string to match against

**Returns:** List of search results ordered by relevance

**Usage:**
```csharp
var searchForm = new SearchItemsForm();
var results = searchForm.PerformSearch("notepad");
```

##### SetSearchFocus()
```csharp
public void SetSearchFocus()
```
Sets focus to the search input field.

### RenameWindow Class

Dialog for renaming window titles.

#### Constructor
```csharp
public RenameWindow(WindowInfo windowInfo)
```
Initializes the rename dialog for a specific window.

**Parameters:**
- `windowInfo`: The window to be renamed

#### Public Methods

##### ShowRenameDialog()
```csharp
public DialogResult ShowRenameDialog()
```
Displays the rename dialog and returns the user's choice.

**Returns:** DialogResult indicating OK or Cancel

### FileIndexes Class

File system indexing for enhanced search capabilities.

#### Constructor
```csharp
public FileIndexes()
public FileIndexes(string rootPath)
```
Initializes the file indexing system, optionally with a specific root path.

#### Public Methods

##### BuildIndex()
```csharp
public async Task BuildIndex()
```
Asynchronously builds the file index by scanning the file system.

##### SearchFiles(string query)
```csharp
public List<FileSearchResult> SearchFiles(string query)
```
Searches indexed files for the given query.

**Parameters:**
- `query`: Search term to match against file names and paths

**Returns:** List of matching files with relevance scores

##### AddPath(string path)
```csharp
public void AddPath(string path)
```
Adds a new path to be included in the index.

##### RemovePath(string path)
```csharp
public bool RemovePath(string path)
```
Removes a path from the index.

**Returns:** True if the path was successfully removed

## 📊 Data Models

### WindowInfo Class

Represents information about a window.

```csharp
public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; }
    public string ProcessName { get; set; }
    public uint ProcessId { get; set; }
    public Icon Icon { get; set; }
    public bool IsVisible { get; set; }
    public bool IsMinimized { get; set; }
    public Rectangle Bounds { get; set; }
}
```

#### Properties

- **Handle**: Native window handle (HWND)
- **Title**: Current window title
- **ProcessName**: Name of the owning process
- **ProcessId**: Process identifier
- **Icon**: Window icon (if available)
- **IsVisible**: Whether the window is currently visible
- **IsMinimized**: Whether the window is minimized
- **Bounds**: Window position and size

### SearchResult Class

Represents a search result with relevance scoring.

```csharp
public class SearchResult
{
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public object Data { get; set; }
    public double Score { get; set; }
    public SearchResultType Type { get; set; }
    public Icon Icon { get; set; }
}
```

#### Properties

- **Title**: Primary display text
- **Subtitle**: Secondary description text
- **Data**: Associated data object (WindowInfo, FileInfo, etc.)
- **Score**: Relevance score (0.0 to 1.0)
- **Type**: Type of search result (Window, File, etc.)
- **Icon**: Display icon

### FileSearchResult Class

Specialized search result for file system items.

```csharp
public class FileSearchResult : SearchResult
{
    public string FullPath { get; set; }
    public string Extension { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
}
```

## 🛠️ Utility Classes

### Colors Class

Color management and theme integration utilities.

#### Static Methods

##### GetAccentColor()
```csharp
public static Color GetAccentColor()
```
Gets the current Windows accent color.

**Returns:** System accent color

##### GetSecondaryColor()
```csharp
public static Color GetSecondaryColor()
```
Gets a complementary secondary color based on the accent color.

##### GetBackgroundColor()
```csharp
public static Color GetBackgroundColor()
```
Gets the appropriate background color for the current theme.

### IconHelper Class

Icon extraction and management utilities.

#### Static Methods

##### ExtractWindowIcon(IntPtr hWnd)
```csharp
public static Icon ExtractWindowIcon(IntPtr hWnd)
```
Extracts the icon associated with a window.

**Parameters:**
- `hWnd`: Window handle

**Returns:** Window icon or null if not available

##### GetFileIcon(string filePath)
```csharp
public static Icon GetFileIcon(string filePath)
```
Gets the icon associated with a file or file type.

**Parameters:**
- `filePath`: Path to the file

**Returns:** File type icon

##### ResizeIcon(Icon icon, Size size)
```csharp
public static Icon ResizeIcon(Icon icon, Size size)
```
Resizes an icon to the specified dimensions.

### Text Class

Text processing and formatting utilities.

#### Static Methods

##### NormalizeSearchText(string text)
```csharp
public static string NormalizeSearchText(string text)
```
Normalizes text for search operations (case, whitespace, etc.).

##### HighlightMatches(string text, string query)
```csharp
public static string HighlightMatches(string text, string query)
```
Returns HTML-formatted text with highlighted search matches.

##### Truncate(string text, int maxLength)
```csharp
public static string Truncate(string text, int maxLength)
```
Truncates text to the specified length with ellipsis.

### Resizer Class

Window resizing and layout management utilities.

#### Static Methods

##### GetScaledSize(Size originalSize, double scaleFactor)
```csharp
public static Size GetScaledSize(Size originalSize, double scaleFactor)
```
Calculates scaled dimensions based on DPI scaling factor.

##### GetDpiScaleFactor()
```csharp
public static double GetDpiScaleFactor()
```
Gets the current system DPI scaling factor.

## 🔌 Windows API Wrappers

### Window Management APIs

#### EnumWindows
```csharp
[DllImport("user32.dll")]
static extern bool EnumWindows(EnumWindowsPrc lpEnumFunc, IntPtr lParam);
```
Enumerates all top-level windows on the screen.

#### GetWindowText
```csharp
[DllImport("user32.dll", SetLastError = true)]
static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
```
Retrieves the text of a window's title bar.

#### ShowWindow
```csharp
[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
```
Sets the specified window's show state.

**Common nCmdShow values:**
- `SW_HIDE (0)`: Hides the window
- `SW_MINIMIZE (6)`: Minimizes the window
- `SW_RESTORE (9)`: Restores the window

#### SetForegroundWindow
```csharp
[DllImport("user32.dll")]
static extern bool SetForegroundWindow(IntPtr hWnd);
```
Brings a window to the foreground and activates it.

### Hotkey Management APIs

#### RegisterHotKey
```csharp
[DllImport("user32.dll")]
static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);
```
Defines a system-wide hot key.

**Modifier constants:**
- `MOD_ALT (0x1)`: Alt key
- `MOD_CONTROL (0x2)`: Ctrl key
- `MOD_SHIFT (0x4)`: Shift key
- `MOD_WIN (0x8)`: Windows key

#### UnregisterHotKey
```csharp
[DllImport("user32.dll")]
static extern bool UnregisterHotKey(IntPtr hWnd, int id);
```
Frees a hot key previously registered by the calling thread.

## 🔧 Extension Points

### IWindowOperation Interface

Interface for defining custom window operations.

```csharp
public interface IWindowOperation
{
    string Name { get; }
    string Description { get; }
    bool CanExecute(WindowInfo window);
    void Execute(WindowInfo window);
}
```

#### Example Implementation
```csharp
public class MinimizeWindowOperation : IWindowOperation
{
    public string Name => "Minimize";
    public string Description => "Minimize the selected window";
    
    public bool CanExecute(WindowInfo window)
    {
        return window.Handle != IntPtr.Zero && window.IsVisible && !window.IsMinimized;
    }
    
    public void Execute(WindowInfo window)
    {
        ShowWindow(window.Handle, SW_MINIMIZE);
    }
}
```

### ISearchProvider Interface

Interface for adding custom search providers.

```csharp
public interface ISearchProvider
{
    string Name { get; }
    Task<List<SearchResult>> SearchAsync(string query);
    bool CanHandle(string query);
}
```

### IHotkeyAction Interface

Interface for defining custom hotkey actions.

```csharp
public interface IHotkeyAction
{
    string Name { get; }
    string Description { get; }
    Keys Key { get; }
    uint Modifiers { get; }
    void Execute();
}
```

## 📝 Usage Examples

### Basic Window Management
```csharp
// Create main form and refresh window list
var mainForm = new MainForm();
mainForm.RefreshWindowList();

// Find a specific window
var notepadWindow = mainForm.CurrentWindowList
    .FirstOrDefault(w => w.Title.Contains("Notepad"));

if (notepadWindow != null)
{
    // Bring window to front
    SetForegroundWindow(notepadWindow.Handle);
    
    // Or minimize it
    ShowWindow(notepadWindow.Handle, SW_MINIMIZE);
}
```

### Search Operations
```csharp
// Create search form
var searchForm = new SearchItemsForm();

// Perform search
var results = searchForm.PerformSearch("calc");

// Process results
foreach (var result in results)
{
    Console.WriteLine($"{result.Title} (Score: {result.Score:F2})");
    
    if (result.Type == SearchResultType.Window && result.Data is WindowInfo window)
    {
        // Activate found window
        SetForegroundWindow(window.Handle);
        break;
    }
}
```

### File Indexing
```csharp
// Create file index
var fileIndex = new FileIndexes(@"C:\Users\Username\Documents");

// Build index asynchronously
await fileIndex.BuildIndex();

// Search files
var fileResults = fileIndex.SearchFiles("report");
foreach (var file in fileResults)
{
    Console.WriteLine($"Found: {file.FullPath}");
}
```

---

This API reference provides the foundation for integrating with and extending WController. For more detailed examples and advanced usage, refer to the source code and [ARCHITECTURE.md](ARCHITECTURE.md).