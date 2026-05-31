
Imports System.Threading

Module Module1

    Sub Main()

        Try

            Console.WriteLine($"[Main] スタート - スレッドID: {Thread.CurrentThread.ManagedThreadId}")

            Call MainAsync()

            Console.WriteLine($"[Main] エンド - スレッドID: {Thread.CurrentThread.ManagedThreadId}")


        Catch ex As Exception
            Console.WriteLine($"バッチ処理が異常終了しました: {ex.Message}")
        End Try

        Console.WriteLine("処理が完了しました")
    End Sub

    Private Function MainAsync() As Task

        Console.WriteLine($"[MainAsync] スタート - スレッドID: {Thread.CurrentThread.ManagedThreadId}")

        Dim clientId As String = System.Configuration.ConfigurationManager.AppSettings("ClientId")
        Dim clientSecret As String = System.Configuration.ConfigurationManager.AppSettings("ClientSecret")
        Dim tenantId As String = System.Configuration.ConfigurationManager.AppSettings("TenantId")

        Dim client As New PowerBiBatchClient()
        client.Initialize(clientId, clientSecret, tenantId)

        client.GetPowerBiDataAsync().Wait()

        Console.WriteLine($"[MainAsync] エンド - スレッドID: {Thread.CurrentThread.ManagedThreadId}")

    End Function

End Module

