Imports System.Net.Http
Imports System.Net.Http.Headers
Imports Microsoft.Identity.Client
Imports Newtonsoft.Json

Module Module1

    Sub Main()

        Try
            MainAsync().GetAwaiter().GetResult()
        Catch ex As Exception
            Console.WriteLine($"バッチ処理が異常終了しました: {ex.Message}")
        End Try

        Console.WriteLine("処理が完了しました")
    End Sub

    Private Async Function MainAsync() As Task

        Dim clientId As String = System.Configuration.ConfigurationManager.AppSettings("ClientId")
        Dim clientSecret As String = System.Configuration.ConfigurationManager.AppSettings("ClientSecret")
        Dim tenantId As String = System.Configuration.ConfigurationManager.AppSettings("TenantId")

        Dim client As New PowerBiBatchClient()
        client.Initialize(clientId, clientSecret, tenantId)

        Await client.GetPowerBiDataAsync()
    End Function

End Module


Public Class PowerBiBatchClient

    Private Shared ReadOnly _httpClient As New HttpClient()
    'This object will cache tokens in-memory - keep it as a singleton
    'このオブジェクトはメモリ内にトークンをキャッシュします - シングルトンとして保持してください
    Private _singletonApp As IConfidentialClientApplication
    Private _tenantId As String

    '' ログ受け取り用のメソッド
    'Private Sub LogMsal(level As LogLevel, message As String, containsPii As Boolean)
    '    Console.WriteLine($"[MSAL {level}] {message}")
    'End Sub

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

End Class

''' <summary>
''' Power BI API (Get Groups) のルートレスポンスを表すクラス
''' </summary>
Public Class PowerBiGroupResponse
    ' JSONのキー名がVB.NETの変数名と異なる場合（記号が含まれる場合など）は、JsonProperty属性で紐付けます
    <JsonProperty("@odata.count")>
    Public Property ODataCount As Integer

    <JsonProperty("value")>
    Public Property Value As List(Of PowerBiWorkspace)
End Class


''' <summary>
''' 各ワークスペースの情報を表すクラス
''' </summary>
Public Class PowerBiWorkspace
    <JsonProperty("id")>
    Public Property Id As String

    <JsonProperty("name")>
    Public Property Name As String

    <JsonProperty("isReadOnly")>
    Public Property IsReadOnly As Boolean

    <JsonProperty("isOnDedicatedCapacity")>
    Public Property IsOnDedicatedCapacity As Boolean

    <JsonProperty("type")>
    Public Property Type As String

    ' 必要に応じて、APIが返す他のプロパティ（capacityId など）もここに追加できます
End Class