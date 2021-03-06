﻿using System;
using System.Collections.Generic;
using System.Text;
using FlubuCore.TaskGenerator.Models;
using Microsoft.CodeAnalysis;
using Scripty.Core;

namespace FlubuCore.TaskGenerator
{
    public class TaskGenerator : TaskGeneratorBase
    {
        private readonly ScriptContext _context;

        public TaskGenerator(ScriptContext context)
        {
            _context = context;
        }

        public void GenerateTasks(List<Task> tasks)
        {
            foreach (var task in tasks)
            {
                GenerateTask(task);
            }
        }

        public virtual void GenerateTask(Task task)
        {
            _context.Output[task.FileName]
                .WriteLine($@"
//-----------------------------------------------------------------------
// <auto-generated />
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using FlubuCore.Context;
using FlubuCore.Tasks;
using FlubuCore.Tasks.Process;

namespace {task.Namespace}
{{
     public partial class {task.TaskName} : ExternalProcessTaskBase<{task.TaskResult}, {task.TaskName}>
     {{
        {WriteClassFieldsFromConstructorParameters(task.Constructor)}
        {WriteSummary(task.Constructor?.Summary)}
        public {task.TaskName}({WriteConstructorParameters(task)})
        {{
            ExecutablePath = ""{task.ExecutablePath}"";
            {WriteConstructorArguments(task)}
        }}

        protected override string Description {{ get; set; }}
        {WriteMethods(task)}
        {WriteDoExecuteMethod(task)}

     }}
}}");
        }

        protected internal virtual string WriteConstructorArguments(Task task)
        {
            string arguments = string.Empty;

            if (task.Constructor?.Arguments == null || task.Constructor.Arguments.Count == 0)
            {
                return arguments;
            }

            foreach (var argument in task.Constructor.Arguments)
            {
                arguments = $"{arguments}{WriteConstructorArgument(argument)}{Environment.NewLine}";
            }

            if (string.IsNullOrEmpty(arguments))
            {
                arguments = arguments.Remove(arguments.Length - Environment.NewLine.Length, Environment.NewLine.Length);
            }

            return arguments;
        }

        protected internal virtual string WriteConstructorParameters(Task task)
        {
            string parameters = string.Empty;

            if (task.Constructor?.Arguments == null)
            {
                return parameters;
            }

            foreach (var argument in task.Constructor.Arguments)
            {
                if (argument.Parameter != null)
                {
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        parameters = $"{parameters}, ";
                    }

                    parameters = $"{parameters} {WriteParameter(argument.Parameter)}";
                }
            }

            parameters = parameters.Trim();
            return parameters;
        }

        protected internal virtual string WriteMethods(Task task)
        {
            string methods = string.Empty;

            if (task.Methods == null || task.Methods.Count == 0)
            {
                return methods;
            }

            foreach (var method in task.Methods)
            {
                methods = $@"{methods}{WriteSummary(method.MethodSummary)}
        public {task.TaskName} {method.MethodName}({WriteParameter(method.Argument?.Parameter)})
        {{
            {WriteArgument(method.Argument)}
            return this;
        }}" + Environment.NewLine;
            }

            methods = methods.Remove(methods.Length - Environment.NewLine.Length, Environment.NewLine.Length);

            return methods;
        }

        protected internal virtual string WriteConstructorArgument(ConstructorArgument argument)
        {
            if (argument == null)
            {
                return string.Empty;
            }

            if (!argument.AfterOptions)
            {
                return WriteArgument(argument);
            }

            return $"_{argument.Parameter.ParameterName} = {argument.Parameter.ParameterName};";
        }

        protected internal virtual string WriteArgument(Argument argument)
        {
            if (argument == null)
            {
                return string.Empty;
            }

            if (argument.HasArgumentValue)
            {
                string parameterName = ParameterName(argument.Parameter.ParameterName);

                return $"WithArgumentsValueRequired(\"{argument.ArgumentKey}\", {parameterName}.ToString());";
            }
            else
            {
                return $"WithArguments(\"{argument.ArgumentKey}\");";
            }
        }

        protected internal virtual string WriteClassFieldsFromConstructorParameters(Constructor constructor)
        {
            if (constructor?.Arguments?.Count == 0)
            {
                return string.Empty;
            }

            string fields = string.Empty;

            foreach (var argument in constructor.Arguments)
            {
                var parameter = argument.Parameter;
                if (parameter == null)
                {
                    continue;
                }


                if (argument.AfterOptions)
                {
                    string parameterType =
                        parameter.AsParams ? $"{parameter.ParameterType}[]" : parameter.ParameterType;
                    fields = $"{fields}private {parameterType} _{parameter.ParameterName};{Environment.NewLine}";
                }
            }

            return fields;
        }

        protected internal virtual string WriteSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary))
            {
                return null;
            }

            return $@"
        /// <summary>
        /// {summary}
        /// </summary>";
        }

        protected internal virtual string WriteDoExecuteMethod(Task task)
        {
            var constructor = task.Constructor;
            if (constructor?.Arguments == null || constructor.Arguments.Count == 0)
            {
                return string.Empty;
            }

            string withArguments = string.Empty;
            foreach (var constructorArgument in constructor.Arguments)
            {
                if (constructorArgument.Parameter == null || !constructorArgument.AfterOptions)
                {
                    continue;
                }

                withArguments = $"{withArguments} {WriteDoExecuteWithArguments(constructorArgument)}";
            }

            return $@"protected override {task.TaskResult} DoExecute(ITaskContextInternal context)
        {{
            {withArguments}
            return base.DoExecute(context);
        }}";
        }

        protected internal virtual string WriteDoExecuteWithArguments(ConstructorArgument argument)
        {
            return $"WithArguments(_{argument.Parameter.ParameterName});{Environment.NewLine}";
        }
    }
}
