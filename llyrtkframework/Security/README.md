# Security モジュール

データ暗号化、ハッシュ化、セキュアストレージを提供するモジュールです。

## 概要

Securityモジュールは、機密データの保護機能を提供します：

- **AES-256-GCM暗号化**: 最新の認証付き暗号化
- **SHA-256/SHA-512ハッシュ**: データ整合性検証
- **HMAC**: メッセージ認証コード
- **DPAPI セキュアストレージ**: Windows Data Protection API による機密データ保存
- **PBKDF2 鍵導出**: パスワードから安全な暗号化キーを生成

## 主要コンポーネント

### EncryptionService

AES-256-GCM による暗号化・復号化サービス。

```csharp
var encryptionService = new EncryptionService(logger);

// ランダムキーを生成
var key = encryptionService.GenerateKey();  // 32 bytes

// データを暗号化
var plaintext = "Secret Message";
var encryptResult = encryptionService.EncryptString(plaintext, key);
if (encryptResult.IsSuccess)
{
    var ciphertext = encryptResult.Value!;
    Console.WriteLine($"Encrypted: {ciphertext}");

    // データを復号化
    var decryptResult = encryptionService.DecryptString(ciphertext, key);
    if (decryptResult.IsSuccess)
    {
        Console.WriteLine($"Decrypted: {decryptResult.Value}");
    }
}
```

### パスワードベース暗号化

```csharp
var encryptionService = new EncryptionService(logger);

// ソルトを生成
var salt = encryptionService.GenerateSalt();  // 16 bytes

// パスワードからキーを導出
var password = "MySecurePassword123!";
var keyResult = encryptionService.DeriveKeyFromPassword(password, salt, iterations: 100000);

if (keyResult.IsSuccess)
{
    var key = keyResult.Value!;

    // 暗号化
    var plaintext = "Secret data";
    var encryptResult = encryptionService.EncryptString(plaintext, key);

    // ソルトと暗号文を保存（例: JSON）
    var data = new
    {
        Salt = Convert.ToBase64String(salt),
        Ciphertext = encryptResult.Value
    };
}
```

### バイナリデータの暗号化

```csharp
// ファイル内容を暗号化
var fileBytes = await File.ReadAllBytesAsync("document.pdf");

var key = encryptionService.GenerateKey();
var encryptResult = encryptionService.Encrypt(fileBytes, key);

if (encryptResult.IsSuccess)
{
    // 暗号化されたデータを保存
    await File.WriteAllBytesAsync("document.pdf.encrypted", encryptResult.Value!);

    // 復号化
    var encryptedBytes = await File.ReadAllBytesAsync("document.pdf.encrypted");
    var decryptResult = encryptionService.Decrypt(encryptedBytes, key);

    if (decryptResult.IsSuccess)
    {
        await File.WriteAllBytesAsync("document.pdf.decrypted", decryptResult.Value!);
    }
}
```

## HashService

SHA-256、SHA-512、HMAC によるハッシュ化サービス。

```csharp
var hashService = new HashService(logger);

// SHA-256 ハッシュ
var data = "Hello, World!";
var hashResult = hashService.ComputeSha256String(data);
if (hashResult.IsSuccess)
{
    Console.WriteLine($"SHA-256: {hashResult.Value}");
}

// SHA-512 ハッシュ
var hash512Result = hashService.ComputeSha512String(data);
if (hash512Result.IsSuccess)
{
    Console.WriteLine($"SHA-512: {hash512Result.Value}");
}
```

### ファイルハッシュ検証

```csharp
var hashService = new HashService(logger);

// ファイルのハッシュを計算
var fileHashResult = await hashService.ComputeFileSha256Async("document.pdf");
if (fileHashResult.IsSuccess)
{
    var fileHash = fileHashResult.Value!;
    Console.WriteLine($"File hash: {fileHash}");

    // 期待されるハッシュと比較
    var expectedHash = "a1b2c3d4...";
    if (hashService.CompareHashStrings(fileHash, expectedHash))
    {
        Console.WriteLine("File integrity verified");
    }
    else
    {
        Console.WriteLine("File has been modified!");
    }
}
```

### HMAC による認証

```csharp
var hashService = new HashService(logger);

// HMAC キーを生成
var hmacKey = new byte[32];
RandomNumberGenerator.Fill(hmacKey);

// メッセージの HMAC を計算
var message = "Important message";
var hmacResult = hashService.ComputeHmacSha256String(message, hmacKey);

if (hmacResult.IsSuccess)
{
    var hmac = hmacResult.Value!;
    Console.WriteLine($"HMAC: {hmac}");

    // 受信側で HMAC を検証
    var receivedMessage = "Important message";
    var verifyHmacResult = hashService.ComputeHmacSha256String(receivedMessage, hmacKey);

    if (verifyHmacResult.IsSuccess &&
        hashService.CompareHashStrings(hmac, verifyHmacResult.Value!))
    {
        Console.WriteLine("Message authentication successful");
    }
    else
    {
        Console.WriteLine("Message has been tampered with!");
    }
}
```

## SecureStorage

Windows DPAPI を使用した機密データの暗号化ストレージ。

