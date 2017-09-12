﻿using System.Collections.Generic;
using System.IO;
using FlubuCore.Context;
using FlubuCore.Tasks.Process;

namespace FlubuCore.Tasks.MsSql
{
    /// <inheritdoc />
    /// <summary>
    /// Execute SQL script file with sqlcmd.exe
    /// </summary>
    public class SqlCmdTask : TaskBase<int>
    {
        private const string DefaultSqlCmdExe =
            @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\130\Tools\Binn\SQLCMD.EXE";

        private readonly List<string> _arguments = new List<string>();
        private string _workingFolder;
        private readonly List<string> _sqlCmdExePaths = new List<string>();
        private string _output;
        private string _errorOutput;
        private readonly List<string> _sqlFiles = new List<string>();
        private bool _doNotLog;

        /// <inheritdoc />
        public SqlCmdTask(string sqlFileName)
        {
            _sqlFiles.Add(sqlFileName);
            _sqlCmdExePaths.Add(DefaultSqlCmdExe);
        }


        /// <inheritdoc />
        public SqlCmdTask(params string[] sqlFiles)
        {
            _sqlFiles.AddRange(sqlFiles);
            _sqlCmdExePaths.Add(DefaultSqlCmdExe);
        }

        /// <summary>
        /// Add's Argument to the dotnet see <c>Command</c>
        /// </summary>
        /// <param name="arg">Argument to be added</param>
        /// <returns></returns>
        /// <remarks>Do not escape args with ". You should add separate argument for option and value. .WithArguments("-i", "mysqlfile.sql")</remarks>    
        public SqlCmdTask WithArguments(string arg)
        {
            _arguments.Add(arg);
            return this;
        }

        /// <summary>
        /// Add arguments to the sqlcmd executable. See <c>Command</c>
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <remarks>Do not escape args with ". You should add separate argument for option and value. .WithArguments("-i", "mysqlfile.sql")</remarks>    
        public SqlCmdTask WithArguments(params string[] args)
        {
            _arguments.AddRange(args);
            return this;
        }

        /// <summary>
        /// Working folder of the sqlcmd command.
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public SqlCmdTask WorkingFolder(string folder)
        {
            _workingFolder = folder;
            return this;
        }

        /// <summary>
        /// Add another full path to the sqlcmd executable. First one that is found will be used.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public SqlCmdTask SqlCmdExecutable(string fullPath)
        {
            _sqlCmdExePaths.Add(fullPath);
            return this;
        }

        /// <summary>
        /// Return output of the sqlcmd command.
        /// </summary>
        /// <returns></returns>
        public string GetOutput()
        {
            return _output;
        }

        /// <summary>
        /// Return output of the sqlcmd command.
        /// </summary>
        /// <returns></returns>
        public string GetErrorOutput()
        {
            return _errorOutput;
        }

        /// <summary>
        /// Connect to the specified SQL server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public SqlCmdTask UseServer(string server, string userName, string password)
        {
            Server(server)
                .UserName(userName)
                .Password(password);

            return this;
        }

        /// <summary>
        /// Connect to the specified SQL server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="database"></param>
        /// <returns></returns>
        public SqlCmdTask UseServer(string server, string userName, string password, string database)
        {
            UseServer(server, userName, password)
                .Database(database);

            return this;
        }

        /// <summary>
        /// Connect to server.
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public SqlCmdTask Server(string server)
        {
            _arguments.Add("-S");
            _arguments.Add(server);
            return this;
        }

        /// <summary>
        /// Use userName when connecting to the DB.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public SqlCmdTask UserName(string userName)
        {
            _arguments.Add("-U");
            _arguments.Add(userName);
            return this;
        }

        /// <summary>
        /// Use password when connecting to the DB.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public SqlCmdTask Password(string password)
        {
            _arguments.Add("-P");
            _arguments.Add(password);
            return this;
        }

        /// <summary>
        /// Use trusted connection when connecting to the DB.
        /// </summary>
        /// <returns></returns>
        public SqlCmdTask TrustedConnection()
        {
            _arguments.Add("-E");
            return this;
        }

        /// <summary>
        /// Use database name.
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public SqlCmdTask Database(string database)
        {
            _arguments.Add("-d");
            _arguments.Add(database);
            return this;
        }

        /// <summary>
        /// Force that file is in UTF8 encoding. Skip auto detection.
        /// </summary>
        /// <returns></returns>
        public SqlCmdTask ForceUtf8()
        {
            _arguments.Add("-f");
            _arguments.Add("65001");
            return this;
        }

        /// <summary>
        /// Do not log to the output.
        /// </summary>
        /// <returns></returns>
        public SqlCmdTask DoNoLogOutput()
        {
            _doNotLog = true;
            return this;
        }

        /// <inheritdoc />
        protected override int DoExecute(ITaskContextInternal context)
        {
            string program = context.Properties.GetSqlCmdExecutable();

            if (!string.IsNullOrEmpty(program))
                _sqlCmdExePaths.Add(program);

            program = FindExecutable();

            if (string.IsNullOrEmpty(program))
            {
                context.Fail("SqlCmd executable not found!", -1);
                return -1;
            }

            if (_sqlFiles.Count <= 0)
            {
                context.Fail("At least one file must be specified.", -1);
                return -1;
            }

            foreach (string file in _sqlFiles)
            {
                IRunProgramTask task = context.Tasks().RunProgramTask(program);

                if (_doNotLog)
                    task.DoNotLogOutput();

                task
                    .WithArguments("-i")
                    .WithArguments(file)
                    .WithArguments(_arguments.ToArray())
                    .CaptureErrorOutput()
                    .CaptureOutput()
                    .WorkingFolder(_workingFolder)
                    .ExecuteVoid(context);

                _output = task.GetOutput();
                _errorOutput = task.GetErrorOutput();
            }


            return 0;
        }

        private string FindExecutable()
        {
            foreach (string exePath in _sqlCmdExePaths)
            {
                if (File.Exists(exePath))
                    return exePath;
            }

            return null;
        }
    }
}