using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoMCPServer.Commands
{
    public class RhinoMCPServerCommand : Command
    {
        public RhinoMCPServerCommand()
        {
            Instance = this;
        }

        public static RhinoMCPServerCommand? Instance { get; private set; }

        public override string EnglishName => "RhinoMCPServerCommand";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("The {0} command is under construction.", EnglishName);
            return Result.Success;
        }
    }
}
