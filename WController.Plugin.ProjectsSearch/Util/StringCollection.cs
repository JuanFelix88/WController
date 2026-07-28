using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WController.Plugin.ProjectsSearch.Util;
internal class StringColletion : IEnumerable<string>
{
    private static readonly string GlobalCacheFile =
        Path.Combine(Path.GetTempPath(), "stringcollection_global.bin");

    private static readonly Dictionary<Guid, (long offset, int count)> IndexTable = new();
    private static bool _isInitialized = false;
    private static FileStream _globalFileStream;
    private static BinaryWriter _globalBinaryWriter;
    private static BinaryReader _globalBinaryReader;

    private readonly List<string> _strings = new List<string>();
    private Guid _id;

    public StringColletion()
    {
        _id = Guid.NewGuid();
    }

    public void Add(string value)
    {
        if (value != null)
            _strings.Add(value);
    }

    public void SaveInCache()
    {
        InitializeGlobalFileStream();
        
        long start = _globalFileStream.Position;
        _globalBinaryWriter.Write(_strings.Count);
        foreach (var s in _strings)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            _globalBinaryWriter.Write(bytes.Length);
            _globalBinaryWriter.Write(bytes);
        }

        IndexTable[_id] = (start, _strings.Count);

        _strings.Clear();
    }

    public static void ClearCache()
    {
        DisposeGlobalFileStream();
        if (File.Exists(GlobalCacheFile))
            File.Delete(GlobalCacheFile);
        IndexTable.Clear();
    }

    public static void InitializeGlobalFileStream()
    {
        if (_isInitialized) return;
        _globalFileStream = new FileStream(GlobalCacheFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        _globalBinaryWriter = new BinaryWriter(_globalFileStream, Encoding.UTF8);
        _globalBinaryReader = new BinaryReader(_globalFileStream, Encoding.UTF8);
        _isInitialized = true;
    }

    public static void DisposeGlobalFileStream()
    {
        if (!_isInitialized) return;
        _globalBinaryWriter?.Close();
        _globalBinaryReader?.Close();
        _globalFileStream?.Close();
        _isInitialized = false;
    }

    private IEnumerable<string> LoadFromCache()
    {
        if (!IndexTable.TryGetValue(_id, out var info))
            yield break;

        InitializeGlobalFileStream();
        _globalFileStream.Seek(info.offset, SeekOrigin.Begin);

        int count = _globalBinaryReader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int len = _globalBinaryReader.ReadInt32();
            var bytes = _globalBinaryReader.ReadBytes(len);
            yield return Encoding.UTF8.GetString(bytes);
        }
    }

    public IEnumerator<string> GetEnumerator()
    {
        if (_strings.Count > 0)
        {
            foreach (var s in _strings)
                yield return s;
        }
        else
        {
            foreach (var s in LoadFromCache())
                yield return s;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
