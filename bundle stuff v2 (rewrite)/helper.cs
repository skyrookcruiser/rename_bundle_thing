using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SharpCompress.Compressors.Xz;
using System.IO.Compression;
using System.Security.Cryptography;
using SharpCompress.Common;

namespace bundle_renamer
{
    public class helper
    {

        public static (BundleFileInstance, AssetsFileInstance) LoadBundle(AssetsManager manager, string bundlePath)
        {
            //Console.WriteLine(bundlePath);
            BundleFileInstance bundleInstance = manager.LoadBundleFile(bundlePath);
            AssetsFileInstance assetInstance = manager.LoadAssetsFileFromBundle(bundleInstance, 0, true);
            
            //Console.WriteLine($"found bundle {bundleInstance.name} {bundleInstance.path}");
            var sb = new StringBuilder();
            sb.AppendLine("BUNDLE FOUND");
            sb.AppendLine("INPUT: " + bundlePath);
            sb.AppendLine("NAME: "+bundleInstance.name);
            sb.AppendLine("PATH: "+bundleInstance.path);
            Console.WriteLine(sb.ToString());
            return (bundleInstance, assetInstance);
        }

        public static (int, int) CloneAssetsFromBundle(AssetsManager manager, (BundleFileInstance bundleInst, AssetsFileInstance assetInst) original, (BundleFileInstance bundleInst, AssetsFileInstance assetInst) toClone, int typeID, string concatToItemName = "")
        {
            int success = 0;
            int fail = 0;
            AssetBundleDirectoryInfo ressData = toClone.bundleInst.file.BlockAndDirInfo.DirectoryInfos.Find((x) => x.Name.Contains(".resS"));
            var reader = toClone.bundleInst.file.DataReader;
            //var stream = new SegmentStream(toClone.bundleInst.file.DataReader.BaseStream, h.Offset, h.DecompressedSize);

            foreach (AssetFileInfo item in toClone.assetInst.file.GetAssetsOfType(typeID))
            {
                AssetTypeValueField __base = manager.GetBaseField(toClone.assetInst, item);
                if (original.assetInst.file.GetAssetInfo(item.PathId) != null)
                {
                    AssetFileInfo zzz = original.assetInst.file.GetAssetInfo(item.PathId);
                    AssetTypeValueField basefield = manager.GetBaseField(original.assetInst, zzz);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\x1b[38;2;220;20;60m[FAIL]\x1b[0m Skipping {__base.TypeName} {__base.Get("m_Name").AsString} because its pathId {item.PathId} already exists in original bundle!");
                    Console.WriteLine($"\u001b[38;2;220;20;60m[FAIL]\u001b[0m item in original bundle: {basefield.TypeName} {basefield.Get("m_Name").AsString} {zzz.PathId}");
                    fail++;
                    continue;
                }
                
                __base.Get("m_Name").AsString += concatToItemName;

                //create new to prevent it getting mangled
                AssetFileInfo __new = AssetFileInfo.Create(original.assetInst.file, item.PathId, item.TypeId, null);

                if (typeID == (int)AssetClassID.Texture2D)
                {
                    if (ressData != null)
                    {
                        var streamingInfo = __base.Get("m_StreamData");
                        var offset = streamingInfo.Get("offset").AsUInt;
                        var size = streamingInfo.Get("size").AsInt;
                        var path = streamingInfo.Get("path").AsString;
                        if (size < 1) continue;
                        reader.Position = (int)ressData.Offset + offset;
                        __base.Get("image data").AsByteArray = reader.ReadBytes(size);
                        Console.WriteLine($"{size} {offset} {item.PathId}");
                        streamingInfo.Get("size").AsInt = 0;
                        streamingInfo.Get("offset").AsUInt = 0;
                        streamingInfo.Get("path").AsString = "";
                        item.SetNewData(__base);
                    }
                    else Console.WriteLine($"resS file not found!!!!!!!!!");
                }
                

                __new.SetNewData(__base);

                //finally add
                original.assetInst.file.Metadata.AddAssetInfo(__new);
                Console.ResetColor();
                Console.WriteLine($"\u001b[38;2;127;255;212m[SUCCESS]\u001b[0m added {item.PathId} {__base.TypeName} {__base.Get("m_Name").AsString}");
                success++;
            }
            return (success, fail);
        }

        public static void ChangeStreamingInfoPaths(AssetsManager manager, AssetsFileInstance assetInst, BundleFileInstance bundleInst)
        {
            foreach (AssetFileInfo assetFileInfo in assetInst.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                //string newPath = $"archive:/{assetInst.name}.resS";

                AssetTypeValueField item = manager.GetBaseField(assetInst, assetFileInfo);
                AssetTypeValueField streamingInfo = item.Get("m_StreamData"); if (streamingInfo.IsDummy) continue;
                string path_StreamingInfo = streamingInfo.Get("path").AsString; if (path_StreamingInfo.Length < 1) continue;

                path_StreamingInfo = $"archive:/{path_StreamingInfo.Split(@"/")[^1]}";

                assetFileInfo.SetNewData(item); //set changes
            }
            Console.WriteLine("[INFO] Replacing StreamingInfo.path(s)...");
        }

        public static void PackBundle(AssetBundleFile bundle, string path, bool compress = false)
        {
            using (AssetsFileWriter writer = new AssetsFileWriter(path + ".uncompressed")) { bundle.Write(writer); } //write uncompressed
            Console.WriteLine($"compressing...");
            AssetBundleFile uncompressed = new AssetBundleFile();
            uncompressed.Read(new AssetsFileReader(File.OpenRead(path + ".uncompressed")));
            if (compress == true)
            {
                using (AssetsFileWriter writer = new AssetsFileWriter(path)) { uncompressed.Pack(writer, AssetBundleCompressionType.LZ4); }
                uncompressed.Close();
                File.Delete(path + ".uncompressed");
            }
        }

        public static void carraConverter(string filePath)
        {           
            var zipOutputPath = Directory.CreateDirectory(Path.Combine(Program.tempPath, Path.GetFileName(filePath)));
            using (ZipArchive archive = ZipFile.Open(filePath, ZipArchiveMode.Update))
            {
                archive.ExtractToDirectory(Path.Combine(zipOutputPath.FullName));
            }

            foreach (var path in Directory.GetDirectories(zipOutputPath.FullName))
            {
                DirectoryInfo cur = Directory.CreateDirectory(path);
                string expectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                   "..",
                                   "LocalLow",
                                   "Unity",
                                   "ProjectMoon_LimbusCompany",
                                    cur.Name,
                                    cur.GetDirectories()[0].Name,
                                    "__data"
                                    );
                //Console.WriteLine($"expected path: {expectedPath}");
                //Console.WriteLine(cur.Name);
                //BundleFileInstance bundleInstance = manager.LoadBundleFile(expectedPath);
                //AssetsFileInstance assetInstance = manager.LoadAssetsFileFromBundle(bundleInstance, 0, true);
                //Console.WriteLine(bundleInstance.path);

                var manager = new AssetsManager();
                var (sigma, skibidi) = LoadBundle(manager, expectedPath);
                AssetBundleFile bundle = sigma.file;
                AssetsFile asset = skibidi.file;

                Console.WriteLine("found mango mango: "+ bundle.BlockAndDirInfo.DirectoryInfos[0].DecompressedSize);

                asset.Metadata.Externals.ForEach((x) => Console.WriteLine(x.PathName));
                foreach (string rawData in Directory.GetFiles(cur.FullName, "", SearchOption.AllDirectories))
                {
                    using (var xz = new XZStream(File.OpenRead(rawData)))
                    using (Stream toFile = new FileStream(rawData + ".dat", FileMode.Create))
                    {
                        xz.CopyTo(toFile);
                    }


                    var bjgbgb = Path.GetFileName(rawData).Split('.');
                    long pathID = long.Parse(bjgbgb.First());
                    int scriptID = int.Parse(bjgbgb.Last());

                    
                    AssetFileInfo __new = AssetFileInfo.Create(asset, pathID, (int)AssetClassID.MonoBehaviour, null);
                    AssetFileInfo __what_the_sigma = AssetFileInfo.Create(asset, pathID, scriptID, null);
                    if (__what_the_sigma != null)
                    {
                        __new = __what_the_sigma;
                    }
                    //Console.WriteLine();
                    Console.WriteLine($"{(AssetClassID)scriptID + "?"} {scriptID} {pathID} {__what_the_sigma} {File.Exists(rawData + ".dat")}");
                    //var __new_basefield = manager.CreateTemplateBaseField(skibidi, scriptID);
                    var bru = File.ReadAllBytes(rawData + ".dat");
                    Console.WriteLine("raw data len: "+bru.Length);
                    Console.WriteLine($"i probably exist {__new.TypeId}");
                    __new?.SetNewData(bru);

                     AssetFileInfo exist = asset.GetAssetInfo(pathID);
                     if (exist != null) exist.SetNewData(bru);
                     else asset.Metadata.AddAssetInfo(__new);
                     
                }
                bundle.BlockAndDirInfo.DirectoryInfos[0].SetNewData(asset);
                DirectoryInfo finalPath = Directory.CreateDirectory(Path.Combine(Program.carraConvPath, zipOutputPath.Name));
                PackBundle(bundle, Path.Combine(finalPath.FullName, $"{cur.Name}.bundle"));
            }
        }

    }
}