```csharp
var appInfo = new ApplicationInfo();
var storagePath = Path.Combine(appInfo.ApplicationDataPath, "secure.json");
var secureStorage = new SecureStorage(logger, storagePath);

// 機密データを保存
await secureStorage.SaveAsync("ApiKey", "sk-1234567890abcdef");
await secureStorage.SaveAsync("DatabasePassword", "P@ssw0rd123!");

// データを読み込み
var apiKeyResult = await secureStorage.LoadAsync("ApiKey");
if (apiKeyResult.IsSuccess)
{
    var apiKey = apiKeyResult.Value!;
    // API キーを使用
}

// データを削除
await secureStorage.DeleteAsync("ApiKey");

// すべてのキーを取得
var keysResult = await secureStorage.GetAllKeysAsync();
if (keysResult.IsSuccess)
{
    foreach (var key in keysResult.Value!)
    {
        Console.WriteLine($"Stored key: {key}");
    }
}

// すべてクリア
await secureStorage.ClearAsync();
```

### DPAPI の仕組み

- **CurrentUser スコープ**: ログイン中のユーザーのみ復号化可能
- **自動鍵管理**: Windows が暗号化キーを管理
- **マスターキー**: ユーザーのログインパスワードから導出
- **セキュア**: 他のユーザーやプロセスからアクセス不可

```csharp
// DPAPI は自動的にユーザー固有のキーを使用
await secureStorage.SaveAsync("Token", "my-secret-token");
// -> Windows がユーザーのマスターキーで暗号化

// 同じユーザーでログインすれば復号化可能
var tokenResult = await secureStorage.LoadAsync("Token");
// -> Windows が自動的に復号化

// 別のユーザーでは復号化不可
// -> CryptographicException がスローされる
```

## SecurityExtensions

セキュリティ関連の便利な拡張メソッド。

### SecureString 変換

```csharp
// 文字列を SecureString に変換
var password = "MyPassword123!";
var securePassword = password.ToSecureString();

// SecureString を文字列に変換（使用後は必ずクリア）
var plainPassword = securePassword.ToUnsecureString();
// ... 使用 ...
// メモリから削除（C# の文字列は immutable なので完全には削除できない）
```

### バイト配列操作

```csharp
// バイト配列をゼロクリア
var sensitiveData = new byte[] { 1, 2, 3, 4, 5 };
// ... 使用 ...
sensitiveData.Clear();  // すべて 0 に上書き

// 16進数変換
var bytes = new byte[] { 0x12, 0x34, 0x56, 0x78 };
var hex = bytes.ToHexString();  // "12345678"

var decoded = hex.FromHexString();  // { 0x12, 0x34, 0x56, 0x78 }

// Base64 変換
var base64 = bytes.ToBase64();  // "EjRWeA=="
var decodedBase64 = base64.FromBase64();  // { 0x12, 0x34, 0x56, 0x78 }
```

## 使用例

### 設定ファイルの暗号化

```csharp
public class EncryptedConfigurationManager
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<EncryptedConfigurationManager> _logger;
    private readonly byte[] _key;

    public EncryptedConfigurationManager(
        IEncryptionService encryptionService,
        ILogger<EncryptedConfigurationManager> logger,
        byte[] key)
    {
        _encryptionService = encryptionService;
        _logger = logger;
        _key = key;
    }

    public async Task<Result> SaveConfigAsync(string filePath, object config)
    {
        try
        {
            // JSON にシリアライズ
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            // 暗号化
            var encryptResult = _encryptionService.EncryptString(json, _key);
            if (encryptResult.IsFailure)
                return Result.Failure(encryptResult.ErrorMessage);

            // ファイルに保存
            await File.WriteAllTextAsync(filePath, encryptResult.Value!);

            _logger.LogInformation("Encrypted config saved: {FilePath}", filePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save encrypted config");
            return Result.FromException(ex, "Failed to save encrypted config");
        }
    }

    public async Task<Result<T>> LoadConfigAsync<T>(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<T>.Failure("Config file not found");

            // ファイルから読み込み
            var ciphertext = await File.ReadAllTextAsync(filePath);

            // 復号化
            var decryptResult = _encryptionService.DecryptString(ciphertext, _key);
            if (decryptResult.IsFailure)
                return Result<T>.Failure(decryptResult.ErrorMessage);

            // JSON からデシリアライズ
            var config = JsonSerializer.Deserialize<T>(decryptResult.Value!);
            if (config == null)
                return Result<T>.Failure("Failed to deserialize config");

            _logger.LogInformation("Encrypted config loaded: {FilePath}", filePath);
            return Result<T>.Success(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load encrypted config");
            return Result<T>.FromException(ex, "Failed to load encrypted config");
        }
    }
}
```

### API トークンの保護

