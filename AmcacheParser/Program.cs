﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Amcache.Classes;
using CsvHelper;
using CsvHelper.Configuration;
using Exceptionless;
using Fclp;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace AmcacheParser
{
    internal class Program
    {
        private static readonly string _preciseTimeFormat = "yyyy-MM-dd HH:mm:ss.fffffff";


        private static Logger _logger;
        private static Stopwatch _sw;
        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        private static bool CheckForDotnet46()
        {
            using (
                var ndpKey =
                    RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                        .OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\\"))
            {
                if (ndpKey == null)
                {
                    return false;
                }

                var releaseKey = Convert.ToInt32(ndpKey.GetValue("Release"));

                return releaseKey >= 393295;
            }
        }

        private static void Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("prIG996gFK1y6DaZEoXh3InSg8LwrHcQV4Dze2r8");
            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();

            if (!CheckForDotnet46())
            {
                _logger.Warn(".net 4.6 not detected. Please install .net 4.6 and try again.");
                return;
            }


            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.File)
                .As('f')
                .WithDescription("Amcache.hve file to parse. Required").Required();

            _fluentCommandLineParser.Setup(arg => arg.IncludeLinked)
                .As('i').SetDefault(false)
                .WithDescription("Include file entries for Programs entries");

            _fluentCommandLineParser.Setup(arg => arg.Whitelist)
                .As('w')
                .WithDescription(
                    "Path to file containing SHA-1 hashes to *exclude* from the results. Blacklisting overrides whitelisting\r\n");

            _fluentCommandLineParser.Setup(arg => arg.Blacklist)
                .As('b')
                .WithDescription(
                    "Path to file containing SHA-1 hashes to *include* from the results. Blacklisting overrides whitelisting");

            _fluentCommandLineParser.Setup(arg => arg.SaveTo)
                .As("csv").Required()
                .WithDescription("Directory where results will be saved. Required");


            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying timestamps. See https://goo.gl/CNVq0k for options. Default is: yyyy-MM-dd HH:mm:ss")
                .SetDefault("yyyy-MM-dd HH:mm:ss");

            _fluentCommandLineParser.Setup(arg => arg.PreciseTimestamps)
                .As("mp")
                .WithDescription(
                    "When true, display higher precision for timestamps. Default is false").SetDefault(false);

            var header =
                $"AmcacheParser version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/AmcacheParser";

            var footer = @"Examples: AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" --csv C:\temp" + "\r\n\t " +
                         @" AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" -i on --csv C:\temp" + "\r\n\t " +
                         @" AmcacheParser.exe -f ""C:\Temp\amcache\AmcacheWin10.hve"" -w ""c:\temp\whitelist.txt"" --csv C:\temp" +
                         "\r\n\t" +
                         "\r\n\t" +
                         "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("Both -f and --csv are required. Exiting");

                return;
            }

            if (!File.Exists(_fluentCommandLineParser.Object.File))
            {
                _logger.Warn($"'{_fluentCommandLineParser.Object.File}' not found. Exiting");
                return;
            }

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", args)}");
            _logger.Info("");

            if (_fluentCommandLineParser.Object.PreciseTimestamps)
            {
                _fluentCommandLineParser.Object.DateTimeFormat = _preciseTimeFormat;
            }


            _sw = new Stopwatch();
            _sw.Start();

            try
            {
                _sw.Start();

                var am = new Amcache.Amcache(_fluentCommandLineParser.Object.File,
                    _fluentCommandLineParser.Object.RecoverDeleted);

                if (am.ProgramsEntries.Count == 0 && am.UnassociatedFileEntries.Count == 0)
                {
                    _logger.Warn("Hive did not contain program entries nor file entries. Exiting");
                    return;
                }

                _sw.Stop();

                var whitelistHashes = new HashSet<string>();

                var useBlacklist = false;

                if (_fluentCommandLineParser.Object.Blacklist.Length > 0)
                {
                    if (File.Exists(_fluentCommandLineParser.Object.Blacklist))
                    {
                        foreach (var readLine in File.ReadLines(_fluentCommandLineParser.Object.Blacklist))
                        {
                            whitelistHashes.Add(readLine.ToLowerInvariant());
                        }
                        useBlacklist = true;
                    }
                    else
                    {
                        _logger.Warn($"'{_fluentCommandLineParser.Object.Blacklist}' does not exist");
                    }
                }
                else if (_fluentCommandLineParser.Object.Whitelist.Length > 0)
                {
                    if (File.Exists(_fluentCommandLineParser.Object.Whitelist))
                    {
                        foreach (var readLine in File.ReadLines(_fluentCommandLineParser.Object.Whitelist))
                        {
                            whitelistHashes.Add(readLine.ToLowerInvariant());
                        }
                    }
                    else
                    {
                        _logger.Warn($"'{_fluentCommandLineParser.Object.Whitelist}' does not exist");
                    }
                }

                var cleanList =
                    am.UnassociatedFileEntries.Where(t => whitelistHashes.Contains(t.SHA1) == useBlacklist).ToList();
                var totalProgramFileEntries = 0;

                if (Directory.Exists(_fluentCommandLineParser.Object.SaveTo) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(_fluentCommandLineParser.Object.SaveTo);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            $"There was an error creating directory '{_fluentCommandLineParser.Object.SaveTo}'. Error: {ex.Message} Exiting");
                        return;
                    }
                }

                foreach (var pe in am.ProgramsEntries)
                {
                    var cleanList2 =
                        pe.FileEntries.Where(t => whitelistHashes.Contains(t.SHA1) == useBlacklist).ToList();
                    totalProgramFileEntries += cleanList2.Count;
                }

                var ts = DateTime.Now.ToString("yyyyMMddHHmmss");
                var hiveName = Path.GetFileNameWithoutExtension(_fluentCommandLineParser.Object.File);

                var outbase = $"{ts}_{hiveName}_Unassociated file entries.tsv";
                var outFile = Path.Combine(_fluentCommandLineParser.Object.SaveTo, outbase);

                using (var sw = new StreamWriter(outFile))
                {
                    sw.AutoFlush = true;

                    var csv = new CsvWriter(sw);
                    csv.Configuration.RegisterClassMap(
                        new FECacheOutputMap(_fluentCommandLineParser.Object.DateTimeFormat));
                    csv.Configuration.Delimiter = "\t";

                    csv.WriteHeader<FileEntry>();
                    csv.WriteRecords(cleanList);
                }

                if (_fluentCommandLineParser.Object.IncludeLinked)
                {
                    outbase = $"{ts}_{hiveName}_Program entries.tsv";
                    outFile = Path.Combine(_fluentCommandLineParser.Object.SaveTo, outbase);

                    using (var sw = new StreamWriter(outFile))
                    {
                        sw.AutoFlush = true;

                        var csv = new CsvWriter(sw);
                        csv.Configuration.RegisterClassMap(
                            new PECacheOutputMap(_fluentCommandLineParser.Object.DateTimeFormat));
                        csv.Configuration.Delimiter = "\t";

                        csv.WriteHeader<ProgramsEntry>();
                        csv.WriteRecords(am.ProgramsEntries);
                    }

                    outbase = $"{ts}_{hiveName}_Associated file entries.tsv";
                    outFile = Path.Combine(_fluentCommandLineParser.Object.SaveTo, outbase);

                    using (var sw = new StreamWriter(outFile))
                    {
                        var csv = new CsvWriter(sw);
                        csv.Configuration.RegisterClassMap(
                            new FECacheOutputMap(_fluentCommandLineParser.Object.DateTimeFormat));
                        csv.Configuration.Delimiter = "\t";

                        csv.WriteHeader<FileEntry>();

                        sw.AutoFlush = true;

                        foreach (var pe in am.ProgramsEntries)
                        {
                            var cleanList2 =
                                pe.FileEntries.Where(t => whitelistHashes.Contains(t.SHA1) == useBlacklist).ToList();

                            csv.WriteRecords(cleanList2);
                        }
                    }
                }

                var suffix = am.UnassociatedFileEntries.Count == 1 ? "y" : "ies";


                var linked = "";
                if (_fluentCommandLineParser.Object.IncludeLinked)
                {
                    linked =
                        $"and {totalProgramFileEntries:N0} program file entries (across {am.ProgramsEntries.Count:N0} program entries) ";
                }


                _logger.Info("");

                _logger.Info($"Total file entries found: {am.TotalFileEntries:N0}.");

                _logger.Info(
                    $"Found {cleanList.Count:N0} unassociated file entr{suffix} {linked}");

                if (whitelistHashes.Count > 0)
                {
                    var per = (double) (totalProgramFileEntries + cleanList.Count)/am.TotalFileEntries;

                    _logger.Info("");

                    var list = "whitelist";
                    if (_fluentCommandLineParser.Object.Blacklist.Length > 0)
                    {
                        list = "blacklist";
                    }

                    _logger.Info($"{UppercaseFirst(list)} hash count: {whitelistHashes.Count:N0}");

                    _logger.Info("");

                    _logger.Info($"Percentage of total shown based on {list}: {per:P3} ({1 - per:P3} savings)");
                }
                _logger.Info("");

                _logger.Info($"Results saved to: {_fluentCommandLineParser.Object.SaveTo}");


                _logger.Info("");
                _logger.Info(
                    $"Total search time: {_sw.Elapsed.TotalSeconds:N3} seconds.");
            }
            catch (Exception ex)
            {
                _logger.Error($"There was an error: {ex.Message}");
                _logger.Error($"Stacktrace: {ex.StackTrace}");
                _logger.Info("");
                _logger.Error(
                    $"Please send '{_fluentCommandLineParser.Object.File}' to saericzimmerman@gmail.com in order to fix the issue");
            }
        }

        private static void SetupNLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }

        private static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            var a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
    }


    internal class ApplicationArguments
    {
        public string File { get; set; }
        //       public string Extension { get; set; } = string.Empty;
        public string Whitelist { get; set; } = string.Empty;
        public string Blacklist { get; set; } = string.Empty;
        public string SaveTo { get; set; } = string.Empty;
        public bool IncludeLinked { get; set; } = false;
        public bool RecoverDeleted { get; set; } = false;
        public string DateTimeFormat { get; set; }
        public bool PreciseTimestamps { get; set; }
    }

    public sealed class FECacheOutputMap : CsvClassMap<FileEntry>
    {
        public FECacheOutputMap(string dateformat)
        {
            Map(m => m.ProgramName);
            Map(m => m.ProgramID);
            Map(m => m.VolumeID);
            Map(m => m.VolumeIDLastWriteTimestamp).TypeConverterOption(dateformat);
            Map(m => m.FileID);
            Map(m => m.FileIDLastWriteTimestamp).TypeConverterOption(dateformat);
            Map(m => m.SHA1);
            Map(m => m.FullPath);
            Map(m => m.FileExtension);
            Map(m => m.MFTEntryNumber);
            Map(m => m.MFTSequenceNumber);
            Map(m => m.FileSize);
            Map(m => m.FileVersionString);
            Map(m => m.FileVersionNumber);
            Map(m => m.FileDescription);

            Map(m => m.PEHeaderSize);
            Map(m => m.PEHeaderHash);
            Map(m => m.PEHeaderChecksum);

            Map(m => m.Created).TypeConverterOption(dateformat);
            Map(m => m.LastModified).TypeConverterOption(dateformat);
            Map(m => m.LastModified2).TypeConverterOption(dateformat);
            Map(m => m.CompileTime).TypeConverterOption(dateformat);
            Map(m => m.LanguageID);
        }
    }

    public sealed class PECacheOutputMap : CsvClassMap<ProgramsEntry>
    {
        public PECacheOutputMap(string dateformat)
        {
            Map(m => m.ProgramID);
            Map(m => m.LastWriteTimestamp).TypeConverterOption(dateformat);
            Map(m => m.ProgramName_0);
            Map(m => m.ProgramVersion_1);
            Map(m => m.VendorName_2);

            Map(m => m.InstallDateEpoch_a).TypeConverterOption(dateformat);
            Map(m => m.InstallDateEpoch_b).TypeConverterOption(dateformat);

            Map(m => m.LanguageCode_3);
            Map(m => m.InstallSource_6);
            Map(m => m.UninstallRegistryKey_7);
            Map(m => m.PathsList_d);
        }
    }
}