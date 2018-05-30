﻿using System;
using System.Collections.Generic;
using System.Text;
using FlubuCore.Context;
using FlubuCore.Context.FluentInterface.Interfaces;
using FlubuCore.Scripting;
using FlubuCore.Targeting;

namespace Flubu.Tests.TestData.BuildScripts
{
    public class BuildScriptParameterCountNotMatch : DefaultBuildScript
    {
        protected override void ConfigureBuildProperties(IBuildPropertiesContext context)
        {
        }

        protected override void ConfigureTargets(ITaskContext session)
        {
        }

        [Target("Test", "value", 1)]
        public void TestTarget(ITargetFluentInterface target, string param)
        {
        }
    }
}