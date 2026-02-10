using llyrtkframework.Application;
using llyrtkframework.Caching;
using llyrtkframework.Configuration;
using llyrtkframework.DataAccess;
using llyrtkframework.Events;
using llyrtkframework.FileManagement;
using llyrtkframework.Localization;
using llyrtkframework.Logging;
using llyrtkframework.Mvvm;
using llyrtkframework.Notifications;
using llyrtkframework.Results;
using llyrtkframework.Security;
using llyrtkframework.StateManagement;
using llyrtkframework.Time;
using llyrtkframework.Validation;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Serilog;

namespace llyrtkframework.PrismIntegration;

/// <summary>
/// llyrtkframework の全モジュールを Prism DI コンテナに登録するヘルパー
/// </summary>
public static class ContainerRegistration
{
    /// <summary>
    /// すべてのフレームワークコンポーネントを登録
    /// </summary>
    public static void RegisterLlyrtkFramework(
        this IContainerRegistry containerRegistry,
        Action<FrameworkRegistrationOptions>? configure = null)
    {
        var options = new FrameworkRegistrationOptions();
        configure?.Invoke(options);

        // Logging
        RegisterLogging(containerRegistry, options);

        // Time
        RegisterTime(containerRegistry);

        // Events
        RegisterEvents(containerRegistry);

        // Caching
        RegisterCaching(containerRegistry);

        // Configuration
        RegisterConfiguration(containerRegistry, options);

        // StateManagement
        RegisterStateManagement(containerRegistry, options);

        // Localization
        RegisterLocalization(containerRegistry, options);

        // Notifications
        RegisterNotifications(containerRegistry);

        // Security
        RegisterSecurity(containerRegistry, options);

        // FileManagement
        RegisterFileManagement(containerRegistry, options);

        // DataAccess
        if (options.RegisterDataAccess)
        {
            RegisterDataAccess(containerRegistry);
        }

        // Validation
        RegisterValidation(containerRegistry);

        // Application
        RegisterApplication(containerRegistry, options);

        // MVVM Services
        RegisterMvvmServices(containerRegistry);
    }

    private static void RegisterLogging(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        if (options.ConfigureSerilog != null)
        {
            var loggerConfig = new LoggerConfiguration();
            options.ConfigureSerilog(loggerConfig);
            Log.Logger = loggerConfig.CreateLogger();
        }

        // ILoggerFactory
        containerRegistry.RegisterSingleton<ILoggerFactory>(provider =>
        {
            return new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger, dispose: false);
        });

