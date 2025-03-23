using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoMCPServer
{
    public class HelthCheckCommand : Command
    {
        public HelthCheckCommand()
        {
            Instance = this;
        }

        public static HelthCheckCommand Instance { get; private set; }

        public override string EnglishName => "HelthCheckCommand";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("Helth Check !", EnglishName);
            RhinoApp.WriteLine($".NET Version: {Environment.Version}");
            return Result.Success;
        }
    }
}
