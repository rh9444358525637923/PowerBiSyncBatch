
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

