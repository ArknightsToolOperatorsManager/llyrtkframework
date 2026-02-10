# FileManagement モジュール

INPUT/OUTPUTファイルの管理、自動バックアップ、トランザクション保存を提供するモジュールです。

## 概要

FileManagementモジュールは、アプリケーションのデータファイル（JSON、CSVなど）を統一的に管理し、以下の機能を提供します：

- **ファイル単位の管理**: 1ファイル1クラスでSingleton管理
- **自動バックアップ**: 柔軟なトリガーシステム（時間、イベント、起動時など）
- **自動ロールバック**: バックアップからの復元失敗時、古いバックアップを自動試行
- **GitHub連携**: ポーリング方式でリモートリポジトリから自動更新
- **JSON差分検出**: 更新内容の差分を自動検出し、イベント経由で通知
- **トランザクション保存**: 一時ファイル経由で原子性を保証
- **スレッドセーフ**: ReaderWriterLockSlimによる並行アクセス制御
- **統合管理**: FileManagerRegistryによる一元管理

## 新機能（v2.0）

### 自動ロールバック機能
最新のバックアップからの復元に失敗した場合、自動的に古いバックアップを順次試行します。復元成功時にはイベント経由でユーザーに警告通知を行います。

**主な特徴**:
- 最大リトライ回数の設定可能（デフォルト: 3回、-1で全バックアップ試行）
- リトライ間の待機時間設定
- 全失敗時の動作制御（Result.Failureまたは例外スロー）
- BackupRollbackEventによる通知

### GitHub連携機能
ポーリング方式でGitHubリポジトリから自動的にファイルを更新します。リモート優先の衝突解決により、常にリモートの最新状態に同期します。

**主な特徴**:
- `pushed_at`タイムスタンプによる効率的な変更検出
- SHA256ハッシュによるファイル整合性検証
- ローカルバックアップ後、リモートで上書き
- Personal Access Token対応（レート制限緩和）
- キャッシュ機能（デフォルト: 1日）
- GitHubSyncManagerによる複数ファイルの一括管理

**同期フロー**:
1. CacheDuration経過チェック
2. リポジトリの`pushed_at`取得
3. 前回のタイムスタンプと比較（変更なしなら終了）
4. リモートファイルのSHA256取得
5. ローカルファイルのSHA256計算
6. ハッシュ比較（一致なら終了）
7. ローカルバックアップ作成
8. リモート内容ダウンロード
9. JSON差分検出（.jsonファイルの場合）
10. ローカルファイル上書き
11. GitHubFileUpdatedEvent発行

### JSON差分検出
JSON形式のファイル更新時、プロパティの追加・削除・変更を自動検出し、詳細な差分レポートを生成します。

**主な特徴**:
- System.Text.Json使用の高速なJSON解析
- 再帰的なオブジェクト・配列比較
- PropertyPath形式（例: `users[0].name`）での変更箇所特定
- Added/Removed/Modified別の分類
- 人間可読なサマリー生成

## 主要コンポーネント

### Core

#### IFileManager&lt;T&gt;
ファイル管理の基本インターフェース。

```csharp
public interface IFileManager<T> where T : class
{
    Task<Result<T>> LoadAsync(CancellationToken cancellationToken = default);
    Task<Result> SaveAsync(T data, CancellationToken cancellationToken = default);
    Task<Result> CreateBackupAsync(CancellationToken cancellationToken = default);
    Task<Result<T>> RestoreFromLatestBackupAsync();
    Task<Result<T>> RestoreWithRollbackAsync(
        RollbackOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

#### FileManagerBase&lt;T&gt;
ファイル管理の基底クラス。派生クラスを作成して使用します。

```csharp
public class UserSettingsManager : FileManagerBase<UserSettings>
{
    public UserSettingsManager(
        string filePath,
        ILogger logger,
        IEventAggregator eventAggregator)
        : base(
            new FileOptions
            {
                FilePath = filePath,
                BackupTriggers = new List<BackupTrigger>
                {
                    new IntervalBackupTrigger(TimeSpan.FromMinutes(30)),
                    new OnSaveBackupTrigger(eventAggregator, filePath)
                },
                Backup = new BackupOptions
                {
                    MaxBackupCount = 10,
                    RetentionPeriod = TimeSpan.FromDays(7)
                }
            },
            new JsonFileSerializer<UserSettings>(),
            logger,
            eventAggregator)
    {
    }
}

