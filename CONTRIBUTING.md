# Contributing to WController

Thank you for your interest in contributing to WController! This document provides guidelines and information for contributors.

## 🤝 Code of Conduct

We are committed to providing a welcoming and inclusive environment for all contributors. Please be respectful and constructive in all interactions.

## 🚀 Getting Started

### Prerequisites for Contributors
1. **Development Environment**: See [BUILD.md](BUILD.md) for setup instructions
2. **Git Knowledge**: Basic understanding of Git workflow
3. **C# Experience**: Familiarity with C# and Windows Forms development
4. **Windows API Knowledge**: Understanding of Windows API for advanced features (helpful but not required)

### First-Time Contributors
1. **Fork the Repository**: Click "Fork" on the GitHub repository page
2. **Clone Your Fork**: 
   ```bash
   git clone https://github.com/YOUR_USERNAME/WindowsController.git
   cd WindowsController
   ```
3. **Set Up Upstream**: 
   ```bash
   git remote add upstream https://github.com/JuanFelix88/WindowsController.git
   ```
4. **Build and Test**: Follow the [BUILD.md](BUILD.md) instructions to ensure everything works

## 📋 Types of Contributions

### 🐛 Bug Reports
When reporting bugs, please include:
- **Clear Description**: What happened vs. what you expected
- **Steps to Reproduce**: Detailed steps to recreate the issue
- **Environment Details**: Windows version, .NET Framework version, etc.
- **Screenshots**: If applicable, include screenshots or recordings
- **Logs**: Any relevant error messages or log files

**Bug Report Template:**
```markdown
**Bug Description**
A clear description of the bug.

**To Reproduce**
1. Go to '...'
2. Click on '...'
3. Scroll down to '...'
4. See error

**Expected Behavior**
What you expected to happen.

**Environment**
- OS: [e.g., Windows 10 Pro 21H2]
- .NET Framework: [e.g., 4.8]
- WController Version: [e.g., 1.0.0]

**Additional Context**
Any other context about the problem.
```

### ✨ Feature Requests
For new features, please provide:
- **Use Case**: Why this feature would be valuable
- **Detailed Description**: How the feature should work
- **Alternative Solutions**: Other approaches you've considered
- **Implementation Ideas**: Technical suggestions (if you have any)

### 🔧 Code Contributions

#### Areas for Contribution
- **Window Management**: Enhance window detection and manipulation
- **Search Functionality**: Improve search algorithms and UI
- **Performance**: Optimize file indexing and window operations
- **UI/UX**: Improve user interface and experience
- **Documentation**: Code comments, user guides, technical docs
- **Testing**: Add unit tests, integration tests, or manual test procedures
- **Accessibility**: Improve accessibility features
- **Internationalization**: Add support for multiple languages

#### Pull Request Process
1. **Create Feature Branch**: 
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. **Make Changes**: Implement your feature or fix
3. **Test Thoroughly**: Ensure your changes work correctly
4. **Commit with Clear Messages**: Use descriptive commit messages
5. **Push to Your Fork**: 
   ```bash
   git push origin feature/your-feature-name
   ```
6. **Create Pull Request**: Submit PR with detailed description

## 📝 Development Guidelines

### Code Style and Standards

#### C# Coding Conventions
```csharp
// Use PascalCase for public members
public class WindowManager
{
    // Use camelCase for private fields with underscore prefix
    private readonly string _windowTitle;
    
    // Use PascalCase for properties
    public string WindowTitle { get; set; }
    
    // Use PascalCase for methods
    public void MinimizeWindow()
    {
        // Use camelCase for local variables
        var windowHandle = GetWindowHandle();
        // ...
    }
}
```

#### Naming Conventions
- **Classes**: PascalCase (`WindowManager`, `SearchForm`)
- **Methods**: PascalCase (`GetWindowList`, `UpdateUI`)
- **Properties**: PascalCase (`WindowTitle`, `IsVisible`)
- **Fields**: camelCase with underscore prefix (`_windowHandle`, `_isInitialized`)
- **Constants**: PascalCase (`MaxWindowCount`, `DefaultTimeout`)
- **Local Variables**: camelCase (`windowList`, `searchText`)

