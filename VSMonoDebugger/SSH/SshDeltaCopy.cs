/* 
20171022: Version 1.4.0

MIT License

Copyright (c) 2017 GordianDotNet (https://github.com/GordianDotNet/SshFileSync)
Copyright (c) 2012-2016, RENCI (https://www.nuget.org/packages/SSH.NET and https://github.com/sshnet/SSH.NET)
Copyright (c) 2014 Adam Hathcock (https://www.nuget.org/packages/sharpcompress/ and https://github.com/adamhathcock/sharpcompress)


Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace SshFileSync
{
    /// <summary>
    /// TODO remove/create empty folders
    /// </summary>
    public class SshDeltaCopy : IDisposable
    {
        public class Options
        {
            public const int HOST_INDEX = 0;
            public const int PORT_INDEX = 1;
            public const int USERNAME_INDEX = 2;
            public const int PASSWORD_INDEX = 3;
            public const int SOURCE_DIRECTORY_INDEX = 4;
            public const int DESTINATION_DIRECTORY_INDEX = 5;

            public const int DEFAULT_PORT = 22;

            public string Host = string.Empty;
            public int Port = DEFAULT_PORT;
            public string Username = string.Empty;
            public string Password = string.Empty;
            public string DestinationDirectory = string.Empty;
            public string SourceDirectory = string.Empty;
            public bool RemoveOldFiles = true;
            public bool PrintTimings = true;
            public bool RemoveTempDeleteListFile = true;

            public string ConnectionGroupKey
            {
                get
                {
                    return $"{Host}#{Port}#{Username}";
                }
            }
            
            public Options()
            { }

            public static Options CreateFromArgs(string[] args)
            {
                if (args == null)
                {
                    throw new NullReferenceException($"{nameof(CreateFromArgs)}.{nameof(CreateFromArgs)}: {nameof(args)} == null");
                }

                var maxArgsIndex = args.Length - 1;

                if (maxArgsIndex < PASSWORD_INDEX)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(CreateFromArgs)}.{nameof(CreateFromArgs)}: {nameof(args)}.Length < 4");
                }

                int port;
                if (!int.TryParse(args[PORT_INDEX], out port))
                {
                    port = DEFAULT_PORT;
                }

                var instance = new Options()
                {
                    Host = args[HOST_INDEX],
                    Port = port,
                    Username = args[USERNAME_INDEX],
                    Password = args[PASSWORD_INDEX]
                };

                if (maxArgsIndex >= DESTINATION_DIRECTORY_INDEX)
                {
                    instance.SourceDirectory = args[SOURCE_DIRECTORY_INDEX];
                    instance.DestinationDirectory = args[DESTINATION_DIRECTORY_INDEX];
                }

                return instance;
            }
        }

        private struct UpdateCacheEntry
        {
            public string FilePath;
            public long LastWriteTimeUtcTicks;
            public long FileSize;

            public static UpdateCacheEntry ReadFromStream(BinaryReader reader)
            {
                var fileEntry = new UpdateCacheEntry
                {
                    FilePath = reader.ReadString(),
                    LastWriteTimeUtcTicks = reader.ReadInt64(),
                    FileSize = reader.ReadInt64(),
                };

                return fileEntry;
            }

            public static void WriteToStream(BinaryWriter writer, string filePath, FileInfo fileInfo)
            {
                writer.Write(filePath);
                writer.Write(fileInfo.LastWriteTimeUtc.Ticks);
                writer.Write(fileInfo.Length);
            }
        }

        [Flags]
        private enum UpdateFlages : uint
        {
            NONE = 0,
            DELETE_FILES = 1,
            UPADTE_FILES = 2,
            DELETE_AND_UPDATE_FILES = DELETE_FILES | UPADTE_FILES,
        }

        private readonly Options _sshDeltaCopyOptions;

        private Renci.SshNet.SftpClient _sftpClient;
        private Renci.SshNet.ScpClient _scpClient;
        private Renci.SshNet.SshClient _sshClient;

        public static readonly string DefaultBatchFilename = "sshfilesync.csv";
        public static readonly string _deleteListFileName = ".deletedFilesList.cache";
        public static readonly string _uploadCacheFileName = ".uploadCache.cache";
        public static readonly string _uploadCacheTempFileName = ".uploadCache.cache.tmp";
        public static readonly string _compressedUploadDiffContentFilename = "compressedUploadDiffContent.tar.gz";

        private string _scpDestinationDirectory;

        private readonly Stopwatch _stopWatch = new Stopwatch();
        private long _lastElapsedMilliseconds;
        private bool _isConnected;

        public Action<string> LogOutput { get; set; } = Console.WriteLine;

        public static bool RunBatchfile(string batchFileName)
        {
            if (!File.Exists(batchFileName))
            {
                return false;
            }

            return RunBatchLines(File.ReadLines(batchFileName));
        }

        public static bool RunBatchLines(IEnumerable<string> batchLines, params char[] splitChars)
        {
            splitChars = splitChars.Length > 0 ? splitChars : new char[] { ';' };

            var batchRuns = batchLines
                .Select(x => x.Split(splitChars))
                .Where(args => args.Length > Options.DESTINATION_DIRECTORY_INDEX)
                .Select(args => Options.CreateFromArgs(args))
                .GroupBy(x => x.ConnectionGroupKey)
                .ToList();

            foreach (var batchGroup in batchRuns)
            {
                try
                {
                    var groupConnectionOptions = batchGroup.First();
                    using (var sshDeltaCopy = new SshDeltaCopy(groupConnectionOptions))
                    {
                        foreach (var batchElement in batchGroup)
                        {
                            sshDeltaCopy.DeployDirectory(batchElement.SourceDirectory, batchElement.DestinationDirectory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
                }
            }

            return true;
        }

        public SshDeltaCopy(string host, int port, string username, string password, bool removeOldFiles = true, bool printTimings = true, bool removeTempDeleteListFile = true)
        {
            _sshDeltaCopyOptions = new Options()
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                RemoveOldFiles = removeOldFiles,
                PrintTimings = printTimings,
                RemoveTempDeleteListFile = removeTempDeleteListFile
            };
        }

        public SshDeltaCopy(Options sshDeltaCopyOptions)
        {
            _sshDeltaCopyOptions = sshDeltaCopyOptions;
        }

        public void Dispose()
        {
            _sshClient?.Dispose();
            _sftpClient?.Dispose();
            _scpClient?.Dispose();
        }

        public SshCommand RunSSHCommand(string userCommandText, bool throwOnError = true)
        {
            InternalConnect(_sshDeltaCopyOptions.Host, _sshDeltaCopyOptions.Port, _sshDeltaCopyOptions.Username, _sshDeltaCopyOptions.Password, _sshDeltaCopyOptions.DestinationDirectory);

            var commandText = $"cd \"{_sshDeltaCopyOptions.DestinationDirectory}\";{userCommandText}";
            PrintTime($"Running SSH command ...\n{commandText}");

            SshCommand cmd = _sshClient.RunCommand(commandText);
            if (throwOnError && cmd.ExitStatus != 0)
            {
                throw new Exception(cmd.Error);
            }

            PrintTime($"SSH command result:\n{cmd.Result}");

            return cmd;
        }

        public SshCommand CreateSSHCommand(string userCommandText)
        {
            InternalConnect(_sshDeltaCopyOptions.Host, _sshDeltaCopyOptions.Port, _sshDeltaCopyOptions.Username, _sshDeltaCopyOptions.Password, _sshDeltaCopyOptions.DestinationDirectory);

            var commandText = $"cd \"{_sshDeltaCopyOptions.DestinationDirectory}\";{userCommandText}";

            return _sshClient.CreateCommand(commandText);
        }

        public void DeployDirectory(string sourceDirectory, string destinationDirectory)
        {
            InternalConnect(_sshDeltaCopyOptions.Host, _sshDeltaCopyOptions.Port, _sshDeltaCopyOptions.Username, _sshDeltaCopyOptions.Password, destinationDirectory);

            PrintTime($"Copy{(_sshDeltaCopyOptions.RemoveOldFiles ? " and remove" : string.Empty)} all changed files from '{sourceDirectory}' to '{destinationDirectory}'");

            var sourceDirInfo = new DirectoryInfo(sourceDirectory);
            if (!sourceDirInfo.Exists)
            {
                throw new DirectoryNotFoundException($"{sourceDirectory} not found!");
            }

            var localFileCache = CreateLocalFileCache(sourceDirInfo);

            var fileListToDelete = new StringBuilder();
            var filesNoUpdateNeeded = new ConcurrentDictionary<string, bool>();

            DownloadAndCalculateChangedFiles(localFileCache, fileListToDelete, filesNoUpdateNeeded);

            var updateFlags = CreateAndUploadFileDiff(localFileCache, filesNoUpdateNeeded, fileListToDelete.ToString());

            if (updateFlags != UpdateFlages.NONE)
            {
                UnzipCompressedFileDiffAndRemoveOldFiles(destinationDirectory, updateFlags);
            }

            PrintTime($"Finished!");
        }

        private void InternalConnect(string host, int port, string username, string password, string workingDirectory)
        {
            if (_isConnected)
            {
                ChangeWorkingDirectory(workingDirectory);
                return;
            }

            // Restart timer
            _stopWatch.Restart();
            _lastElapsedMilliseconds = 0;

            // Start connection ...
            PrintTime($"Connecting to {username}@{host}:{port} ...");

            _sshClient = new SshClient(host, port, username, password);
            _sshClient.Connect();

            try
            {
                // Use SFTP for file transfers
                var sftpClient = new SftpClient(host, port, username, password);
                sftpClient.Connect();
                _sftpClient = sftpClient;
            }
            catch (Exception ex)
            {
                // Use SCP if SFTP fails
                PrintTime($"Error: {ex.Message} Is SFTP supported for {username}@{host}:{port}? We are using SCP instead!");
                _scpClient = new ScpClient(host, port, username, password);
                _scpClient.Connect();
            }

            var _connectionInfo = _sshClient.ConnectionInfo;

            PrintTime($"Connected to {_connectionInfo.Username}@{_connectionInfo.Host}:{_connectionInfo.Port} via SSH and {(_sftpClient != null ? "SFTP" : "SCP")}");

            _isConnected = true;

            ChangeWorkingDirectory(workingDirectory);
        }

        private void ChangeWorkingDirectory(string destinationDirectory)
        {
            var cmd = _sshClient.RunCommand($"mkdir -p \"{destinationDirectory}\"");
            if (cmd.ExitStatus != 0)
            {
                throw new Exception(cmd.Error);
            }

            if (_sftpClient != null)
            {
                _sftpClient.ChangeDirectory(destinationDirectory);
                _scpDestinationDirectory = "";
            }
            else
            {
                _scpDestinationDirectory = destinationDirectory;
            }

            PrintTime($"Working directory changed to '{destinationDirectory}'");
        }

        private void DownloadFile(string path, Stream output)
        {
            if (_sftpClient != null)
            {
                _sftpClient.DownloadFile(path, output);
            }
            else
            {
                _scpClient.Download(_scpDestinationDirectory + "/" + path, output);
            }
        }

        private void UploadFile(Stream input, string path)
        {
            if (_sftpClient != null)
            {
                _sftpClient.UploadFile(input, path);
            }
            else
            {
                _scpClient.Upload(input, _scpDestinationDirectory + "/" + path);
            }
        }

        private ConcurrentDictionary<string, FileInfo> CreateLocalFileCache(DirectoryInfo sourceDirInfo)
        {
            var startIndex = sourceDirInfo.FullName.Length;
            var localFileCache = new ConcurrentDictionary<string, FileInfo>();
            Parallel.ForEach(GetFiles(sourceDirInfo.FullName), file =>
            {
                var cleanedRelativeFilePath = file.Substring(startIndex);
                cleanedRelativeFilePath = cleanedRelativeFilePath.Replace("\\", "/").TrimStart('/');
                localFileCache[cleanedRelativeFilePath] = new FileInfo(file);
            });

            PrintTime($"Local file cache created");
            return localFileCache;
        }

        private void DownloadAndCalculateChangedFiles(ConcurrentDictionary<string, FileInfo> localFileCache, StringBuilder fileListToDelete, ConcurrentDictionary<string, bool> filesNoUpdateNeeded)
        {
            try
            {
                using (MemoryStream currentRemoteCacheFile = new MemoryStream())
                {
                    DownloadFile(_uploadCacheFileName, currentRemoteCacheFile);

                    PrintTime($"Remote file cache '{_uploadCacheFileName}' downloaded");

                    currentRemoteCacheFile.Seek(0, SeekOrigin.Begin);
                    using (BinaryReader reader = new BinaryReader(currentRemoteCacheFile))
                    {
                        int entryCount = reader.ReadInt32();
                        int deleteFileCount = 0;
                        long deleteFileSize = 0;
                        int upToDateFileCount = 0;
                        long upToDateFileSize = 0;
                        long remoteFilesSize = 0;
                        for (int i = 0; i < entryCount; i++)
                        {
                            var remotefileEntry = UpdateCacheEntry.ReadFromStream(reader);
                            remoteFilesSize += remotefileEntry.FileSize;

                            FileInfo localFileInfo;
                            if (!localFileCache.TryGetValue(remotefileEntry.FilePath, out localFileInfo))
                            {
                                deleteFileCount++;
                                deleteFileSize += remotefileEntry.FileSize;
                                fileListToDelete.Append(remotefileEntry.FilePath).Append("\n");
                            }
                            else if (localFileInfo.LastWriteTimeUtc.Ticks == remotefileEntry.LastWriteTimeUtcTicks && localFileInfo.Length == remotefileEntry.FileSize)
                            {
                                upToDateFileCount++;
                                upToDateFileSize += remotefileEntry.FileSize;
                                filesNoUpdateNeeded[remotefileEntry.FilePath] = true;
                            }
                        }
                        PrintTime($"{deleteFileCount,7:n0} [{deleteFileSize,13:n0} bytes] of {entryCount,7:n0} [{remoteFilesSize,13:n0} bytes] files need to be deleted");
                        PrintTime($"{upToDateFileCount,7:n0} [{upToDateFileSize,13:n0} bytes] of {entryCount,7:n0} [{remoteFilesSize,13:n0} bytes] files don't need to be updated");
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO log verbose messages differently
                var msg = ex.Message;
                PrintTime($"Remote file cache '{_uploadCacheFileName}' not found! We are uploading all files!");
            }

            PrintTime($"Diff between local and remote file cache calculated");
        }

        private UpdateFlages CreateAndUploadFileDiff(ConcurrentDictionary<string, FileInfo> localFileCache, ConcurrentDictionary<string, bool> filesNoUpdateNeeded, string fileListToDelete)
        {
            var updateFlags = UpdateFlages.NONE;

            using (Stream tarGzStream = new MemoryStream())
            {
                using (var tarGzWriter = WriterFactory.Open(tarGzStream, ArchiveType.Tar, CompressionType.GZip))
                {
                    using (MemoryStream newCacheFileStream = new MemoryStream())
                    {
                        using (BinaryWriter newCacheFileWriter = new BinaryWriter(newCacheFileStream))
                        {
                            newCacheFileWriter.Write(localFileCache.Count);

                            var updateNeeded = false;
                            var updateFileCount = 0;
                            long updateFileSize = 0;
                            var allFileCount = 0;
                            long allFileSize = 0;

                            foreach (var file in localFileCache)
                            {
                                allFileCount++;
                                allFileSize += file.Value.Length;

                                // add new cache file entry
                                UpdateCacheEntry.WriteToStream(newCacheFileWriter, file.Key, file.Value);

                                // add new file entry
                                if (filesNoUpdateNeeded.ContainsKey(file.Key))
                                {
                                    continue;
                                }

                                updateNeeded = true;
                                updateFileCount++;
                                updateFileSize += file.Value.Length;

                                try
                                {
                                    tarGzWriter.Write(file.Key, file.Value);
                                }
                                catch (IOException ioEx)
                                {
                                    PrintError(ioEx, withStacktrace: false);
                                }
                                catch (Exception ex)
                                {
                                    PrintError(ex);
                                }
                            }

                            PrintTime($"{updateFileCount,7:n0} [{updateFileSize,13:n0} bytes] of {allFileCount,7:n0} [{allFileSize,13:n0} bytes] files need to be updated");

                            if (!string.IsNullOrEmpty(fileListToDelete))
                            {
                                updateFlags |= UpdateFlages.DELETE_FILES;

                                using (var deleteListStream = new MemoryStream(Encoding.UTF8.GetBytes(fileListToDelete.ToString())))
                                {
                                    UploadFile(deleteListStream, $"{_deleteListFileName}");
                                }

                                PrintTime($"Deleted file list '{_deleteListFileName}' uploaded");

                                if (!updateNeeded)
                                {
                                    PrintTime($"Only delete old files");
                                    return updateFlags;
                                }
                            }
                            else if (!updateNeeded)
                            {
                                PrintTime($"No update needed");
                                return updateFlags;
                            }

                            updateFlags |= UpdateFlages.UPADTE_FILES;

                            newCacheFileStream.Seek(0, SeekOrigin.Begin);
                            UploadFile(newCacheFileStream, $"{_uploadCacheTempFileName}");

                            PrintTime($"New remote file cache '{_uploadCacheTempFileName}' uploaded");
                        }
                    }
                }

                var tarGzStreamSize = tarGzStream.Length;
                tarGzStream.Seek(0, SeekOrigin.Begin);
                UploadFile(tarGzStream, _compressedUploadDiffContentFilename);

                PrintTime($"Compressed file diff '{_compressedUploadDiffContentFilename}' [{tarGzStreamSize,13:n0} bytes] uploaded");
            }

            return updateFlags;
        }

        private SshCommand UnzipCompressedFileDiffAndRemoveOldFiles(string destinationDirectory, UpdateFlages updateFlags)
        {
            var commandText = $"set -e;cd \"{destinationDirectory}\"";
            if (updateFlags.HasFlag(UpdateFlages.UPADTE_FILES))
            {
                commandText += $";tar -zxf \"{_compressedUploadDiffContentFilename}\"";
                if (_sshDeltaCopyOptions.RemoveTempDeleteListFile)
                {
                    commandText += $";rm \"{_compressedUploadDiffContentFilename}\"";
                }
                commandText += $";mv \"{_uploadCacheTempFileName}\" \"{_uploadCacheFileName}\"";
            }
            if (updateFlags.HasFlag(UpdateFlages.DELETE_FILES))
            {
                if (_sshDeltaCopyOptions.RemoveOldFiles)
                {
                    commandText += $";while read file ; do rm -f \"$file\" ; done < \"{_deleteListFileName}\"";
                }

                if (_sshDeltaCopyOptions.RemoveTempDeleteListFile)
                {
                    commandText += $";rm \"{_deleteListFileName}\"";
                }
            }

            SshCommand cmd = _sshClient.RunCommand(commandText);
            if (cmd.ExitStatus != 0)
            {
                throw new Exception(cmd.Error);
            }

            PrintTime($"Compressed file diff '{_compressedUploadDiffContentFilename}' unzipped {(_sshDeltaCopyOptions.RemoveTempDeleteListFile ? "and temp files cleaned up" : "and temp files not cleaned up")}");

            return cmd;
        }

        internal void PrintTime(string text)
        {
            if (_sshDeltaCopyOptions.PrintTimings)
            {
                var currentElapsedMilliseconds = _stopWatch.ElapsedMilliseconds;
                LogOutput?.Invoke($"[{(currentElapsedMilliseconds - _lastElapsedMilliseconds),7} ms][{currentElapsedMilliseconds,7} ms] {text}");
                _lastElapsedMilliseconds = currentElapsedMilliseconds;
            }
        }

        private void PrintError(Exception ex, bool withStacktrace = true)
        {
            if (withStacktrace)
            {
                LogOutput?.Invoke($"Exception: {ex.Message}\n{ex.StackTrace}");
            }
            else
            {
                LogOutput?.Invoke($"Exception: {ex.Message}");
            }
        }

        private static IEnumerable<string> GetFiles(string path)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                try
                {
                    foreach (string subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                string[] files = null;
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }
    }
}
