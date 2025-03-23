# RhinoMCPServer

RhinocerosでModel Context Protocol (MCP)サーバーを起動するためのRhinocerosプラグインです。MCPクライアントとSSE (Server-Sent Events)で直接通信し、Rhinoの機能をMCPツールとして提供します。

## 概要

このプラグインは、[Model Context Protocol](https://github.com/modelcontextprotocol/csharp-sdk)を使用してRhinoの機能をMCPクライアントに公開します。
WebSocket通信を介さず、MCPクライアントとSSE接続することで、効率的な通信を実現しています。

## システム要件

- Rhino 9 WIP
- .NET 8.0


## 使用方法

### MCPサーバーの起動

1. Rhinoで`StartMCPServerCommand`コマンドを実行します。
2. ポート番号の入力を求められます（デフォルト：3001）。
   - Enterキーを押すとデフォルトのポート3001を使用します。
   - 任意のポート番号を入力することも可能です。
3. サーバーが起動し、指定したポートでMCPクライアントからの接続を待ち受けます。

### MCPクライアントからの接続

Claude DesktopのMCPクライアントは現在SSE接続に対応していないため、[標準入出力をSSEにブリッジするmcpサーバー](https://github.com/boilingdata/mcp-server-and-gw)を使用する必要があります。

## 提供されるMCPツール

このプラグインは以下のMCPツールを提供します：

- **echo**
   - 説明：入力されたテキストをエコーバックします。
   - パラメータ：`message` (string)

- **sampleLLM**
   - 説明：MCPのサンプリング機能を使用してLLMからレスポンスを生成します。
   - パラメータ：
     - `prompt` (string): LLMに送信するプロンプト
     - `maxTokens` (number): 生成する最大トークン数

- **sphere**
   - 説明：Rhino内に球体を作成します。
   - パラメータ：`radius` (number): 球体の半径


## ログ

サーバーのログは以下の場所に保存されます：
- アプリケーションディレクトリ内の`logs/TestServer_.log`

## ライセンス

このプロジェクトはMITのもとで公開されています。