// 使用例
var manager = new UserSettingsManager("settings.json", logger, eventAggregator);
var result = await manager.LoadAsync();
if (result.IsSuccess)
{
    var settings = result.Value;
    settings.Theme = "Dark";
    await manager.SaveAsync(settings);
}
```

#### FileManagerRegistry
すべてのファイルマネージャーを一元管理するSingletonクラス。

```csharp
// 自動登録（FileManagerBaseコンストラクタで実行）
var manager = new UserSettingsManager("settings.json", logger, eventAggregator);

// 手動操作
var registry = FileManagerRegistry.Instance;

// すべてのファイルをバックアップ
await registry.ExecuteBackupAllAsync();

// 変更されたファイルのみバックアップ
await registry.ExecuteIncrementalBackupAsync();

// 特定のファイルをバックアップ
await registry.ExecuteBackupAsync("settings.json");

// 登録ファイル一覧を取得
var files = registry.GetRegisteredFiles();
```

### Backup

#### BackupOptions
バックアップの設定。

```csharp
public class BackupOptions
{
    public int MaxBackupCount { get; set; } = 10;
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    public string? BackupDirectory { get; set; }
    public string BackupFilePattern { get; set; } = "{filename}_{timestamp}.bak";
}
```

#### BackupManager
バックアップファイルの作成と管理。

```csharp
var backupManager = new BackupManager("data.json", options, logger);

// バックアップ作成
await backupManager.CreateBackupAsync();

// 最新のバックアップパスを取得
var latestResult = backupManager.GetLatestBackupPath();

// すべてのバックアップパスを取得
var allResult = backupManager.GetAllBackupPaths();
```

### Triggers

バックアップトリガーシステム。ファイルごとに複数のトリガーを設定可能。

#### IntervalBackupTrigger
一定間隔でバックアップ。

```csharp
// 30分ごとにバックアップ
var trigger = new IntervalBackupTrigger(TimeSpan.FromMinutes(30));
```

#### ScheduledTimeBackupTrigger
特定時刻にバックアップ。

```csharp
// 毎日2:00にバックアップ
var trigger = new ScheduledTimeBackupTrigger(
    dateTimeProvider,
    new TimeSpan(2, 0, 0),  // 2:00 AM
    TimeSpan.FromDays(1)    // 24時間ごと
);
```

#### OnSaveBackupTrigger
ファイル保存時にバックアップ。

```csharp
var trigger = new OnSaveBackupTrigger(eventAggregator, "data.json");
```

#### OnModifiedBackupTrigger
ファイル変更後、一定時間経過してバックアップ（デバウンス）。

```csharp
// 変更後30秒待ってバックアップ
var trigger = new OnModifiedBackupTrigger(
    eventAggregator,
    "data.json",
    TimeSpan.FromSeconds(30)
);
```

#### OnStartupBackupTrigger
アプリ起動時にバックアップ。

```csharp
var trigger = new OnStartupBackupTrigger(TimeSpan.FromSeconds(5));
```

#### CombinedBackupTrigger
複数のトリガーを組み合わせ。

```csharp
var trigger = new CombinedBackupTrigger(
    new OnSaveBackupTrigger(eventAggregator, "data.json"),
    new IntervalBackupTrigger(TimeSpan.FromHours(1))
);
```

#### GitHubBackupTrigger
GitHubリポジトリのポーリング方式同期トリガー。

```csharp
// GitHub同期設定
var githubOptions = new GitHubFileOptions
{
    Owner = "myorg",
    Repository = "config-repo",
    Branch = "main",
    FilePath = "configs/app.json",
    Token = Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
    PollingInterval = TimeSpan.FromMinutes(15),
    CacheDuration = TimeSpan.FromDays(1)
};

var trigger = new GitHubBackupTrigger(
    githubOptions,
    logger,
    eventAggregator
);
```

### Serializers

#### JsonFileSerializer&lt;T&gt;
JSON形式のシリアライザー。

```csharp
var serializer = new JsonFileSerializer<MyData>();

// カスタムオプション
var customSerializer = new JsonFileSerializer<MyData>(
    new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    }
);
```

### Events

#### FileSavedEvent
ファイル保存時に発行。

```csharp
eventAggregator.GetEvent<FileSavedEvent>()
    .Subscribe(e => Console.WriteLine($"Saved: {e.FilePath}"));
```

#### FileLoadedEvent
ファイル読み込み時に発行。

```csharp
eventAggregator.GetEvent<FileLoadedEvent>()
    .Subscribe(e => Console.WriteLine($"Loaded: {e.FilePath}"));
```

#### BackupCreatedEvent
バックアップ作成時に発行。

```csharp
eventAggregator.GetEvent<BackupCreatedEvent>()
    .Subscribe(e => Console.WriteLine($"Backup: {e.BackupFilePath}"));