        // ILogger<T>
        containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
    }

    private static void RegisterTime(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDateTimeProvider, SystemDateTimeProvider>();
    }

    private static void RegisterEvents(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<llyrtkframework.Events.IEventAggregator, llyrtkframework.Events.EventAggregator>();
    }

    private static void RegisterCaching(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ICache, InMemoryCache>();
    }

    private static void RegisterConfiguration(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        containerRegistry.RegisterSingleton<IConfigurationManager>(provider =>
        {
            var logger = provider.Resolve<ILogger<ConfigurationManager>>();
            return new ConfigurationManager(logger, options.ConfigurationBasePath);
        });
    }

    private static void RegisterStateManagement(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        containerRegistry.RegisterSingleton<IStateStore>(provider =>
        {
            var logger = provider.Resolve<ILogger<StateStore>>();
            return new StateStore(logger);
        });
    }

    private static void RegisterLocalization(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        containerRegistry.RegisterSingleton<ILocalizationService>(provider =>
        {
            var logger = provider.Resolve<ILogger<LocalizationService>>();
            var service = new LocalizationService(logger, options.DefaultCulture);

            // 初期化コールバック
            options.ConfigureLocalization?.Invoke(service);

            return service;
        });
    }

    private static void RegisterNotifications(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<INotificationService, NotificationService>();
    }

    private static void RegisterSecurity(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        containerRegistry.RegisterSingleton<IEncryptionService, EncryptionService>();
        containerRegistry.RegisterSingleton<IHashService, HashService>();

        containerRegistry.RegisterSingleton<ISecureStorage>(provider =>
        {
            var logger = provider.Resolve<ILogger<SecureStorage>>();
            var storagePath = Path.Combine(options.ApplicationDataPath, "secure.json");
            return new SecureStorage(logger, storagePath);
        });
    }

    private static void RegisterFileManagement(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        containerRegistry.RegisterSingleton<FileManagementCoordinator>(provider =>
        {
            var loggerFactory = provider.Resolve<ILoggerFactory>();
            var eventAggregator = provider.Resolve<llyrtkframework.Events.IEventAggregator>();
            return new FileManagementCoordinator(loggerFactory, eventAggregator);
        });
    }

    private static void RegisterDataAccess(IContainerRegistry containerRegistry)
    {
        // Repository は具体的なエンティティごとに登録
        containerRegistry.Register(typeof(IRepository<>), typeof(InMemoryRepository<>));
    }

    private static void RegisterValidation(IContainerRegistry containerRegistry)
    {
        // Validation は FluentValidation の AbstractValidator を各アプリで登録
    }

    private static void RegisterApplication(IContainerRegistry containerRegistry, FrameworkRegistrationOptions options)
    {
        // ApplicationInfo
        containerRegistry.RegisterSingleton<ApplicationInfo>(() => options.ApplicationInfo);

        // ApplicationInstanceManager
        containerRegistry.RegisterSingleton<ApplicationInstanceManager>();

        // CrashRecoveryManager
        containerRegistry.RegisterSingleton<CrashRecoveryManager>(provider =>
        {
            var logger = provider.Resolve<ILogger<CrashRecoveryManager>>();
            return new CrashRecoveryManager(logger, options.ApplicationDataPath);
        });

        // ApplicationVersionManager
        containerRegistry.RegisterSingleton<ApplicationVersionManager>(provider =>
        {
            var logger = provider.Resolve<ILogger<ApplicationVersionManager>>();
            return new ApplicationVersionManager(logger, options.ApplicationDataPath);
        });

        // ApplicationLifecycleManager
        containerRegistry.RegisterSingleton<ApplicationLifecycleManager>(provider =>
        {
            var logger = provider.Resolve<ILogger<ApplicationLifecycleManager>>();
            var recoveryManager = provider.Resolve<CrashRecoveryManager>();
            return new ApplicationLifecycleManager(logger, recoveryManager);
        });

        // ApplicationBootstrapper
        containerRegistry.Register<ApplicationBootstrapper>();
    }

    private static void RegisterMvvmServices(IContainerRegistry containerRegistry)
    {
        // IDialogService, INavigationService は各アプリで実装を登録
    }
}

/// <summary>
/// フレームワーク登録オプション
/// </summary>
public class FrameworkRegistrationOptions
{
    /// <summary>
    /// アプリケーション情報
    /// </summary>
    public ApplicationInfo ApplicationInfo { get; set; } = new ApplicationInfo();

    /// <summary>
    /// アプリケーションデータディレクトリ
    /// </summary>
    public string ApplicationDataPath => ApplicationInfo.ApplicationDataPath;

    /// <summary>
    /// 設定ファイルのベースパス
    /// </summary>
    public string ConfigurationBasePath { get; set; } = "Config";

    /// <summary>
    /// デフォルトカルチャ
    /// </summary>
    public System.Globalization.CultureInfo DefaultCulture { get; set; }
        = System.Globalization.CultureInfo.CurrentCulture;

    /// <summary>
    /// ローカライゼーション初期化コールバック
    /// </summary>
    public Action<LocalizationService>? ConfigureLocalization { get; set; }

    /// <summary>
    /// Serilog 設定コールバック
    /// </summary>
    public Action<LoggerConfiguration>? ConfigureSerilog { get; set; }

    /// <summary>
    /// ファイルバックアップを有効化
    /// </summary>
    public bool FileBackupEnabled { get; set; } = true;

    /// <summary>
    /// ファイルバックアップ間隔
    /// </summary>
    public TimeSpan FileBackupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// DataAccess (Repository) を登録
    /// </summary>
    public bool RegisterDataAccess { get; set; } = true;
}