#### Code Organization
- **One class per file** (except for small helper classes)
- **Group related functionality** in the same namespace
- **Use regions sparingly** and only for large files
- **Order members**: Fields, Properties, Constructor, Methods
- **Keep methods focused** and single-purpose

### Documentation Standards

#### XML Documentation Comments
```csharp
/// <summary>
/// Retrieves a list of all visible windows on the desktop.
/// </summary>
/// <param name="includeMinimized">Whether to include minimized windows in the result.</param>
/// <returns>A list of window information objects.</returns>
/// <exception cref="UnauthorizedAccessException">Thrown when insufficient permissions.</exception>
public List<WindowInfo> GetVisibleWindows(bool includeMinimized = false)
{
    // Implementation...
}
```

#### Code Comments
- **Explain WHY, not WHAT**: Focus on reasoning rather than obvious operations
- **Document complex algorithms**: Especially Windows API interactions
- **Note important limitations**: Performance considerations, OS version requirements
- **Reference sources**: Link to documentation for complex Win32 API calls

### Testing Guidelines

#### Manual Testing
- **Test on Multiple Windows Versions**: Windows 10 and 11
- **Test Different DPI Settings**: 100%, 125%, 150%, 200%
- **Test Multiple Monitors**: Verify multi-monitor scenarios
- **Test Edge Cases**: Empty window lists, permission issues, etc.

#### Code Review Checklist
- [ ] Code follows established conventions
- [ ] No obvious performance issues
- [ ] Error handling is appropriate
- [ ] UI changes are responsive and accessible
- [ ] Windows API calls are used correctly
- [ ] Memory management is proper (dispose patterns)
- [ ] Thread safety is considered for UI operations

## 🔄 Git Workflow

### Branch Naming
- **Features**: `feature/description` (e.g., `feature/search-improvements`)
- **Bug Fixes**: `bugfix/description` (e.g., `bugfix/hotkey-registration`)
- **Documentation**: `docs/description` (e.g., `docs/api-documentation`)
- **Refactoring**: `refactor/description` (e.g., `refactor/window-manager`)

### Commit Messages
Follow the conventional commit format:
```
type(scope): description

[optional body]

[optional footer]
```

**Examples:**
```
feat(search): add fuzzy search algorithm
fix(hotkeys): resolve hotkey registration on Windows 11
docs(readme): update installation instructions
refactor(ui): extract window list component
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or modifying tests
- `chore`: Maintenance tasks

### Pull Request Guidelines

#### PR Title
Use the same format as commit messages:
```
feat(search): implement advanced window filtering
```

#### PR Description Template
```markdown
## Description
Brief description of changes made.

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Manual testing completed
- [ ] Edge cases considered
- [ ] Multiple Windows versions tested (if applicable)

## Screenshots (if applicable)
Include screenshots or recordings of UI changes.

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Code is commented where necessary
- [ ] Documentation updated (if needed)
```

## 🛡️ Security Considerations

### Sensitive Operations
WController performs several operations that require careful consideration:
- **Window manipulation**: Affecting other applications
- **Hotkey registration**: Global system hooks
- **Process enumeration**: Accessing information about running processes
- **File system access**: Indexing files and directories

### Security Guidelines
- **Validate all inputs**: Especially file paths and window handles
- **Handle permissions gracefully**: Provide clear error messages for access issues
- **Minimize privileges**: Request only necessary permissions
- **Sanitize data**: Prevent injection attacks in file operations

## 📞 Getting Help

### Communication Channels
- **GitHub Issues**: For bug reports and feature requests
- **GitHub Discussions**: For questions and general discussion
- **Pull Request Comments**: For code-specific discussions

### Questions and Support
Before asking questions:
1. **Check existing issues**: Your question might already be answered
2. **Read documentation**: README, BUILD, and this CONTRIBUTING guide
3. **Review code**: Understanding the existing codebase helps with questions

When asking questions:
- **Be specific**: Include relevant details and context
- **Provide examples**: Code snippets, error messages, or screenshots
- **Show what you've tried**: Demonstrate your effort to solve the problem

## 🎉 Recognition

Contributors will be recognized in:
- **README acknowledgments**: Major contributors listed in README
- **Release notes**: Contributors mentioned in release changelogs
- **GitHub insights**: Automatic recognition through GitHub's contributor features

## 📜 License

By contributing to WController, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to WController! Your efforts help make this tool better for everyone.