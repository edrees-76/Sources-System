using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Services;
using Sources.Helpers;
using System.Windows;

namespace Sources.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IAuditService _auditService;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _rememberMe;
    [ObservableProperty] private bool _isPasswordVisible;

    private readonly string _configPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sources", "login_config.dat");

    public event Action? LoginSuccess;

    public LoginViewModel(IUserService userService, IAuditService auditService)
    {
        _userService = userService;
        _auditService = auditService;
        LoadCredentials();
    }

    private void LoadCredentials()
    {
        if (System.IO.File.Exists(_configPath))
        {
            try
            {
                var lines = System.IO.File.ReadAllLines(_configPath);
                if (lines.Length >= 2)
                {
                    Username = lines[0];
                    RememberMe = bool.TryParse(lines[1], out var r) && r;
                }
            }
            catch { /* Ignore errors */ }
        }
    }

    private void SaveCredentials()
    {
        try
        {
            if (RememberMe)
            {
                System.IO.File.WriteAllLines(_configPath, new[] { Username, RememberMe.ToString() });
            }
            else if (System.IO.File.Exists(_configPath))
            {
                System.IO.File.Delete(_configPath);
            }
        }
        catch { /* Ignore errors */ }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private void Login()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
        {
            SetError(TranslationHelper.GetString("MsgErrUsernameReq"));
            return;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            SetError(TranslationHelper.GetString("MsgErrPasswordReq"));
            return;
        }

        IsLoading = true;
        var (success, message) = _userService.Login(Username, Password);
        IsLoading = false;

        if (success)
        {
            SaveCredentials();
            _auditService.Log("Login", "Users", _userService.CurrentUser?.Id, TranslationHelper.GetString("LogActionLogin"));
            LoginSuccess?.Invoke();
        }
        else
        {
            SetError(message);
        }
    }

    private void SetError(string msg)
    {
        ErrorMessage = msg;
        HasError = true;
    }
}
