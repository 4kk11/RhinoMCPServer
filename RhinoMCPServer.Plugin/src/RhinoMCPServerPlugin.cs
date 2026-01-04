using System;

namespace RhinoMCPServer.Plugin
{
    public class RhinoMCPServerPlugin : Rhino.PlugIns.PlugIn
    {
        public RhinoMCPServerPlugin()
        {
            Instance = this;
            Console.WriteLine("RhinoMCPServerPlugin");
        }

        public static RhinoMCPServerPlugin? Instance { get; private set; }
    }
}
