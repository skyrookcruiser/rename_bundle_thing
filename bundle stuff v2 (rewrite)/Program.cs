// See https://aka.ms/new-console-template for more information
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.IO;

string baseDir = AppDomain.CurrentDomain.BaseDirectory;

//path stuff
DirectoryInfo to_renamePath = Directory.CreateDirectory(Path.Combine(baseDir, "to_rename"));
DirectoryInfo exportPath = Directory.CreateDirectory(Path.Combine(baseDir, "export"));

AssetsManager manager = new AssetsManager();

Console.WriteLine($"Finding bundles in {to_renamePath.FullName}");
string[] paths = Directory.GetFiles(to_renamePath.FullName);
foreach (string path in paths)
{
    (BundleFileInstance bundleInst, AssetsFileInstance assetInst) = LoadBundle(manager, path);
    AssetBundleFile bundle = bundleInst.file;
    AssetsFile asset = assetInst.file;

    AssetBundleDirectoryInfo main = bundle.BlockAndDirInfo.DirectoryInfos[0];
    main.Name = bundleInst.name; //change bundle name
    Console.WriteLine($"changing StreamingInfo paths on {bundleInst.name}...");
    ChangeStreamingInfoPaths(manager, assetInst);
    main.SetNewData(asset); //confirm changes to asset
    Console.WriteLine($"packing {bundleInst.name}...");
    PackBundle(bundle, Path.Combine(exportPath.FullName, $"{bundleInst.name}"));
}

Console.WriteLine($"found [{paths.Length}] bundles");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

static (BundleFileInstance, AssetsFileInstance) LoadBundle(AssetsManager manager, string bundlePath)
{
    BundleFileInstance bundleInstance = manager.LoadBundleFile(bundlePath);
    AssetsFileInstance assetInstance = manager.LoadAssetsFileFromBundle(bundleInstance, 0, true);
    Console.WriteLine($"found bundle {bundleInstance.name}");
    return (bundleInstance, assetInstance);
}


static void ChangeStreamingInfoPaths(AssetsManager manager, AssetsFileInstance assetInst)
{
    string newPath = $"archive:/{assetInst.name}.resS";
    foreach(AssetFileInfo assetFileInfo in assetInst.file.AssetInfos)
    {
        AssetTypeValueField item = manager.GetBaseField(assetInst, assetFileInfo);
        AssetTypeValueField streamingInfo = item.Get("m_StreamData"); if (streamingInfo.ToString().StartsWith("DUMMY DUMMY")) continue;
        AssetTypeValueField path_StreamingInfo = streamingInfo.Get("path"); if (path_StreamingInfo.AsString.Length < 1) continue;
        path_StreamingInfo.AsString = newPath; //change path
        assetFileInfo.SetNewData(item); //set changes
    }
}

static void PackBundle(AssetBundleFile bundle, string path)
{
    using (AssetsFileWriter writer = new AssetsFileWriter(path + ".uncompressed")) { bundle.Write(writer);} //write uncompressed
    Console.WriteLine($"compressing...");
    AssetBundleFile uncompressed = new AssetBundleFile();
    uncompressed.Read(new AssetsFileReader(File.OpenRead(path + ".uncompressed")));
    using (AssetsFileWriter writer = new AssetsFileWriter(path)) { uncompressed.Pack(writer, AssetBundleCompressionType.LZ4);}
    uncompressed.Close();
    File.Delete(path + ".uncompressed");
}