```csharp
public class ApiTokenManager
{
    private readonly ISecureStorage _secureStorage;
    private readonly ILogger<ApiTokenManager> _logger;

    public ApiTokenManager(ISecureStorage secureStorage, ILogger<ApiTokenManager> logger)
    {
        _secureStorage = secureStorage;
        _logger = logger;
    }

    public async Task<Result> SaveTokenAsync(string serviceName, string token)
    {
        var key = $"Token_{serviceName}";
        var result = await _secureStorage.SaveAsync(key, token);

        if (result.IsSuccess)
        {
            _logger.LogInformation("API token saved for service: {ServiceName}", serviceName);
        }

        return result;
    }

    public async Task<Result<string>> GetTokenAsync(string serviceName)
    {
        var key = $"Token_{serviceName}";
        return await _secureStorage.LoadAsync(key);
    }

    public async Task<Result> DeleteTokenAsync(string serviceName)
    {
        var key = $"Token_{serviceName}";
        return await _secureStorage.DeleteAsync(key);
    }
}

// 使用例
var tokenManager = new ApiTokenManager(secureStorage, logger);

// トークンを保存
await tokenManager.SaveTokenAsync("GitHub", "ghp_abc123...");

// トークンを取得
var tokenResult = await tokenManager.GetTokenAsync("GitHub");
if (tokenResult.IsSuccess)
{
    var token = tokenResult.Value!;
    // GitHub API を呼び出し
}
```

### ライセンスキー検証

```csharp
public class LicenseValidator
{
    private readonly IHashService _hashService;
    private readonly byte[] _hmacKey;

    public LicenseValidator(IHashService hashService, byte[] hmacKey)
    {
        _hashService = hashService;
        _hmacKey = hmacKey;
    }

    public Result<bool> ValidateLicense(string licenseKey, string signature)
    {
        try
        {
            // ライセンスキーの HMAC を計算
            var hmacResult = _hashService.ComputeHmacSha256String(licenseKey, _hmacKey);
            if (hmacResult.IsFailure)
                return Result<bool>.Failure(hmacResult.ErrorMessage);

            // 署名を検証
            var isValid = _hashService.CompareHashStrings(hmacResult.Value!, signature);

            return Result<bool>.Success(isValid);
        }
        catch (Exception ex)
        {
            return Result<bool>.FromException(ex, "License validation failed");
        }
    }

    public Result<string> GenerateLicenseSignature(string licenseKey)
    {
        return _hashService.ComputeHmacSha256String(licenseKey, _hmacKey);
    }
}
```

## DI統合

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    var appInfo = new ApplicationInfo();

    // Singleton登録
    containerRegistry.RegisterSingleton<IEncryptionService, EncryptionService>();
    containerRegistry.RegisterSingleton<IHashService, HashService>();

    // SecureStorage
    containerRegistry.RegisterSingleton<ISecureStorage>(provider =>
    {
        var logger = provider.Resolve<ILogger<SecureStorage>>();
        var storagePath = Path.Combine(appInfo.ApplicationDataPath, "secure.json");
        return new SecureStorage(logger, storagePath);
    });

    // カスタムサービス
    containerRegistry.RegisterSingleton<EncryptedConfigurationManager>();
    containerRegistry.RegisterSingleton<ApiTokenManager>();
}
```

## ベストプラクティス

1. **キーの安全な管理**: 暗号化キーをコードに埋め込まない
2. **DPAPI の使用**: Windows環境では DPAPI を優先
3. **ソルトの保存**: パスワードベース暗号化ではソルトも保存
4. **タイミング攻撃対策**: ハッシュ比較は `CompareHashes` を使用
5. **メモリクリア**: 機密データ使用後は `Clear()` でゼロクリア

```csharp
// 良い例: キーを SecureStorage に保存
var encryptionKey = encryptionService.GenerateKey();
await secureStorage.SaveAsync("EncryptionKey", Convert.ToBase64String(encryptionKey));

// 使用時に読み込み
var keyResult = await secureStorage.LoadAsync("EncryptionKey");
if (keyResult.IsSuccess)
{
    var key = Convert.FromBase64String(keyResult.Value!);
    // 暗号化処理
    // ...
    // 使用後はクリア
    key.Clear();
}

// 悪い例: キーをハードコード
var badKey = new byte[] { 1, 2, 3, 4, ... };  // NG!
```

```csharp
// 良い例: タイミング攻撃耐性あり
if (hashService.CompareHashStrings(computed, expected))
{
    // 検証成功
}

// 悪い例: タイミング攻撃に脆弱
if (computed == expected)  // NG!
{
    // 文字列比較は処理時間から情報が漏れる
}
```

## セキュリティ考慮事項

### AES-256-GCM の利点

- **認証付き暗号化**: 改ざん検知が組み込まれている
- **高速**: ハードウェアアクセラレーション対応
- **NIST推奨**: 米国標準技術研究所が推奨

### DPAPI の制限

- **Windows専用**: Linux/macOS では使用不可
- **ユーザー依存**: ユーザーごとに異なるキー
- **バックアップ**: マスターキーのバックアップが必要

### パスワードベース暗号化

- **十分なイテレーション**: 最低100,000回以上
- **ランダムソルト**: 毎回異なるソルトを生成
- **強いパスワード**: 英数字+記号、12文字以上推奨

## 他モジュールとの統合

- **FileManagement**: ファイルの暗号化保存
- **Configuration**: 設定の暗号化
- **Application**: API トークンの保護
- **StateManagement**: 状態データの暗号化
