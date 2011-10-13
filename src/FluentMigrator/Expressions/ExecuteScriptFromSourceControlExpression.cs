
using System;
using System.IO;
using FluentMigrator.Builders.Execute;
using FluentMigrator.Infrastructure;
using SharpSvn;

namespace FluentMigrator.Expressions
{
    /// <summary>
    /// Extends execution a script to obtain scripts to execute from source control
    /// </summary>
    /// <remarks>
    /// <param>This version by default only supports SVN as a source control client.</param>
    /// <param>It could be extended to have a provider model and implement different <see cref="ISourceControlClient"/></param>
    /// </remarks>
    public class ExecuteScriptFromSourceControlExpression : IExecuteFromSourceControlExpression
    {
        private readonly ExecuteSqlScriptExpression _expression;
        private ISourceControlClient _client;
        private Func<string> _getWorkingDirectory;

        /// <summary>
        /// The source control client to be used to retreive scripts
        /// </summary>
        public ISourceControlClient SourceControlClient
        {
            get { return _client ?? new SvnClient(); }
            set { _client = value; }
        }

        /// <summary>
        /// The working directory to place scripts within
        /// </summary>
        public Func<string> GetWorkingDirectory
        {
            get { return _getWorkingDirectory ?? (_getWorkingDirectory = DefaultMigrationConventions.GetWorkingDirectory); }
            set { _getWorkingDirectory = value; }
        }

        /// <summary>
        /// Constructs a new instance of a <see cref="ExecuteScriptFromSourceControlExpression"/>
        /// </summary>
        /// <param name="expression">The script to be executed</param>
        public ExecuteScriptFromSourceControlExpression(ExecuteSqlScriptExpression expression)
        {
            _expression = expression;
        }

        /// <summary>
        /// Downloads the script from source control and updates the <see cref="ExecuteSqlScriptExpression.SqlScript"/> to the downloaded revision
        /// </summary>
        /// <param name="revision">The revision to be downloaded</param>
        public void FromSourceControl<T>(T revision)
        {
            var revisionScript = Path.Combine(Path.GetDirectoryName(_expression.SqlScript) ?? "",
                                              Path.GetFileNameWithoutExtension(_expression.SqlScript) + "_" + revision +
                                       Path.GetExtension(_expression.SqlScript));

            var fullRevisionScript = Path.Combine(GetWorkingDirectory(), revisionScript);

            if (SourceControlClient.Settings != null)
            {
                var script = _expression.SqlScript ?? "";
                if (!string.IsNullOrEmpty(script) && script.StartsWith(@"\"))
                    script = script.Substring(1);

                var scriptPath = Path.GetDirectoryName(Path.Combine(GetWorkingDirectory(), script));
                if (scriptPath != null && !Directory.Exists(scriptPath))
                    Directory.CreateDirectory(scriptPath);

                SourceControlClient.GetFile(ConvertScriptPath(_expression.SqlScript), revision, fullRevisionScript);
            }

            // Execute revision script instead (If no sourceControlSettings assume that the file already exists)
            _expression.SqlScript = revisionScript;
        }

        /// <summary>
        /// Convert from local file path syntax to uri syntax e.g. \scripts\test.sql to /scripts/test.sql
        /// </summary>
        /// <param name="path">The path to be converted</param>
        /// <returns>The converted path</returns>
        private string ConvertScriptPath(string path)
        {
            return path.Replace("\\", "/");
        }
    }

    /// <summary>
    /// Implementation independant version of source control client
    /// </summary>
    public interface ISourceControlClient
    {
        /// <summary>
        /// Settings required to get information from the source control system
        /// </summary>
        SourceControlSettings Settings { get; set; }

        /// <summary>
        /// Get a specified file from Subversion
        /// </summary>
        /// <param name="sourceUri">The relative path to the file to be retrieved</param>
        /// <param name="revision">The revision to return</param>
        /// <param name="destination">The location to save the file</param>
        void GetFile<T>(string sourceUri, T revision, string destination);
    }

    /// <summary>
    /// Default implementation of SVN client using SvnSharp
    /// </summary>
    public class SvnClient : ISourceControlClient
    {
        private SourceControlSettings _settings;
        private Func<string> _getWorkingDirectory;

        /// <summary>
        /// The working directory to read source control settings from
        /// </summary>
        public Func<string> GetWorkingDirectory
        {
            get { return _getWorkingDirectory ?? (_getWorkingDirectory = DefaultMigrationConventions.GetWorkingDirectory); }
            set { _getWorkingDirectory = value; }
        }

        /// <summary>
        /// The settings to interact with the source control system
        /// </summary>
        public SourceControlSettings Settings
        {
            get { return _settings ?? LoadSourceControlSettings(); }
            set { _settings = value; }
        } 

        /// <summary>
        /// Get a specified file from Subversion
        /// </summary>
        /// <param name="sourceUri">The relative path to the file to be retrieved</param>
        /// <param name="revision">The revision to return</param>
        /// <param name="destination">The location to save the file</param>
        public void GetFile<T>(string sourceUri, T revision, string destination)
        {
            using (var client = new SharpSvn.SvnClient())
            {
                client.Export(new SvnUriTarget(Settings.BaseUri + sourceUri, new SvnRevision(int.Parse(revision.ToString()))), destination);
            }  
        }

        /// <summary>
        /// Read source control settings
        /// </summary>
        /// <remarks>
        /// Very simple source control settings load import assumes the file sourcecontrol.txt is the working directory
        /// </remarks>
        /// <returns>The settings to load the script files</returns>
        private SourceControlSettings LoadSourceControlSettings()
        {
            var configFile = Path.Combine(GetWorkingDirectory(), "sourcecontrol.txt");
            if (File.Exists(configFile))
            {
                var lines = File.ReadAllLines(configFile);
                // Assume that the first line of the file is the subversion URL
                return new SourceControlSettings { BaseUri = lines[0] };
            }
            return null;
        }
    }

    /// <summary>
    /// Settings required to get information from the source control system
    /// </summary>
    public class SourceControlSettings
    {
        /// <summary>
        /// The location withing the source control systems that file references are relative to
        /// </summary>
        public string BaseUri { get; set; }
    }
}