```

#### GlobalBackupCompletedEvent
全ファイルバックアップ完了時に発行。

```csharp
eventAggregator.GetEvent<GlobalBackupCompletedEvent>()
    .Subscribe(e => Console.WriteLine($"Completed: {e.FileCount} files"));
```

#### BackupRollbackEvent
ロールバック実行時に発行（最新以外のバックアップから復元した場合）。

```csharp
eventAggregator.GetEvent<BackupRollbackEvent>()
    .Subscribe(e =>
    {
        if (e.IsFullFailure)
        {
            Console.WriteLine($"All {e.TriedBackupPaths.Count} backups failed");
        }
        else
        {
            Console.WriteLine($"Restored from: {e.SuccessfulBackupPath}");
        }
    });
```

#### GitHubFileCheckedEvent
GitHub確認完了時に発行（変更なし時も発行）。

```csharp
eventAggregator.GetEvent<GitHubFileCheckedEvent>()
    .Subscribe(e =>
    {
        Console.WriteLine($"Checked: {e.LocalFilePath}");
        Console.WriteLine($"Has changes: {e.HasChanges}");
    });
```

#### GitHubFileUpdatedEvent
GitHub同期完了時に発行（実際に更新された場合のみ）。

```csharp
eventAggregator.GetEvent<GitHubFileUpdatedEvent>()
    .Subscribe(e =>
    {
        Console.WriteLine($"Updated from: {e.RemoteUrl}");

        if (e.DiffReport?.HasChanges == true)
        {
            Console.WriteLine($"Added: {e.DiffReport.AddedProperties.Count}");
            Console.WriteLine($"Removed: {e.DiffReport.RemovedProperties.Count}");
            Console.WriteLine($"Modified: {e.DiffReport.ModifiedProperties.Count}");

            foreach (var change in e.DiffReport.ModifiedProperties)
            {
                Console.WriteLine($"  {change.PropertyPath}: {change.OldValue} → {change.NewValue}");
            }
        }
    });
```

### Rollback

#### RollbackOptions
ロールバック時のリトライ動作を設定。

```csharp
public class RollbackOptions
{
    // リトライ回数（-1で全バックアップ試行、デフォルト: 3）
    public int MaxRetries { get; set; } = 3;

    // リトライ間の待機時間（デフォルト: 100ms）
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    // 全失敗時に例外をスローするか（デフォルト: false）
    public bool ThrowOnAllFailed { get; set; } = false;
}
```

### GitHub

#### GitHubFileOptions
GitHub連携の設定。

```csharp
public class GitHubFileOptions
{
    public string Owner { get; set; }           // リポジトリオーナー
    public string Repository { get; set; }      // リポジトリ名
    public string Branch { get; set; } = "main"; // ブランチ
    public string FilePath { get; set; }        // リポジトリ内のファイルパス
    public string? Token { get; set; }          // Personal Access Token（任意）
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}
```

#### GitHubSyncManager
複数ファイルの同期を一括管理するSingletonクラス。

```csharp
var manager = GitHubSyncManager.Instance;

// 初期化（任意）
manager.Initialize(logger);

// すべてのファイルを同期
await manager.SyncAllAsync();

// 特定のファイルを同期
await manager.SyncAsync("path/to/file.json");

// 登録ファイル一覧を取得
var files = manager.GetRegisteredFiles();
```

### Diff

#### JsonDiffDetector
JSON差分を自動検出する汎用ユーティリティ。

```csharp
// 2つのJSON文字列を比較
var diffReport = JsonDiffDetector.DetectDifferences(oldJson, newJson);

if (diffReport.HasChanges)
{
    Console.WriteLine($"Summary: {diffReport.GetSummary()}");

    // 追加されたプロパティ
    foreach (var added in diffReport.AddedProperties)
    {
        Console.WriteLine($"Added: {added.PropertyPath} = {added.NewValue}");
    }

    // 削除されたプロパティ
    foreach (var removed in diffReport.RemovedProperties)
    {
        Console.WriteLine($"Removed: {removed.PropertyPath} = {removed.OldValue}");
    }

    // 変更されたプロパティ
    foreach (var modified in diffReport.ModifiedProperties)
    {
        Console.WriteLine($"Modified: {modified.PropertyPath}");
        Console.WriteLine($"  Old: {modified.OldValue}");
        Console.WriteLine($"  New: {modified.NewValue}");
    }
}
```

### Utilities

#### HashUtility
SHA256ハッシュ計算ユーティリティ。

```csharp
// ファイルから計算（非同期）
var hash = await HashUtility.CalculateSha256FromFileAsync(filePath);

