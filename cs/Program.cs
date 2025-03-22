﻿using System;
using MCPSharp;
using MCPSharp.Model;
using MCPSharp.Model.Schemas;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace MCP_RhinoServer
{
    class Program
    {
        
        [System.STAThread]
        static async Task Main(string[] args)
        {
            MCPServer.Register<ExternalTool>(); 
            MCPServer.Register<SemKerExample>();

            MCPServer.AddToolHandler( new Tool() 
            {
                Name = "dynamicTool",
                Description = "A Test Tool",
                InputSchema = new InputSchema {
                    Type = "object",
                    Required = new List<string> { "input" },
                    Properties = new Dictionary<string, ParameterSchema>{
                        {"input", new ParameterSchema{Type="string", Description="the input"}},
                        {"input2", new ParameterSchema{Type="string", Description="the input2"}}
                    }
                }
            }, (string input, string? input2) => { return $"hello, {input}.\n{input2 ?? "didn't feel like filling in the second value just because it wasn't required? shame. just kidding! thanks for your help!"}"; });

            await MCPServer.StartAsync("TestServer", "1.0");
        }

    }

    public class MCPDev
    {
        [McpResource("name", "test://{name}")]
        public string Name(string name) => $"hello {name}";


        [McpResource("settings", "test://settings", "string", "the settings document")]
        public string Settings { get; set; } = "settings";


        [McpTool("write-to-console", "write a string to the console")] 
        public static void WriteToConsole(string message) => Console.WriteLine(message);

        ///<summary>just returns a message for testing.</summary>
        [McpTool] 
        public static string Hello() => "hello, claude.";

        ///<summary>returns ths input string back</summary>
        ///<param name="input">the string to echo</param>
        [McpTool]
        public static string Echo([McpParameter(true)] string input) => input;

        ///<summary>Add Two Numbers</summary>
        ///<param name="a">first number</param>
        ///<param name="b">second number</param>
        [McpTool] 
        public static string Add(int a, int b) => (a + b).ToString();


        /// <summary>
        /// Adds a complex object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [McpTool]
        public static string AddComplex(ComplicatedObject obj) => $"Name: {obj.Name}, Age: {obj.Age}, Hobbies: {string.Join(", ", obj.Hobbies)}";

        /// <summary>
        /// throws an exception - for ensuring we handle them gracefully
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [McpFunction("throw_exception")] //leaving this one as [McpFunction] for testing purposes
        public static string Exception() => throw new Exception("This is an exception");
    }

    /// <summary>
    /// A complicated object
    /// </summary>
    public class ComplicatedObject
    {
        /// <summary>The name of the object</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>The age of the object</summary>
        [JsonPropertyName("age")]
        public int Age { get; set; } = 0;

        /// <summary>The hobbies of the object</summary>
        [JsonPropertyName("hobbies")]
        public string[] Hobbies { get; set; } = new string[0];
    }

        [McpTool("external_tools", "for testing accessing tool classes loaded from a library")]
    public class ExternalTool
    {

        [McpFunction("dll-tool", "attempts to use a tool that is loaded from an external assembly dll. should return 'success'")]
        public static async Task<string> UseAsync() 
        {
            return await Task.Run(()=>"success");
        }

    }

    public class SemKerExample
    {
        [KernelFunction("SemanticTest")]
        [Description("test semantic kernel integration")]
        public static async Task<string> UseAsync()
        {
            return await Task.Run(() => "success");
        }
    }
}
