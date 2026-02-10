# FileManagement モジュール改善実装

## 改善内容

### 1. フラグ管理の一貫性改善

#### 変更前の問題点
- `IsModified` と `HasUnsavedChanges` の2つのフラグが混在
- フラグの意味が不明瞭
- 操作メソッドが不統一

#### 変更後
```csharp
// 明確な命名
bool HasChangesSinceBackup   // バックアップフラグ
bool HasPendingAutoSave      // 自動保存フラグ

// 一貫した操作メソッド
void MarkAsChanged()         // 両方のフラグを立てる
void ClearAutoSaveFlag()     // 自動保存フラグのみクリア
void ClearBackupFlag()       // バックアップフラグのみクリア
```

#### フラグの状態遷移
```
データ変更時:
  HasPendingAutoSave = true
  HasChangesSinceBackup = true

自動保存実行後:
  HasPendingAutoSave = false      ← クリア
  HasChangesSinceBackup = true    ← 保持

バックアップ実行後:
  HasPendingAutoSave = (変更なし)
  HasChangesSinceBackup = false   ← クリア
```

### 2. FileManagerRegistry の責務分離

#### 変更前の問題点
FileManagerRegistry が複数の責務を持っていた:
1. ファイルマネージャーの登録管理
2. 自動保存システム
3. バックアップ統括実行
4. GitHub同期統括実行

#### 変更後のアーキテクチャ

```
FileManagementCoordinator (ファサード)
├── FileManagerRegistry (レジストリ)
├── AutoSaveService (自動保存)
├── BackupOrchestrator (バックアップ統括)
└── GitHubSyncOrchestrator (GitHub同期統括)
```

## 新しいクラス

### AutoSaveService
自動保存専用サービス

**場所**: `llyrtkframework/FileManagement/Services/AutoSaveService.cs`

**責務**:
- 自動保存のポーリング
- 自動保存の実行
- 自動保存の開始/停止管理

**主なメソッド**:
```csharp
void Start(TimeSpan? interval = null)
void Stop()
Task<Result> ExecuteNowAsync(CancellationToken cancellationToken = default)
bool IsRunning { get; }
```

### BackupOrchestrator
バックアップ処理のオーケストレーター

**場所**: `llyrtkframework/FileManagement/Services/BackupOrchestrator.cs`

**責務**:
- 全体バックアップの統括
- 差分バックアップの統括
- 条件付きバックアップの統括

**主なメソッド**:
```csharp
Task<Result> BackupAllAsync(CancellationToken cancellationToken = default)
Task<Result> BackupAsync(string filePath, CancellationToken cancellationToken = default)
Task<Result> IncrementalBackupAsync(CancellationToken cancellationToken = default)
Task<Result> BackupWhereAsync(Predicate<IFileManager> predicate, CancellationToken cancellationToken = default)
```

### GitHubSyncOrchestrator
GitHub同期処理のオーケストレーター

**場所**: `llyrtkframework/FileManagement/Services/GitHubSyncOrchestrator.cs`

**責務**:
- GitHub同期の統括
- 並列同期の制御

**主なメソッド**:
```csharp
Task<Result> SyncAllAsync(CancellationToken cancellationToken = default)
Task<Result> SyncAsync(string filePath, CancellationToken cancellationToken = default)
Task<Result> SyncAllAsync(int maxParallelism, CancellationToken cancellationToken = default)
```

### FileManagementCoordinator
ファイル管理システム全体の統一インターフェース

**場所**: `llyrtkframework/FileManagement/FileManagementCoordinator.cs`

**責務**:
- すべてのサービスへの統一されたアクセス
- 各サービスのライフサイクル管理

## 使用方法

### 従来の使い方（互換性あり）

```csharp
// FileManagerBase を継承したクラスは変更不要
public class MyFileManager : FileManagerBase<MyData>
{
    protected override string ConfigureFilePath() => "data/myfile.json";
    // 他の設定...
}

// 従来通りの使い方も可能
FileManagerRegistry.Instance.StartAutoSave();
await FileManagerRegistry.Instance.ExecuteBackupAllAsync();
```

