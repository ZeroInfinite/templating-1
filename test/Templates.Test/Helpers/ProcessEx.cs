﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Templates.Test.Helpers
{
    internal class ProcessEx : IDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _stderrCapture;
        private readonly StringBuilder _stdoutCapture;
        private BlockingCollection<string> _stdoutLines;

        public static ProcessEx Run(string workingDirectory, string command, string args = null, IDictionary<string, string> envVars = null)
        {
            var startInfo = new ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            if (envVars != null)
            {
                foreach (var envVar in envVars)
                {
                    startInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
                }
            }

            var proc = Process.Start(startInfo);

            return new ProcessEx(proc);
        }

        public ProcessEx(Process proc)
        {
            _stdoutCapture = new StringBuilder();
            _stderrCapture = new StringBuilder();
            _stdoutLines = new BlockingCollection<string>();

            _process = proc;
            proc.EnableRaisingEvents = true;
            proc.OutputDataReceived += OnOutputData;
            proc.ErrorDataReceived += OnErrorData;
            proc.Exited += OnProcessExited;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        public string Error => _stderrCapture.ToString();

        public string Output => _stdoutCapture.ToString();

        public int ExitCode => _process.ExitCode;

        private void OnErrorData(object sender, DataReceivedEventArgs e)
        {
            _stderrCapture.AppendLine(e.Data);
            Console.Error.WriteLine(e.Data);
        }

        private void OnOutputData(object sender, DataReceivedEventArgs e)
        {
            _stdoutCapture.AppendLine(e.Data);
            Console.WriteLine(e.Data);

            if (_stdoutLines != null)
            {
                _stdoutLines.Add(e.Data);
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            _stdoutLines.CompleteAdding();
            _stdoutLines = null;
        }

        public void WaitForExit(bool assertSuccess)
        {
            _process.WaitForExit();

            if (assertSuccess && _process.ExitCode != 0)
            {
                throw new Exception($"Process exited with code {_process.ExitCode}\nStdErr: {Error}\nStdOut: {Output}");
            }
        }

        public void Dispose()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit();
            }
        }

        public IEnumerable<string> OutputLinesAsEnumerable => _stdoutLines.GetConsumingEnumerable();
    }
}
