# RhinoMCPServer

RhinocerosでModel Context Protocol (MCP)サーバーを実行するためのプラグインです。Rhinoの機能をMCPツールとして提供し、MCPクライアントと効率的な通信を実現します。

## 概要

このプラグインは、[Model Context Protocol](https://github.com/modelcontextprotocol/csharp-sdk)を使用してRhinoの機能をMCPクライアントに公開します。従来のWebSocket通信ではなく、Server-Sent Events (SSE)を採用することで、より効率的で軽量な双方向通信を実現しています。

## システム要件

- Rhino 9 WIP（Work In Progress：開発プレビュー版）
- .NET 8.0 Runtime

## 使用方法

### MCPサーバーの起動

1. Rhinoのコマンドラインに`StartMCPServerCommand`と入力します
2. ポート番号の設定
   - デフォルト：3001（Enterキーを押すと自動的に使用）
   - カスタム：任意のポート番号を入力可能
3. サーバー起動後、指定したポートでMCPクライアントからの接続を待機します

### MCPクライアントとの接続

現在、Claude DesktopのMCPクライアントはSSE接続に直接対応していないため、[標準入出力をSSEにブリッジするmcpサーバー](https://github.com/boilingdata/mcp-server-and-gw)を使用する必要があります。

## 提供されるMCPツール

### 基本ツール
- **echo**
  - 機能：入力テキストのエコーバック
  - パラメータ：
    - `message` (string) - エコーバックするテキスト

### LLM関連
- **sampleLLM**
  - 機能：MCPのサンプリング機能を使用したLLMレスポンス生成
  - パラメータ：
    - `prompt` (string) - LLMへのプロンプト
    - `maxTokens` (number) - 生成する最大トークン数

### Rhinoジオメトリ
- **sphere**
  - 機能：Rhino内での球体作成
  - パラメータ：
    - `radius` (number) - 球体の半径（単位：Rhinoの現在の単位系）

## ログ

サーバーのログは以下の場所に保存されます：
- アプリケーションディレクトリ内の`logs/TestServer_.log`

## ライセンス

本プロジェクトは[MITライセンス](./LICENSE)のもとで公開されています。詳細はLICENSEファイルをご確認ください。