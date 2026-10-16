using CyberGame.Application.DTOs;

namespace CyberGame.Client.Services;

public interface IStateService
{
    UserProfileDto? CurrentUser { get; set; }
    ShiftDto? CurrentShift { get; set; }
    string? BranchName { get; set; }

    bool IsAuthenticated => CurrentUser != null;
    bool HasActiveShift => CurrentShift != null;

    event Action? StateChanged;
    void NotifyStateChanged();
    void Clear();
}

public class StateService : IStateService
{
    private UserProfileDto? _currentUser;
    private ShiftDto? _currentShift;
    private string? _branchName = "CyberGame Chi Nhánh Trung Tâm";

    public UserProfileDto? CurrentUser
    {
        get => _currentUser;
        set
        {
            _currentUser = value;
            NotifyStateChanged();
        }
    }

    public ShiftDto? CurrentShift
    {
        get => _currentShift;
        set
        {
            _currentShift = value;
            NotifyStateChanged();
        }
    }

    public string? BranchName
    {
        get => _branchName;
        set
        {
            _branchName = value;
            NotifyStateChanged();
        }
    }

    public event Action? StateChanged;

    public void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    public void Clear()
    {
        _currentUser = null;
        _currentShift = null;
        NotifyStateChanged();
    }
}
