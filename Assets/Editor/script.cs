using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Specialized;
using Newtonsoft.Json;

public class BuildAssetBundlesBuildMapExample : MonoBehaviour
{
    public static byte[] EncryptMST(byte[] rawData)
    {
        string password = "3559b435f24b297a79c68b9709ef2125";
        byte[] salt = new byte[16];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(salt);
        }

        Rfc2898DeriveBytes pbk = new Rfc2898DeriveBytes(password, salt, 1000);
        byte[] key = pbk.GetBytes(16);

        byte[] iv = new byte[16];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(iv);
        }

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;
            aesAlg.Mode = CipherMode.CBC;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using (var msEncrypt = new System.IO.MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(rawData, 0, rawData.Length);
                    csEncrypt.FlushFinalBlock();
                    byte[] encryptedData = msEncrypt.ToArray();

                    // Construct the encrypted MST format
                    byte[] encryptedMst = new byte[salt.Length + iv.Length + 1 + encryptedData.Length];
                    Array.Copy(salt, 0, encryptedMst, 0, salt.Length);
                    Array.Copy(iv, 0, encryptedMst, salt.Length, iv.Length);
                    encryptedMst[salt.Length + iv.Length] = (byte)iv.Length;
                    Array.Copy(encryptedData, 0, encryptedMst, salt.Length + iv.Length + 1, encryptedData.Length);
                    return encryptedMst;
                }
            }
        }
    }
    
    public static byte[] EncryptAES_CBC(string key, string iv, byte[] buffer)
    {
        if (key == null || key.Length != 32)
            throw new ArgumentException("Key must be 32 characters long.");

        if (iv == null || iv.Length != 16)
            throw new ArgumentException("IV must be 16 characters long.");

        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv);

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = keyBytes;
            aesAlg.IV = ivBytes;
            aesAlg.Mode = CipherMode.CBC;
            aesAlg.Padding = PaddingMode.PKCS7;

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesAlg.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    csEncrypt.Write(buffer, 0, buffer.Length);
                }
                return msEncrypt.ToArray();
            }
        }
    }
    private static void Init()
    {
        var path = "Assets/StreamingAssets/out/";
        if (Directory.Exists(path))
        {
            var dir = new DirectoryInfo(path);
            dir.Delete(true);
        }
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(path + "jp/Android");
        Directory.CreateDirectory(path + "jp/iOS");
        Directory.CreateDirectory(path + "gl/Android");
        Directory.CreateDirectory(path + "gl/iOS");
    }
    private static void MoveOutput(string outFile, bool android, bool jp)
    {
        var basePath = "Assets/StreamingAssets/out/" + (jp ? "jp" : "gl") + "/" + (android ? "Android" : "iOS") + "/";
        var readText = File.ReadAllText(outFile + ".manifest");
        var hash = readText.Split("AssetFileHash").Last().Split("Hash:")[1].Split("\n")[0].Trim();
        var path = basePath + hash + "/" + outFile.Split("/").Last();
        Directory.CreateDirectory(basePath + hash);
        Debug.Log(hash);
        
        File.Copy(outFile, path);
    }
    
    private static string BuildBundle(string folderPath, string bundleName, bool android, bool jp)
    {
        var buildMap = new AssetBundleBuild[2];
        buildMap[0].assetBundleName = bundleName;

        var contents = Directory.GetFiles(folderPath);
        
        buildMap[0].assetNames = contents;

        var path = "Assets/StreamingAssets/temp";
        if (Directory.Exists(path))
        {
            var dir = new DirectoryInfo(path);
            dir.Delete(true);
        }
        Directory.CreateDirectory(path);
        
        BuildPipeline.BuildAssetBundles(path, buildMap, BuildAssetBundleOptions.ChunkBasedCompression, android ? BuildTarget.Android : BuildTarget.iOS);
        MoveOutput(path + "/" + bundleName, android, jp);
        return path + "/" + bundleName;
    }

    public static byte[] Compress(byte[] data)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return memoryStream.ToArray();
        }
    }

    private static byte[] UpdateData(string file, string type, string lastBuild)
    {
        var readText = File.ReadAllText(lastBuild + ".manifest");
        var hash = readText.Split("AssetFileHash").Last().Split("Hash:")[1].Split("\n")[0].Trim();
        var crc = uint.Parse(readText.Split("CRC:").Last().Split("\n")[0].Trim());
        var length = new System.IO.FileInfo(lastBuild).Length;
        
        if (type == "Bundle.json")
        {
            var obj = JsonConvert.DeserializeObject<ShockBinaryBundleSingleManifest>(File.ReadAllText(file));
            foreach (var item in obj.m_manifestCollection)
            {
                if (item.m_identifier != "mst.ab")
                {
                    continue;
                }
                item.m_hash = hash;
                item.m_crc = crc;
                item.m_length = length;
                break;
            }
            SerializeObject<ShockBinaryBundleSingleManifest>(obj, "temp.bin");
            return File.ReadAllBytes("temp.bin");
        }

        return File.ReadAllBytes(file);
    }

    private static void EncryptFiles(string folder, string path, string lastBuild)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        var files = Directory.GetFiles(folder);
        foreach (var file in files)
        {
            if (file.EndsWith("Bundle.json") || file.EndsWith("Movie") || file.EndsWith("Sound")) {
                var encrypted = EncryptAES_CBC("akmzncej3dfheuds654sg9ad1f3fnfoi", "lmxcye89bsdfb0a1", Compress(UpdateData(file, file.Split("/").Last(), lastBuild)));
                File.WriteAllBytes(path + file.Split("/").Last() + ".bytes", encrypted);

            } else if (file.IndexOf(".") == -1)
            {
                var encrypted = EncryptMST(File.ReadAllBytes(file));
                File.WriteAllBytes(path + file.Split("/").Last() + ".bytes", encrypted);
            }
            else if (file.Split("/").Last() == "release_label.json")
            {
                var release_label = JsonConvert.DeserializeObject<ReleaseLabelMst[]>(File.ReadAllText(file));
                SerializeObject<ReleaseLabelMst[]>(release_label, "temp.bin");
                var encrypted = EncryptMST(File.ReadAllBytes("temp.bin"));
                var outPath = file.Split("/").Last().Substring(0, file.Split("/").Last().Length-5);
                File.WriteAllBytes(path + outPath + ".bytes", encrypted);
            }
        }
    }

    
    private static string BuildFromArch(bool android, bool jp)
    {
        var arch = android ? "Android" : "iOS";
        var basePath = "Assets/TextAsset/" + (jp ? "jp" : "gl") + "/";

        EncryptFiles(basePath + "Masterdata/Raw/", basePath + "Masterdata/Encrypted/", "");
        var last = BuildBundle(basePath + "Masterdata/Encrypted/", "6572ca8348bc566b8cf01d43c4cc1b58.unity3d", android, jp);
        
        EncryptFiles(basePath + "Manifest/Raw/", basePath + "Manifest/Encrypted/", last);
        
        var lastBuild = BuildBundle(basePath + "Manifest/Encrypted/", "387b0126300c54515911bffb6540982d.unity3d", android, jp);
        var readText = File.ReadAllText(lastBuild + ".manifest");
        var hash = readText.Split("AssetFileHash").Last().Split("Hash:")[1].Split("\n")[0].Trim();
        return hash;
    }
    
    [MenuItem("Build/Build")]
    public static void Build()
    {
        Init();
        var hash = BuildFromArch(true, true);
        var hash2 = BuildFromArch(false, true);

        var hash3 = BuildFromArch(true, false);
        var hash4 = BuildFromArch(false, false);
        Debug.Log("Build finished.\n Android JP asset hash: " + hash + "\n iOS JP asset hash: " + hash2 + "\n Android GL asset hash: " + hash3 + "\n iOS GL asset hash: " + hash4);
    }

    //Serialisation
    public static ShockBinaryBundleSingleManifest DeserializeObject(string filePath)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Open))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Binder = new CustomSerializationBinder();
            return (ShockBinaryBundleSingleManifest)formatter.Deserialize(fs);
        }
    }

    public static void SerializeObject<T>(T obj, string filePath)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Create))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Binder = new CustomSerializationBinder();
            formatter.Serialize(fs, obj);
        }
    }
}



