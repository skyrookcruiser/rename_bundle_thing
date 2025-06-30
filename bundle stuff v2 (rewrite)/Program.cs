using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.Xz.Filters;
using System.Drawing;
using System.IO.Compression;
using System.Reflection.Metadata;

namespace bundle_renamer
{
    internal class Program
    {
        public static string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static DirectoryInfo to_renamePath = Directory.CreateDirectory(Path.Combine(baseDir, "to_rename"));
        public static DirectoryInfo exportPath = Directory.CreateDirectory(Path.Combine(baseDir, "export"));
        public static DirectoryInfo cloneFromPath = Directory.CreateDirectory(Path.Combine(baseDir, "cloneFrom"));
        public static string carraConvPath = Path.Combine(baseDir, "carra2bundle");
        public static string tempPath = Path.Combine(baseDir, "temp");

        static AssetsManager manager = new AssetsManager();

        [STAThread]
        static void Main(string[] args)
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            if (Directory.Exists(carraConvPath)) Directory.Delete(carraConvPath, true);
            Directory.CreateDirectory(carraConvPath);
            Directory.CreateDirectory(tempPath);

            List<(BundleFileInstance, AssetsFileInstance)> bruh = new();

            Console.WriteLine("Finding original bundle...");
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = "Select original bundle",
                InitialDirectory = baseDir,
                Multiselect = false

            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var bundlePath = ofd.FileName;
                if (Path.GetExtension(ofd.FileName).Contains("carra"))
                {                   
                    helper.carraConverter(bundlePath);
                }
                var yes = helper.LoadBundle(manager, ofd.FileName);
                bruh.Add(yes);
            }
            else Environment.Exit(0);

            OpenFileDialog ofd2 = new OpenFileDialog()
            {
                Title = "Select bundles to insert to original",
                InitialDirectory = baseDir,
                Multiselect = true

            };

            Console.WriteLine("Finding bundles to clone...");
            if (ofd2.ShowDialog() == DialogResult.OK)
            {
                BundleFileInstance bundleInst = bruh[0].Item1;
                AssetsFileInstance assetsInst = bruh[0].Item2;
                AssetBundleFile bundle = bundleInst.file;
                AssetsFile asset = assetsInst.file;

                AssetBundleDirectoryInfo main = bundle.BlockAndDirInfo.DirectoryInfos[0];
                helper.ChangeStreamingInfoPaths(manager, assetsInst, bundleInst);

                int success = 0;
                int fail = 0;
                foreach (string path in ofd2.FileNames)
                {
                    var (clBundleInst, clAssetInst) = helper.LoadBundle(manager, path);
                    var (s1, f1) = helper.CloneAssetsFromBundle(manager, (bundleInst, assetsInst), (clBundleInst, clAssetInst), (int)AssetClassID.Texture2D, "_experimental_t2d");
                    var (s2, f2) = helper.CloneAssetsFromBundle(manager, (bundleInst, assetsInst), (clBundleInst, clAssetInst), (int)AssetClassID.Sprite, "_experimental_sprite");
                    var (x1, x2) = helper.CloneAssetsFromBundle(manager, (bundleInst, assetsInst), (clBundleInst, clAssetInst), (int)AssetClassID.SpriteAtlas, "_experimental_atlas");
                    success += s1 + s2 + x1;
                    fail += f1 + f2 + x2;
                }
                Console.WriteLine($"[INFO] succeeded importing {success} files, failed to import {fail} files to original bundle");
                main.SetNewData(asset);
                helper.PackBundle(bundle, Path.Combine(exportPath.FullName, $"{bundleInst.name}"));
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            else Environment.Exit(0);

        }
    }
}