### 新しい使い方（推奨）

```csharp
// アプリケーション起動時
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var eventAggregator = new EventAggregator();

var coordinator = new FileManagementCoordinator(loggerFactory, eventAggregator);

// 自動保存の開始
coordinator.StartAutoSave(TimeSpan.FromMilliseconds(500));

// バックアップ実行
await coordinator.BackupAllAsync();
await coordinator.IncrementalBackupAsync();

// GitHub同期
await coordinator.SyncAllWithGitHubAsync();

// 特定のファイルのみ操作
await coordinator.BackupAsync("data/myfile.json");
await coordinator.SyncWithGitHubAsync("data/myfile.json");

// 条件付きバックアップ
await coordinator.BackupWhereAsync(manager =>
    manager.HasChangesSinceBackup && manager.FileExists);

// 即座に自動保存を実行
await coordinator.ExecuteAutoSaveNowAsync();

// アプリケーション終了時
coordinator.StopAutoSave();
coordinator.Dispose();
```

### サービスを個別に使用

```csharp
var registry = FileManagerRegistry.Instance;
var logger = loggerFactory.CreateLogger<AutoSaveService>();

// 自動保存サービスのみ使用
var autoSaveService = new AutoSaveService(registry, logger, eventAggregator);
autoSaveService.Start();
await autoSaveService.ExecuteNowAsync();
autoSaveService.Stop();

// バックアップオーケストレーターのみ使用
var backupOrchestrator = new BackupOrchestrator(
    registry,
    loggerFactory.CreateLogger<BackupOrchestrator>(),
    eventAggregator);
await backupOrchestrator.BackupAllAsync();
```

## マイグレーションガイド

### ステップ1: フラグ名の確認
既存のコードで以下のプロパティ/メソッドを使用している場合は更新が必要です:

```csharp
// 変更前 → 変更後
IsModified              → HasChangesSinceBackup
HasUnsavedChanges       → HasPendingAutoSave
ClearUnsavedChanges()   → ClearAutoSaveFlag()
```

### ステップ2: FileManagerRegistry の直接使用を確認
以下のメソッドを使用している場合、FileManagementCoordinator への移行を検討:

```csharp
// 従来（まだ動作します）
FileManagerRegistry.Instance.StartAutoSave();
await FileManagerRegistry.Instance.ExecuteBackupAllAsync();

// 新しい方法（推奨）
var coordinator = new FileManagementCoordinator(loggerFactory, eventAggregator);
coordinator.StartAutoSave();
await coordinator.BackupAllAsync();
```

### ステップ3: FileManagerBase 継承クラスは変更不要
FileManagerBase を継承したクラスは**何も変更する必要がありません**。
内部的にフラグ名が変更されただけで、外部インターフェースは互換性が保たれています。

## 利点

### フラグ管理改善の利点
1. **明確な意味**: フラグの目的が名前から明確
2. **独立した制御**: 自動保存とバックアップのフラグを独立して管理
3. **一貫した操作**: すべてのフラグ操作が統一されたメソッドで実行可能

### 責務分離の利点
1. **単一責任原則**: 各クラスが1つの責務のみ持つ
2. **テスタビリティ**: 各サービスを独立してテスト可能
3. **保守性**: 変更の影響範囲が明確
4. **拡張性**: 新しいオーケストレーターの追加が容易
5. **再利用性**: 各サービスを個別に利用可能

## 互換性

### 後方互換性
- FileManagerBase を継承した既存のクラスは**変更不要**
- FileManagerRegistry の既存メソッドは**すべて動作**（非推奨だが利用可能）
- 既存のイベント、トリガーシステムは**そのまま動作**

### 推奨される移行
新規プロジェクトや大規模なリファクタリング時には、FileManagementCoordinator の使用を推奨します。
既存のプロジェクトは段階的に移行可能です。
