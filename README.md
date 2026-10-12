# VRC Group & Status Switcher

VRChatのグループイベント参加時に**グループバナー（掲示グループ）とステータスを自動的に切り替え**、イベント終了後やインスタンス退出時に**デフォルト設定へ自動復帰**させるWindowsデスクトップアプリケーションです。

---

## 🌟 主な機能

1. **グループインスタンス自動追従モード (推奨)**
   - VRChatで `~group(grp_...)` 形式のグループインスタンス（Group+、Group Public等）に入室すると、所属グループを自動検知して**即座にそのグループバナーをネームプレートに掲示**します。
   - ステータスメッセージも自動で `{group_name} イベント参加中` などのテンプレートに更新可能。

2. **インスタンス退出時の自動デフォルト復帰**
   - イベントインスタンスを抜けた際（ホームワールドに戻る、通常ワールド・別インスタンスへ移動、またはVRChat終了時）、**あらかじめ設定した「デフォルトグループ」および「デフォルトステータス」に自動復帰**します。
   - イベント後のバナー戻し忘れを完全に防止できます。

3. **高度なカスタムルール設定**
   - グループインスタンス形式になっていないイベント会場（プライベートインスタンスや特定ワールド）でも、**ワールドID (`wrld_...`)** や **ワールド名（部分一致）** でルールを設定可能。
   - 「このワールドに入ったらこのグループバナーを掲示し、ステータスをJoin Meにする」といった柔軟な自動化が可能です。

4. **ワンクリック手動切り替え**
   - 所属グループ一覧がカード形式で表示され、「今すぐ掲示」「デフォルト指定」をワンクリックで実行可能。

5. **安全な認証情報の保護 (DPAPI)**
   - ログイン時のセッションクッキーは Windows DPAPI（Data Protection API）によって暗号化されてローカルに保存されます。
   - 次回起動時からはパスワード入力なしで自動ログイン可能。パスワードが平文で保存されたり外部に送信されることはありません。
   - 2要素認証（TOTP / Email OTP）にも対応しています。

6. **軽量＆ネイティブ動作**
   - C# .NET WPFによるネイティブWindowsデスクトップアプリ。
   - VRChatクライアントの公式ログファイル (`output_log_*.txt`) をリアルタイム監視するため、VRChat本体の改造（MOD等）は一切不要で規約違反になりません。

---

## 📦 ダウンロード
ビルド済みの最新バイナリは [GitHub Releases](https://github.com/nezumi-tech/VRChat_Group_Status_Switcher/releases) からダウンロードできます：
- **`VRCGroupSwitcher-win-x64.zip`**: 軽量版（要 [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download) / サイズ約700KB）
- **`VRCGroupSwitcher-win-x64-standalone.zip`**: スタンドアロン版（ランタイム同梱、インストール不要で即動作）

---

## 🛠️ ビルド・実行方法

### 必要な環境
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) 以降
- Windows 10 / 11 (x64)

### 開発・通常実行
リポジトリをクローン後、プロジェクトディレクトリで以下を実行します：

```powershell
cd VRCGroupSwitcher
dotnet run
```

### 単一実行ファイル (.exe) のビルド
配布用の軽量な単一実行ファイルとしてビルドする場合は、以下のコマンドを実行します：

```powershell
cd VRCGroupSwitcher
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ..\PublishApp
```

- 出力先: `PublishApp\VRCGroupSwitcher.exe`
- ※実行環境に [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download) が必要です（サイズは約700KBと非常に軽量になります）。
- ランタイムを同梱した自己完結型（Self-contained）にしたい場合は、`--self-contained true` を指定してください。

---

## 📖 使い方ガイド

### 1. ログイン
- アプリを起動すると、ログイン画面が表示されます。
- VRChatのアカウント名（またはメールアドレス）とパスワードを入力して「ログイン」をクリックします。
- 2段階認証を設定している場合は、認証コード（6桁）入力画面が表示されますのでコードを入力してください。

### 2. デフォルトグループ＆ステータスの設定
- サイドバーの「🔧 一般設定」を開きます。
- **デフォルト掲示グループ**: イベント外で通常時に掲示しておきたいグループ（または掲示なし）を選択します。
- **デフォルトステータス**: 通常時のステータス（Active, Join Meなど）とステータスメッセージを設定します。
- **「インスタンス退出時に自動でデフォルトに復帰する」** にチェックを入れておきます。

### 3. イベントへの参加
- あとはアプリを起動したまま VRChat で遊ぶだけ！
- グループインスタンスに入ると、自動的にバナーとステータスが切り替わります。
- イベントが終了してインスタンスを抜けると、自動で元のデフォルト設定に戻ります。

### 4. カスタムルールの活用
- 「⚙️ 切替ルール」タブから、特定のワールドやインスタンスに対する独自ルールを自由に追加・編集できます。
- `{group_name}` や `{world_name}` などの変数を使って、ステータスメッセージを動的に生成できます。

---

## 🛡️ 仕様・動作詳細
- **ログ監視ディレクトリ**: `%USERPROFILE%\AppData\LocalLow\VRChat\VRChat\`
- **対応API**: VRChat Web API (`api.vrchat.cloud`)
- **設定ファイル保存先**: `%APPDATA%\VRCGroupStatusSwitcher\config.json`
- **暗号化認証情報**: `%APPDATA%\VRCGroupStatusSwitcher\auth.dat` (Windows DPAPI暗号化)
