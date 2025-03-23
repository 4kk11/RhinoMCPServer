using System;
using Rhino;

namespace RhinoMCPServer
{
    public class RhinoMCPServerPlugin : Rhino.PlugIns.PlugIn
    {
        public RhinoMCPServerPlugin()
        {
            Instance = this;
        }
        
        public static RhinoMCPServerPlugin Instance { get; private set; }
    }
}