public class CustomSerializationBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        Debug.Log(assemblyName);
        switch(typeName) {
            case "ShockBinaryBundleSingleManifest":
                return typeof(ShockBinaryBundleSingleManifest);
            case "ShockBinaryBundleManifest":
                return typeof(ShockBinaryBundleManifest);
            case "Shock.ReleaseLabelMst":
                return typeof(ReleaseLabelMst);
        }
        return null;
    }
    public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        typeName = null;
        assemblyName = "Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        if (serializedType == typeof(ShockBinaryBundleSingleManifest))
        {
            typeName = "ShockBinaryBundleSingleManifest";
        } else if (serializedType == typeof(ShockBinaryBundleManifest))
        {
            typeName = "ShockBinaryBundleManifest";
        } else if (serializedType == typeof(ReleaseLabelMst))
        {
            typeName = "Shock.ReleaseLabelMst";
        }

        Debug.Log(typeName);
    }
}

[Serializable]
public class ShockBinaryBundleSingleManifest
{
    // Fields
    public ShockBinaryBundleManifest[] m_manifestCollection; // 0x10
}

[Serializable]
public class ShockBinaryBundleManifest // TypeDefIndex: 71
{
    // Fields
    public string m_identifier; // 0x10
    public string m_name; // 0x18
    public string m_hash; // 0x20
    public uint m_crc; // 0x30
    public long m_length; // 0x38
    public string[] m_dependencies; // 0x40
    public string[] m_labels; // 0x48
    public string[] m_assets; // 0x50

    // Properties
    public string identifier { get; }
    public string name { get; }
    public Hash128 hash { get; }
    public uint crc { get; }
    public long length { get; }
    public string[] dependencies { get; }
    public string[] labels { get; }
    public string[] assets { get; }
}

[Serializable]
public class ReleaseLabelMst // TypeDefIndex: 5320
{
    // Fields
    public uint _id; // 0x10
    public string _description; // 0x18
    public uint _releaseStatus; // 0x20
    public string _scope; // 0x28
    public string _openedAt; // 0x30
    public string _closedAt; // 0x38
}
