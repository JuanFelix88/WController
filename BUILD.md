# Building WController

This guide covers building WController from source code and setting up a development environment.

## 🛠️ Development Environment Setup

### Prerequisites

#### Required Software
- **Visual Studio 2019/2022** (Community, Professional, or Enterprise)
  - Or **Visual Studio Build Tools 2019/2022**
  - Or **JetBrains Rider** (alternative IDE)
- **.NET Framework 4.8 Developer Pack**
- **Git** for version control

#### Optional Tools
- **Visual Studio Code** (for lightweight editing)
- **ReSharper** (for enhanced development experience in Visual Studio)
- **GitHub Desktop** (for easier Git management)

### Getting the Source Code

```bash
# Clone the repository
git clone https://github.com/JuanFelix88/WindowsController.git
cd WindowsController
```

## 🔨 Building the Project

### Using Visual Studio

1. **Open the Solution**
   ```
   Open WController.sln in Visual Studio
   ```

2. **Restore NuGet Packages** (if any)
   ```
   Right-click Solution → Restore NuGet Packages
   ```

3. **Build the Solution**
   ```
   Build → Build Solution (Ctrl+Shift+B)
   ```

4. **Run the Application**
   ```
   Debug → Start Debugging (F5)
   or
   Debug → Start Without Debugging (Ctrl+F5)
   ```

### Using MSBuild (Command Line)

```bash
# Navigate to project directory
cd WindowsController

# Build Debug version
msbuild WController.sln /p:Configuration=Debug

# Build Release version
msbuild WController.sln /p:Configuration=Release

# Build with specific platform
msbuild WController.sln /p:Configuration=Release /p:Platform="Any CPU"
```

### Using .NET CLI (if applicable)

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Build for release
dotnet build --configuration Release
```

## 📁 Project Structure

```
WController/
├── WController.sln
├── WController/                         # Main application
│   ├── WController.csproj
│   ├── MainForm.cs
│   ├── Forms/
│   ├── Properties/
│   ├── Resources/
│   └── Util/
├── WController.Plugin.GoogleSearch/
├── WController.Plugin.ProjectsSearch/
└── WController.Plugin.Prompts/
```

The root solution contains the main application and all three plugins.

## 🔧 Build Configurations

### Debug Configuration
- **Purpose**: Development and debugging
- **Optimization**: Disabled
- **Debug Symbols**: Full
- **Output**: each project's `bin/Debug/`
- **Defines**: `DEBUG;TRACE`

### Release Configuration
- **Purpose**: Production deployment
- **Optimization**: Enabled
- **Debug Symbols**: PDB-only
- **Output**: each project's `bin/Release/`
- **Defines**: `TRACE`
- **Platform Target**: Any CPU (with 32-bit preference disabled)

### Plugin Deployment Configurations
- **ToPlugin**: builds the application in Debug and copies plugin outputs to `WController/bin/Debug/`
- **ToPluginRel**: builds the application in Release and copies plugin outputs to `WController/bin/Release/`

## 🐛 Debugging

### Visual Studio Debugging
1. Set breakpoints in the code
2. Press F5 to start debugging
3. Use Debug → Windows to access debugging tools:
   - **Locals**: View local variables
   - **Watch**: Monitor specific expressions
   - **Call Stack**: View execution path
   - **Output**: See debug output

### Common Debug Scenarios
- **Window Management**: Debug window enumeration and manipulation
- **Hotkey Registration**: Test global hotkey functionality
- **UI Threading**: Monitor UI thread operations
- **File Operations**: Debug file indexing and search operations

## ⚠️ Common Build Issues

### Issue: Missing .NET Framework 4.8
**Solution**: Install .NET Framework 4.8 Developer Pack from Microsoft

### Issue: MSBuild not found
**Solutions**:
- Install Visual Studio Build Tools
- Add MSBuild to PATH environment variable
- Use Developer Command Prompt for Visual Studio

### Issue: Platform Target Conflicts
**Solution**: Ensure consistent platform targeting across all projects

### Issue: Resource Compilation Errors
**Solutions**:
- Verify resource files (`.resx`) are properly formatted
- Check that embedded resources are correctly referenced
- Ensure icon files are accessible

## 🧪 Testing

### Manual Testing Checklist
- [ ] Application launches without errors
- [ ] Window enumeration works correctly
- [ ] Search functionality responds properly
- [ ] Hotkeys register and function correctly
- [ ] Window renaming works as expected
- [ ] UI elements render correctly
- [ ] File indexing completes successfully

### Testing Different Windows Versions
- Test on Windows 10 (various builds)
- Test on Windows 11
- Verify compatibility with different DPI settings
- Test with multiple monitors

## 📦 Creating Releases

### Release Build Process
1. **Update Version Numbers**
   ```csharp
   // Update in WController/Properties/AssemblyInfo.cs
   [assembly: AssemblyVersion("1.0.0.0")]
   [assembly: AssemblyFileVersion("1.0.0.0")]
   ```

2. **Build Release Configuration**
   ```bash
   msbuild WController.sln /p:Configuration=Release
   ```

3. **Test Release Build**
   - Verify all functionality works
   - Test on clean Windows installation
   - Validate file dependencies

4. **Package for Distribution**
   - Create ZIP archive with executable and dependencies
   - Include documentation files
   - Test installation on target systems

### Deployment Considerations
- **Dependencies**: Ensure .NET Framework 4.8 is available on target systems
- **Permissions**: Application may require administrative privileges for some features
- **Antivirus**: Some antivirus software may flag the application due to window manipulation capabilities

## 🔍 Code Analysis

### Static Analysis Tools
- **Visual Studio Code Analysis**: Built-in code analysis in Visual Studio
- **SonarLint**: Real-time code quality analysis
- **ReSharper**: Advanced code analysis and refactoring

### Code Quality Guidelines
- Follow C# coding conventions
- Use meaningful variable and method names
- Document complex algorithms
- Handle exceptions appropriately
- Validate user inputs

---

For more information about contributing to the codebase, see [CONTRIBUTING.md](CONTRIBUTING.md).