# README: MSAL.NET を利用したデーモンアプリ（夜間バッチ）の Entra ID 認証基盤

## 概要
本アプリはVB.NET Framework MSAL.NETを用いてサービスプリンシパル、クライアントシークレットを利用して認証、 Power BI REST APIを実行するデーモンアプリを実装

* 公式ドキュメント：[Microsoft Authentication Library for .NET](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/)
* Nuget：[Microsoft.Identity.Client](https://www.nuget.org/packages/Microsoft.Identity.Client/)

---

## 1. デーモンアプリとは？
本プロジェクトで構築するシステムは「デーモンアプリ（Daemon Application）」に分類されます。

* **名前の由来:** ギリシャ神話に登場する「人間と神々の間で裏から手助けをする精霊（Daemon）」が語源。UNIX系OSで「裏側で働き続けるプログラム」を指す言葉として定着しました。
* **特徴:** 人間（ユーザー）による画面操作（ID/パスワードの手入力やボタンのクリック）を一切介さず、完全にバックグラウンドで自律的に動作するプログラムのことです。
* **代表例:** 指定した時間に起動する「夜間バッチ」、システム間のデータを定期的に同期する「連携モジュール」など。

### 【重要】Entra ID における認証方式の比較表

| 項目 | デーモンアプリ★ | ユーザー利用アプリ |
| :--- | :--- | :--- |
| **利用する権限** | **アプリ自身（サービスプリンシパル）** | ユーザー自身 |
| **トークンの種類** | **アプリケーショントークン** | ユーザートークン |
| **認証フロー** | **クライアント資格情報フロー（Client credential flows）** | 承認コードフロー など |
| **使用するMSALクラス** | **ConfidentialClientApplication (機密)** | PublicClientApplication (パブリック) |
| **要求するスコープ** | **常に .default** | ユーザーが都度同意した権限 |
| **認証に使う情報** | **クライアントシークレット / 証明書** | ユーザーID と パスワード |

---

## 2. 事前設定

デーモンアプリでは、事前の設定で与えられた「アプリ自身の権限」を使用して認証を行います（クライアント資格情報フロー）。
アクセス権限（スコープ）は常に事前承認済みのデフォルト権限を示すURLを指定します。


📝 実装コード（スコープの指定）


    ' サービスプリンシパルで接続の場合、必ずdefaultスコープを指定
    ' 上記手順で付与した権限が、この .default のリクエストによって一括で適用される　個別付与は不可
    Dim scopes As String() = New String() {"https://analysis.windows.net/powerbi/api/.default"}


* 公式ドキュメント：[クライアント資格情報フロー（Client credential flows）について](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows)

### 【重要1】Entra ID ポータルでのアクセス許可の事前設定
コード内で `.default` スコープを使用して通信を成功させるためには、事前に Entra ID の管理画面でアプリに対して API の権限を割り当て、「管理者の同意」を完了しておく必要があります。

**■ Entra ID での設定手順**
1. [Microsoft Entra 管理センター](https://entra.microsoft.com/)（または Azure Portal）の **[アプリの登録]** から該当のアプリ（サービスプリンシパル）を選択します。
2. 左側メニューの **[API のアクセス許可]** を開き、**[アクセス許可の追加]** をクリックします。
3. **[Power BI Service]** を選択します。
4. デーモンアプリ（ユーザー不在）であるため、必ず **[アプリケーションのアクセス許可]** を選択します（※「委任されたアクセス許可」ではありません）。
5. 必要な権限（例: `Tenant.Read.All` や `Workspace.Read.All` など、要件に応じたもの）にチェックを入れて追加します。
6. 追加後、一覧画面の上部にある **[＜テナント名＞ に管理者の同意を与えます]** ボタンを必ずクリックし、ステータス列を緑色のチェックマーク（付与済み）にします。

* 公式ドキュメント：[Web API にアクセスするようにクライアント アプリケーションを構成する](https://learn.microsoft.com/ja-jp/entra/identity-platform/quickstart-configure-app-access-web-apis)


### 【重要2】Power BI ワークスペースへのサービスプリンシパルの追加
Entra ID でアプリを登録しただけでは、Power BI のデータにはアクセスできません。実際のデータが保存されている「ワークスペース」のメンバーとして、
このサービスプリンシパル（アプリ）を明示的に追加する必要があります。これを行わないと、API呼び出し時に `403 Forbidden` などのエラーが発生します。

**■ Power BI サービスでの設定手順**
1. **前提条件:** Power BI テナント管理者が、「管理ポータル」＞「テナント設定」＞「開発者向け設定」にて、**[サービス プリンシパルに Power BI API の使用を許可する]** を有効にしている必要があります。
2. Power BI サービス (app.powerbi.com) にログインし、バッチ処理の対象となる**ワークスペース**を開きます。
3. 画面上部の **[アクセスの管理]** をクリックします。
4. 「ユーザーまたはグループの追加」欄で、Entra ID で登録した**アプリの名前**（サービスプリンシパル名）を入力・検索して選択します。
5. 役割（ロール）として、要件に応じて **[メンバー]** または **[共同作成者]** などを選択します（※閲覧者では更新APIなどが弾かれる場合があります）。
6. **[追加]** をクリックして完了です。

* 公式ドキュメント：[Power BI サービス プリンシパルを使用する（Power BI側の設定）](https://learn.microsoft.com/ja-jp/power-bi/developer/embedded/embed-service-principal)




---
## 3. 実装（初期化）

`ConfidentialClientApplication` クラスを初期化します。

### 【重要】 `.WithAuthority` の必須性について
デーモンアプリ（`ConfidentialClientApplication`）を構築する場合、`.WithAuthority` を使用して**「自社のテナントIDを含めた専用窓口のURL」を指定することが必須**となります。

これを省略した場合、MSALはデフォルトで「世界共通窓口（common）」へアクセスしようとします。人間が使うアプリであれば、ログイン画面で入力されたメールアドレスから Entra ID がテナントを特定できますが、画面を持たないデーモンアプリではテナントを特定するヒントが出せないため、「テナントが特定できません」というエラー（`MsalServiceException`）になり認証が失敗します。そのため、最初からURLでテナントをピンポイントに指定する必要があります。

📝 **実装コード（初期化とリソース管理）**

    ''' <summary>
    ''' MSAL アプリケーションの初期化を行います。
    ''' </summary>
    Public Sub Initialize(clientId As String, clientSecret As String, tenantId As String)

        _tenantId = tenantId

        If _singletonApp Is Nothing Then
            ' Don't specify authority here, we'll do it on the request 
            ' ここでは authority（機関）を指定せず、リクエスト時に指定します
            ' ログ出力が必要な場合は.WithLogging()をチェーンする .WithLogging(AddressOf LogMsal) _
            _singletonApp = ConfidentialClientApplicationBuilder.Create(clientId) _
                .WithClientSecret(clientSecret) _
                .Build()
        End If
    End Sub

    
* 公式ドキュメント：[Public/Confidential Client Applicationの初期化](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/getting-started/initializing-client-applications)
  （※ `.WithAuthority` についての記載あり）
* 公式ドキュメント：[クライアント資格情報フロー（Client credential flows）について](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows)
  （※ トークン取得に特化したページのため `.WithAuthority` の記載なし）
* 公式ドキュメント：[MSAL.NET のベストプラクティス](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/getting-started/best-practices)
  （※ アプリや HttpClient のシングルトン設計について記載あり）
---

## 4. トークン取得

MSAL.NET は、アクセストークンの有効期限管理とキャッシュ機構を内部で自動処理します。この仕組みは、取得するトークンの種類（Confidential、Public）によってコードの書き方が大きく変わります。

### デーモンアプリ（アプリケーショントークン）の場合【今回の実装】
AcquireTokenForClient は、デーモンアプリにおける**アプリケーショントークン取得専用**のメソッドです。
これを使用する場合、開発者が手動でキャッシュを確認する AcquireTokenSilent を呼び出す必要はありません。
この1行を記述するだけで、MSALが裏側で「キャッシュがあればそれを返し、期限切れならEntra IDと通信して再取得する」という最適化された動作を全自動で行います。

📝 実装コード（デーモンアプリのトークン取得）

        Dim scopes As String() = New String() {"https://analysis.windows.net/powerbi/api/.default"}

        Try
            Console.WriteLine("Entra ID からアクセストークンを取得します...")

            ' Uses the token cache automatically, which is optimized for multi-tenant access
            ' トークンキャッシュを自動的に使用します。これはマルチテナントアクセス用に最適化されています（AcquireTokenForClientはキャッシュ確認の機能もあるのでAcquireTokenSilentは不要）
            ' When making the request, specify the tenant-based authority
            ' リクエストを行う際に、テナントベースの authority（機関）を指定します
            Dim authResult As AuthenticationResult = Await _singletonApp.AcquireTokenForClient(scopes) _
                .WithTenantId(_tenantId) _
                .ExecuteAsync()

            Console.WriteLine("トークン取得成功。Power BI API を呼び出します...")
            
* 公式ドキュメント：[クライアント資格情報フロー（Client credential flows）について](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows)

### 【参考】ユーザー利用アプリ（ユーザートークン）の場合
ユーザー利用アプリ（ユーザートークン）を作る場合は、AcquireTokenForClient は使用できません。
そのため、以下のように「まず手動でキャッシュを確認（Silent）し、ダメなら画面を出す（Interactive）」という分岐処理を自作しなければなりません。
デーモンアプリではこの面倒な記述が一切不要になります。

📝 参考コード（ユーザートークンの場合に必要な分岐処理）

    ' ※今回は使わない、人間向けアプリでの書き方
    Try
        ' 1. キャッシュ確認
        result = Await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync()
    Catch ex As MsalUiRequiredException
        ' 2. キャッシュが空、または期限切れの場合は画面をポップアップさせてログインさせる
        result = Await app.AcquireTokenInteractive(scopes).ExecuteAsync()
    End Try

* 公式ドキュメント：[トークン取得の概要とキャッシュ機構](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/acquiring-tokens/overview)
    
---

## 45 例外処理

### 📊 例外の種類

| 例外クラス | 発生元 | 主な原因・特徴 | バッチ処理での対応方針 |
| :--- | :--- | :--- | :--- |
| **`MsalServiceException`** | サーバー側<br>(Entra ID) | アプリの権限不足、シークレットの期限切れ、テナント指定ミスなど、Entra ID サーバーから明確に拒否されたエラー。 | 原因調査のため、例外オブジェクトから `ErrorCode` と `CorrelationId` (相関ID) を必ずログに残す。原則として即座に異常終了させる。 |
| **`MsalClientException`** | クライアント側<br>(実行環境) | ネットワークの切断、プロキシ設定の不備、設定値のフォーマット違反など、手元の環境に起因するエラー。 | エラーメッセージをログに残す。一時的なネットワーク障害の可能性があるため、リトライの対象になり得る。 |
| **`MsalUiRequiredException`** | サーバー側 | パスワードの期限切れや多要素認証（MFA）など、「人間に画面操作をしてほしい」という要求エラー。 | **※デーモンアプリでは絶対に発生しない。**（画面操作が不可能なため、対処コードを書く必要はない） |
| **`Exception`** | その他 | メモリ不足など、MSAL以外の予期せぬ重大なシステムエラー。 | システムエラーとして詳細なスタックトレースを記録し、異常終了させる。 |

📝 実装コード

    Try
        Dim authResult As AuthenticationResult = Await _singletonApp.AcquireTokenForClient(scopes) _
            .WithTenantId(_tenantId) _
            .ExecuteAsync()
    
    Catch ex As MsalServiceException
        Console.WriteLine($"[認証エラー] {ex.ErrorCode} (相関ID: {ex.CorrelationId})")
        Console.WriteLine($"詳細: {ex.Message}")
    Catch ex As MsalClientException
        Console.WriteLine($"[クライアントエラー] {ex.Message}")
    Catch ex As Exception
        Console.WriteLine($"[システムエラー] {ex.Message}")
    End Try

> Note: 大規模バッチで高度な再試行（HTTP 429制限回避など）が必要な場合は、自作のループ処理ではなく .NET 標準ライブラリである Polly の導入を推奨します。

* 公式ドキュメント：[MSAL.NET の例外処理とエラーの種類](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/advanced/exceptions/)

---

## 6. Logging

ブラックボックス化を防ぐため、初期化時の Builder チェーンに .WithLogging() を追加し、MSAL 内部の通信状況を出力

📝 実装コード（ロギングの設定）

    ' ログ受け取り用のメソッド
    Private Sub LogMsal(level As LogLevel, message As String, containsPii As Boolean)
        Console.WriteLine($"[MSAL {level}] {message}")
    End Sub
    
    ''' <summary>
    ''' MSAL アプリケーションの初期化を行います。
    ''' </summary>
    Public Sub Initialize(clientId As String, clientSecret As String, tenantId As String)

        _tenantId = tenantId

        If _singletonApp Is Nothing Then
        
            ' ログ出力が必要な場合は.WithLogging()をチェーンする .WithLogging(AddressOf LogMsal) _
            _singletonApp = ConfidentialClientApplicationBuilder.Create(clientId) _
                .WithClientSecret(clientSecret) _
                .WithLogging(AddressOf LogMsal) _
                .Build()
        End If
    End Sub

> Note: .WithLogging(AddressOf LogMSAL) と省略した場合、セキュリティ保護のため個人情報（PII）のログ出力は自動的に False（無効）として扱われ、機密情報がログに出力されない安全な設計になっています。

* 公式ドキュメント：[MSAL.NET でのログ出力設定](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/advanced/exceptions/msal-logging)

---

## 7. HttpClient を使用した Web API の呼び出し

取得したアクセストークンを `HttpClient` の `Authorization` ヘッダーにセットし、Power BI の API を実行します。

### 重要なステップ
1. **Authorization ヘッダーの設定:** トークンを `Bearer`（所有者）としてセットします。
2. **HTTPステータスコードの判定:** `IsSuccessStatusCode` で判定し、デーモンアプリ特有のエラー（`401`、`403`、`429`）に備えます。
特に `403 Forbidden` は、前述の「ワークスペースへのアプリ追加」が漏れている場合によく発生します。

📝 **実装コード（API呼び出し）**

    ''' <summary>
    ''' トークンを取得し、Power BI REST API を呼び出します。
    ''' </summary>
    Public Async Function GetPowerBiDataAsync() As Task
        ' サービスプリンシパルで接続の場合、必ずdefaultスコープを指定
        ' 上記手順で付与した権限が、この .default のリクエストによって一括で適用される　個別付与は不可
        Dim scopes As String() = New String() {"https://analysis.windows.net/powerbi/api/.default"}

        Try
            Console.WriteLine("Entra ID からアクセストークンを取得します...")

            ' Uses the token cache automatically, which is optimized for multi-tenant access
            ' トークンキャッシュを自動的に使用します。これはマルチテナントアクセス用に最適化されています（AcquireTokenForClientはキャッシュ確認の機能もあるのでAcquireTokenSilentは不要）
            ' When making the request, specify the tenant-based authority
            ' リクエストを行う際に、テナントベースの authority（機関）を指定します
            Dim authResult As AuthenticationResult = Await _singletonApp.AcquireTokenForClient(scopes) _
                .WithTenantId(_tenantId) _
                .ExecuteAsync()

            Console.WriteLine("トークン取得成功。Power BI API を呼び出します...")

            ' 取得したトークンを Authorization ヘッダーにセット
            _httpClient.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", authResult.AccessToken)

            ' Call the web API.
            ' Web API を呼び出します。
            Dim response As HttpResponseMessage = Await _httpClient.GetAsync("https://api.powerbi.com/v1.0/myorg/groups")

            If response.IsSuccessStatusCode Then
                Dim responseData As String = Await response.Content.ReadAsStringAsync()
                Console.WriteLine("API呼び出し成功！")
                Dim parsedData As PowerBiGroupResponse = JsonConvert.DeserializeObject(Of PowerBiGroupResponse)(responseData)

                Console.WriteLine($"取得したワークスペース数: {parsedData.Value.Count}")

                For Each workspace In parsedData.Value
                    Console.WriteLine($"--------------------------------------------------")
                    Console.WriteLine($"ワークスペースID : {workspace.Id}")
                    Console.WriteLine($"ワークスペース名 : {workspace.Name}")
                    Console.WriteLine($"タイプ           : {workspace.Type}")
                Next
                Console.WriteLine($"--------------------------------------------------")

            Else
                Console.WriteLine($"APIアクセス拒否: {response.StatusCode} - {response.ReasonPhrase}")
            End If

        Catch ex As MsalServiceException
            Console.WriteLine($"[認証エラー] {ex.ErrorCode} (相関ID: {ex.CorrelationId})")
            Console.WriteLine($"詳細: {ex.Message}")
        Catch ex As MsalClientException
            Console.WriteLine($"[クライアントエラー] {ex.Message}")
        Catch ex As Exception
            Console.WriteLine($"[システムエラー] {ex.Message}")
        End Try
    End Function
    
    
* 公式ドキュメント：[Web API を呼び出すデーモン アプリ - Web API の呼び出し](https://learn.microsoft.com/ja-jp/entra/identity-platform/scenario-daemon-call-api?tabs=dotnet)

---

## 8. 今後のセキュリティ強化に向けた拡張性

現在は「クライアントシークレット（文字列パスワード）」による認証を想定していますが、将来的なセキュリティ要件の引き上げに対して、コードの根幹を変えずに「証明書ベースの認証」へ移行できる拡張性が担保されています。

📝 実装コード（将来的な証明書認証への移行イメージ）

    ' パスワード文字列の代わりに、サーバーにインストールされた証明書オブジェクトを読み込んで渡す
    Dim cert As X509Certificate2 = ReadCertificate(CertificateThumbprint)
    
    app = ConfidentialClientApplicationBuilder.Create(clientId) _
        .WithCertificate(cert) ' ← シークレットの代わりにここを変えるだけ
        .WithAuthority(New Uri($"https://login.microsoftonline.com/{tenantId}")) _
        .Build()

* 公式ドキュメント：[機密クライアント アプリケーションでの証明書資格情報の使用](https://learn.microsoft.com/ja-jp/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows#withcertificate)



---

## 9. サンプルコードと参考リポジトリ

### 開発者向け：Microsoft 公式サンプルリポジトリ

1. **[Entra ID (MSAL.NET) 公式デーモンアプリサンプル](https://github.com/Azure-Samples/active-directory-dotnetcore-daemon-v2)**
   * ConfidentialClientApplication を用いたサーバー間通信（デーモンアプリ）の公式ベストプラクティス集です。
   * ※ 1-Call-MSGraphはGraph API 用のサンプルですが、スコープを .default に変更するだけで Power BI に流用可能です。
2. **[Power BI 開発者向け公式サンプル集](https://github.com/microsoft/PowerBI-Developer-Samples)**
   * サービスプリンシパル（App Owns Data）を利用した Power BI REST API の具体的な呼び出し方や、レポート埋め込みの実装例が確認できます。