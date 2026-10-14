using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

// Immutable, content-addressed scene baselines shared by save slots. A baseline
// is durably committed BEFORE any save referencing it is committed. Old inline
// templates remain readable and are migrated only on the next successful save.
public static class SaveSceneTemplateStore
{
    private static readonly Dictionary<string, string> verifiedFiles = new Dictionary<string, string>();
    public static string Folder => Path.Combine(GameSaveSystem.SaveDirectory, "scene-templates");

    public static SaveGameCatalog.SceneTemplate Reference(SaveGameCatalog.SceneTemplate source)
    {
        if (!string.IsNullOrEmpty(source.templateKey))
        {
            if (!Validate(source.templateKey)) throw new InvalidDataException("场景初始模板缺失或损坏：" + source.name);
            return new SaveGameCatalog.SceneTemplate { name = source.name, path = source.path, templateKey = source.templateKey };
        }
        byte[] bytes = !string.IsNullOrEmpty(source.authoredStateBase64)
            ? Convert.FromBase64String(source.authoredStateBase64) : Encoding.UTF8.GetBytes(source.authoredState ?? string.Empty);
        string key = Hash(bytes);
        string path = TemplatePath(key);
        if (!Validate(key))
        {
            Directory.CreateDirectory(Folder);
            string temporary = path + ".tmp";
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var gzip = new GZipStream(file, CompressionLevel.Fastest, true)) gzip.Write(bytes, 0, bytes.Length);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            verifiedFiles[path] = Stamp(path);
        }
        return new SaveGameCatalog.SceneTemplate { name = source.name, path = source.path, templateKey = key };
    }

    public static string Read(string key)
    {
        string path = TemplatePath(key);
        using (var file = File.OpenRead(path))
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gzip.CopyTo(output);
            byte[] bytes = output.ToArray();
            if (Hash(bytes) != key) throw new InvalidDataException("场景初始模板校验失败。");
            verifiedFiles[path] = Stamp(path);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public static bool Validate(string key)
    {
        if (!ValidKey(key)) return false;
        string path = TemplatePath(key);
        if (!File.Exists(path)) return false;
        if (verifiedFiles.TryGetValue(path, out string previous) && previous == Stamp(path)) return true;
        try { Read(key); return true; } catch (Exception) { return false; }
    }
    private static string Stamp(string path)
    {
        var info = new FileInfo(path);
        return info.Length + ":" + info.LastWriteTimeUtc.Ticks;
    }
    private static bool ValidKey(string key)
    {
        if (key == null || key.Length != 64) return false;
        foreach (char c in key) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
        return true;
    }
    private static string TemplatePath(string key)
    {
        if (!ValidKey(key)) throw new InvalidDataException("无效的场景初始模板标识。");
        return Path.Combine(Folder, key + ".gz");
    }
    private static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
