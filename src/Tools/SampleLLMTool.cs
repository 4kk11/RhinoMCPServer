using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPServer.Tools
{
    public class SampleLLMTool : IMCPTool
    {

        public SampleLLMTool()
        {
        }

        public string Name => "sampleLLM";
        public string Description => "Samples from an LLM using MCP's sampling feature.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "prompt": {
                        "type": "string",
                        "description": "The prompt to send to the LLM"
                    },
                    "maxTokens": {
                        "type": "number",
                        "description": "Maximum number of tokens to generate"
                    }
                },
                "required": ["prompt", "maxTokens"]
            }
            """);

        private static CreateMessageRequestParams CreateRequestSamplingParams(string context, string uri, int maxTokens = 100)
        {
            return new CreateMessageRequestParams()
            {
                Messages = [new SamplingMessage()
                {
                    Role = Role.User,
                    Content = new Content()
                    {
                        Type = "text",
                        Text = $"Resource {uri} context: {context}"
                    }
                }],
                SystemPrompt = "You are a helpful test server.",
                MaxTokens = maxTokens,
                Temperature = 0.7f,
                IncludeContext = ContextInclusion.ThisServer
            };
        }

        public async Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null ||
                !request.Arguments.TryGetValue("prompt", out var prompt) ||
                !request.Arguments.TryGetValue("maxTokens", out var maxTokens))
            {
                throw new McpServerException("Missing required arguments 'prompt' and 'maxTokens'");
            }

            var sampleResult = await server!.RequestSamplingAsync(
                CreateRequestSamplingParams(
                    prompt?.ToString() ?? "",
                    "sampleLLM",
                    Convert.ToInt32(maxTokens?.ToString())),
                default);

            return new CallToolResponse()
            {
                Content = [new Content() { Text = $"LLM sampling result: {sampleResult.Content.Text}", Type = "text" }]
            };
        }
    }
}