// This file is disabled because Prism.Avalonia does not support Prism.Regions namespace
// If you need navigation support, please implement it using Avalonia-specific navigation patterns

#if PRISM_REGIONS_SUPPORT
using llyrtkframework.Mvvm;
using llyrtkframework.Results;
using Microsoft.Extensions.Logging;
using Prism.Regions;

namespace llyrtkframework.PrismIntegration;

/// <summary>
/// Prism の IRegionManager を使用した NavigationService 実装
/// </summary>
public class PrismNavigationService : INavigationService
{
    private readonly IRegionManager _regionManager;
    private readonly ILogger<PrismNavigationService> _logger;
    private readonly string _defaultRegionName;

    public PrismNavigationService(
        IRegionManager regionManager,
        ILogger<PrismNavigationService> logger,
        string defaultRegionName = "ContentRegion")
    {
        _regionManager = regionManager;
        _logger = logger;
        _defaultRegionName = defaultRegionName;
    }

    public bool CanGoBack
    {
        get
        {
            try
            {
                var region = _regionManager.Regions[_defaultRegionName];
                return region?.NavigationService.Journal.CanGoBack ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    public ViewModelBase? CurrentViewModel
    {
        get
        {
            try
            {
                var region = _regionManager.Regions[_defaultRegionName];
                return region?.ActiveViews?.FirstOrDefault() as ViewModelBase;
            }
            catch
            {
                return null;
            }
        }
    }

    public Task<Result> NavigateAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
    {
        var viewModelName = typeof(TViewModel).Name;
        // ViewModelの名前から"ViewModel"を削除してView名を取得
        var viewName = viewModelName.EndsWith("ViewModel")
            ? viewModelName.Substring(0, viewModelName.Length - "ViewModel".Length)
            : viewModelName;

        return NavigateAsync(viewName, parameter);
    }

    public Task<Result> NavigateAsync(string viewName, object? parameter = null)
    {
        return NavigateAsync(_defaultRegionName, viewName, parameter);
    }

    public Task<Result> NavigateAsync(string regionName, string viewName, object? parameter = null)
    {
        var tcs = new TaskCompletionSource<Result>();

        try
        {
            _logger.LogInformation("Navigating to {ViewName} in region {RegionName}", viewName, regionName);

            var navigationParameters = parameter != null
                ? new NavigationParameters { { "parameter", parameter } }
                : null;

            _regionManager.RequestNavigate(
                regionName,
                viewName,
                result =>
                {
                    if (result.Result == true)
                    {
                        _logger.LogInformation("Navigation successful");
                        tcs.SetResult(Result.Success());
                    }
                    else
                    {
                        var error = result.Error?.Message ?? "Unknown navigation error";
                        _logger.LogError("Navigation failed: {Error}", error);
                        tcs.SetResult(Result.Failure(error));
                    }
                },
                navigationParameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {ViewName}", viewName);
            tcs.SetResult(Result.FromException(ex, "Navigation failed"));
        }

        return tcs.Task;
    }

    public Task<Result> GoBackAsync()
    {
        try
        {
            var region = _regionManager.Regions[_defaultRegionName];
            if (region == null)
            {
                _logger.LogWarning("Region not found: {RegionName}", _defaultRegionName);
                return Task.FromResult(Result.Failure("Region not found"));
            }

            var journal = region.NavigationService.Journal;
            if (journal.CanGoBack)
            {
                journal.GoBack();
                _logger.LogInformation("Navigated back in region {RegionName}", _defaultRegionName);
                return Task.FromResult(Result.Success());
            }
            else
            {
                _logger.LogWarning("Cannot go back - no previous page in history");
                return Task.FromResult(Result.Failure("Cannot go back - no previous page"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go back");
            return Task.FromResult(Result.FromException(ex, "Failed to go back"));
        }
    }

    public void ClearHistory()
    {
        try
        {
            var region = _regionManager.Regions[_defaultRegionName];
            if (region == null)
            {
                _logger.LogWarning("Region not found: {RegionName}", _defaultRegionName);
                return;
            }

            var journal = region.NavigationService.Journal;
            journal.Clear();
            _logger.LogInformation("Cleared navigation history in region {RegionName}", _defaultRegionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history");
        }
    }

    public Task<Result> GoForwardAsync(string? regionName = null)
    {
        try
        {
            var region = _regionManager.Regions[regionName ?? _defaultRegionName];
            if (region == null)
            {
                _logger.LogWarning("Region not found: {RegionName}", regionName ?? _defaultRegionName);
                return Task.FromResult(Result.Failure("Region not found"));
            }

            var journal = region.NavigationService.Journal;
            if (journal.CanGoForward)
            {
                journal.GoForward();
                _logger.LogInformation("Navigated forward in region {RegionName}", regionName ?? _defaultRegionName);
                return Task.FromResult(Result.Success());
            }
            else
            {
                _logger.LogWarning("Cannot go forward - no next page in history");
                return Task.FromResult(Result.Failure("Cannot go forward - no next page"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go forward");
            return Task.FromResult(Result.FromException(ex, "Failed to go forward"));
        }
    }

    public Task<Result<bool>> CanGoBackAsync(string? regionName = null)
    {
        try
        {
            var region = _regionManager.Regions[regionName ?? _defaultRegionName];
            if (region == null)
            {
                return Task.FromResult(Result<bool>.Success(false));
            }

            var canGoBack = region.NavigationService.Journal.CanGoBack;
            return Task.FromResult(Result<bool>.Success(canGoBack));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if can go back");
            return Task.FromResult(Result<bool>.FromException(ex, "Failed to check navigation state"));
        }
    }

    public Task<Result<bool>> CanGoForwardAsync(string? regionName = null)
    {
        try
        {
            var region = _regionManager.Regions[regionName ?? _defaultRegionName];
            if (region == null)
            {
                return Task.FromResult(Result<bool>.Success(false));
            }

            var canGoForward = region.NavigationService.Journal.CanGoForward;
            return Task.FromResult(Result<bool>.Success(canGoForward));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if can go forward");
            return Task.FromResult(Result<bool>.FromException(ex, "Failed to check navigation state"));
        }
    }
}
#endif