// バイト配列から計算
var hash = HashUtility.CalculateSha256FromBytes(bytes);

// 文字列から計算
var hash = HashUtility.CalculateSha256FromString(content);
```

## 使用例

### 基本的な使用方法

```csharp
// 1. データモデル定義
public class AppSettings
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "ja-JP";
    public int FontSize { get; set; } = 14;
}

// 2. ファイルマネージャー作成
public class AppSettingsManager : FileManagerBase<AppSettings>
{
    public AppSettingsManager(
        ILogger<AppSettingsManager> logger,
        IEventAggregator eventAggregator)
        : base(
            new FileOptions
            {
                FilePath = "appsettings.json",
                BackupTriggers = new List<BackupTrigger>
                {
                    new OnSaveBackupTrigger(eventAggregator, "appsettings.json"),
                    new IntervalBackupTrigger(TimeSpan.FromHours(1))
                },
                Backup = new BackupOptions
                {
                    MaxBackupCount = 5,
                    RetentionPeriod = TimeSpan.FromDays(7)
                }
            },
            new JsonFileSerializer<AppSettings>(),
            logger,
            eventAggregator)
    {
    }
}

// 3. 使用
var manager = new AppSettingsManager(logger, eventAggregator);

// 読み込み
var loadResult = await manager.LoadAsync();
if (loadResult.IsSuccess)
{
    var settings = loadResult.Value;
    Console.WriteLine($"Theme: {settings.Theme}");

    // 変更
    settings.Theme = "Dark";

    // 保存（自動的にバックアップも実行される）
    await manager.SaveAsync(settings);
}

// バックアップから復元
var restoreResult = await manager.RestoreFromLatestBackupAsync();
```

### 複数ファイルの管理

```csharp
// 各ファイルのマネージャーを作成
var settingsManager = new AppSettingsManager(logger, eventAggregator);
var userDataManager = new UserDataManager(logger, eventAggregator);
var cacheManager = new CacheDataManager(logger, eventAggregator);

// すべて自動的にFileManagerRegistryに登録される

// 定期的にすべてのファイルをバックアップ
var timer = new System.Threading.Timer(async _ =>
{
    await FileManagerRegistry.Instance.ExecuteBackupAllAsync();
}, null, TimeSpan.Zero, TimeSpan.FromHours(24));

// 変更されたファイルのみバックアップ
await FileManagerRegistry.Instance.ExecuteIncrementalBackupAsync();
```

### ロールバック機能の使用

```csharp
// 最新のバックアップから復元（失敗時は自動的に古いバックアップを試行）
var result = await manager.RestoreWithRollbackAsync(new RollbackOptions
{
    MaxRetries = 5,  // 最大5個のバックアップを試行
    RetryDelay = TimeSpan.FromMilliseconds(100),
    ThrowOnAllFailed = false
});

if (result.IsSuccess)
{
    Console.WriteLine($"Restored: {result.Value}");
}
else
{
    Console.WriteLine($"All backups failed: {result.ErrorMessage}");
}

// ロールバックイベントの購読
eventAggregator.GetEvent<BackupRollbackEvent>()
    .Subscribe(e =>
    {
        if (!e.IsFullFailure)
        {
            // 最新以外のバックアップから復元された場合、ユーザーに警告
            MessageBox.Show(
                $"最新のバックアップが破損していたため、{Path.GetFileName(e.SuccessfulBackupPath)}から復元しました。",
                "バックアップ警告",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        else
        {
            // すべてのバックアップが失敗
            MessageBox.Show(
                $"すべてのバックアップファイルが破損しています。\n試行したバックアップ: {e.TriedBackupPaths.Count}個",
                "バックアップエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    });
```

### GitHub連携の使用

```csharp
// 1. GitHub同期設定
var githubOptions = new GitHubFileOptions
{
    Owner = "myorg",
    Repository = "config-repo",
    Branch = "main",
    FilePath = "configs/app.json",
    Token = Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
    PollingInterval = TimeSpan.FromMinutes(15),  // 15分ごとにチェック
    CacheDuration = TimeSpan.FromDays(1)  // pushed_atキャッシュ期間
};

// 2. トリガー作成
var githubTrigger = new GitHubBackupTrigger(
    githubOptions,
    logger,
    eventAggregator
);

// 3. ファイルマネージャー作成（GitHub同期トリガー付き）
var fileOptions = new FileOptions
{
    FilePath = @"C:\AppData\app.json",
    BackupTriggers = new List<BackupTrigger> { githubTrigger },
    Backup = new BackupOptions { MaxBackupCount = 20 }
};

var manager = new JsonFileManager<AppConfig>(
    fileOptions,
    new JsonFileSerializer<AppConfig>(),
    logger,
    eventAggregator
);

// 4. イベント購読
eventAggregator.GetEvent<GitHubFileUpdatedEvent>()
    .Subscribe(evt =>
    {
        if (evt.DiffReport?.HasChanges == true)
        {
            // 変更内容をユーザーに通知
            var message = $"GitHubからファイルが更新されました:\n\n";

            foreach (var added in evt.DiffReport.AddedProperties)
            {
                message += $"追加: {added.PropertyPath} = {added.NewValue}\n";
            }

            foreach (var removed in evt.DiffReport.RemovedProperties)
            {
                message += $"削除: {removed.PropertyPath}\n";
            }

            foreach (var modified in evt.DiffReport.ModifiedProperties)
            {
                message += $"変更: {modified.PropertyPath}\n";
                message += $"  旧: {modified.OldValue}\n";
                message += $"  新: {modified.NewValue}\n";
            }

            MessageBox.Show(message, "設定更新通知", MessageBoxButton.OK, MessageBoxImage.Information);

            // ファイルを再読み込み
            _ = manager.LoadAsync();
        }
    });

// 5. 手動同期（任意）
await GitHubSyncManager.Instance.SyncAsync(fileOptions.FilePath);
```

### JSON差分検出の使用

```csharp
// JSON文字列の差分を検出
var oldJson = @"{
    ""name"": ""Alice"",
    ""age"": 30,
    ""hobbies"": [""reading"", ""gaming""]
}";

var newJson = @"{
    ""name"": ""Alice"",
    ""age"": 31,
    ""hobbies"": [""reading"", ""gaming"", ""coding""],
    ""country"": ""Japan""
}";

var diffReport = JsonDiffDetector.DetectDifferences(oldJson, newJson);

Console.WriteLine(diffReport.GetSummary());
// 出力: 1 added, 0 removed, 2 modified

// 詳細表示
foreach (var change in diffReport.Changes)
{
    switch (change.ChangeType)
    {
        case JsonChangeType.Added:
            Console.WriteLine($"[追加] {change.PropertyPath} = {change.NewValue}");
            break;
        case JsonChangeType.Removed:
            Console.WriteLine($"[削除] {change.PropertyPath} = {change.OldValue}");
            break;
        case JsonChangeType.Modified:
            Console.WriteLine($"[変更] {change.PropertyPath}: {change.OldValue} → {change.NewValue}");
            break;
    }
}
```

### カスタムトリガー

```csharp
// カスタムトリガーの作成
public class OnApplicationExitBackupTrigger : BackupTrigger
{
    public override BackupTriggerType Type => BackupTriggerType.Custom;

    public override void Register(IFileManager fileManager, Func<Task<Result>> backupAction)
    {
        if (IsActive) return;

        Application.Current.Exit += async (s, e) =>
        {
            await backupAction();
        };

        IsActive = true;
    }

    public override void Unregister()
    {
        // クリーンアップ
        IsActive = false;
    }
}
```

## スレッドセーフティ

FileManagerBaseはReaderWriterLockSlimを使用し、以下を保証します：

- **同時読み込み**: 複数スレッドから同時に読み込み可能
- **排他的書き込み**: 書き込み中は他の操作をブロック
- **デッドロック防止**: タイムアウト設定あり

```csharp
// 複数スレッドから安全にアクセス可能
await Task.WhenAll(
    manager.LoadAsync(),
    manager.LoadAsync(),
    manager.LoadAsync()
);
```

## ベストプラクティス

1. **Singleton管理**: ファイルマネージャーは各ファイルパスごとに1インスタンス
2. **適切なトリガー選択**: ファイルの重要度とアクセス頻度で選択
3. **バックアップ数の制限**: MaxBackupCountで古いバックアップを自動削除
4. **ロールバック設定**: 重要なファイルはMaxRetries=-1で全バックアップ試行
5. **GitHub Token管理**: 環境変数を使用し、コードにハードコードしない
6. **イベント購読**: FileSavedEvent、GitHubFileUpdatedEventなどでUI更新や他の処理を連携
7. **エラーハンドリング**: Resultパターンで常にエラーをチェック
8. **差分通知**: GitHubFileUpdatedEventのDiffReportを使用してユーザーに変更内容を通知

## 他モジュールとの統合

- **Results**: 一貫したエラーハンドリング
- **Logging**: 詳細な診断ログ
- **Events**: ReactiveUIによるイベント駆動
- **Time**: テスタブルな時刻管理
- **Resilience**: リトライ機能の統合可能
