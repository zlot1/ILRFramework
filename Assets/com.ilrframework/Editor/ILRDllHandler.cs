using System.IO;
using System.Threading.Tasks;
using com.aaframework.Runtime;
using com.ilrframework.Runtime;
using UnityEditor;
using UnityEngine;

namespace com.ilrframework.Editor
{
    public class ILRDllHandler : AssetPostprocessor
    {
        private static string SlnBuildDllPathInProject => Path.Combine("Assets", "StreamingAssets", ILRConfig.DLL_FILE_NAME);
        public static string SlnBuildDllFullPath => Path.Combine(Application.streamingAssetsPath, ILRConfig.DLL_FILE_NAME);

        private static string SlnBuildPdbPathInProject => Path.Combine("Assets", "StreamingAssets", ILRConfig.PDB_FILE_NAME);
        public static string SlnBuildPdbFullPath => Path.Combine(Application.streamingAssetsPath, ILRConfig.PDB_FILE_NAME);

        private static string OutputDllPathInAssetsPack => Path.Combine("HotFix", $"{ILRConfig.DLL_FILE_NAME}.bytes");
        private static string OutputPdbPathInAssetsPack => Path.Combine("HotFix", $"{ILRConfig.PDB_FILE_NAME}.bytes");
        
        private static string OutputDllFullPath => Path.Combine(Application.dataPath, AAConfig.AssetsRootFolder, OutputDllPathInAssetsPack);
        private static string OutputPdbFullPath => Path.Combine(Application.dataPath, AAConfig.AssetsRootFolder, OutputPdbPathInAssetsPack);

        private static string OutputDllPathInProject => Path.Combine("Assets", AAConfig.AssetsRootFolder, OutputDllPathInAssetsPack);

        private static string OutputPdbPathInProject =>
            Path.Combine("Assets", AAConfig.AssetsRootFolder, OutputPdbPathInAssetsPack);
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths) {

            var updateHotFixDll = false;
            var updateHotFixPdb = false;

            var dllPath = SlnBuildDllPathInProject.Replace("\\", "/");
            var pdbPath = SlnBuildPdbPathInProject.Replace("\\", "/");
            
            for (var i = 0; i < importedAssets.Length; i++) {
                var importedAsset = importedAssets[i];
                if (importedAsset.Equals(dllPath)) {
                    updateHotFixDll = true;
                } else if (importedAsset.Equals(pdbPath)) {
                    updateHotFixPdb = true;
                }
            }

            HandleHotFixDllAndPdb(updateHotFixDll, updateHotFixPdb);
        }

        public static async void HandleHotFixDllAndPdb(bool updateHotFixDll, bool updateHotFixPdb) {
            if (updateHotFixDll || updateHotFixPdb) {
                var title = "????????????????????????";
                var progress = 0f;
                EditorUtility.DisplayProgressBar(title, "????????????", progress);
                
                if (updateHotFixDll) {
                    progress += 0.2f;
                    EditorUtility.DisplayProgressBar(title, "???????????? dll ??????", progress);
                    HandleHotFixDll();
                    progress += 0.2f;
                    EditorUtility.DisplayProgressBar(title, "?????? dll ????????????", progress);
                }

                if (updateHotFixPdb) {
                    progress += 0.2f;
                    EditorUtility.DisplayProgressBar(title, "???????????? pdb ??????", progress);
                    HandleHotFixPdb();
                    progress += 0.2f;
                    EditorUtility.DisplayProgressBar(title, "?????? pdb ????????????", progress);
                }

                EditorUtility.DisplayProgressBar(title, "????????????...", progress);

                await Task.Delay(1000);
                ILRDllAnalyzer.ParsingHotFixDll();
                
                EditorUtility.DisplayProgressBar(title, "????????????", 1);
                EditorUtility.ClearProgressBar();

                if (!Application.isBatchMode) {
                    var assembly = typeof(UnityEditor.EditorWindow).Assembly;
                    var type = assembly.GetType("UnityEditor.SceneView");
                    EditorWindow.GetWindow(type).ShowNotification(new GUIContent("?????????????????????"), 2.0f);
                }
            }
        }

        private static void HandleHotFixDll() {
            if (!File.Exists(SlnBuildDllFullPath)) return;

            var outputDir = Path.GetDirectoryName(OutputDllFullPath);
            if (!Directory.Exists(outputDir)) {
                Directory.CreateDirectory(outputDir);
            }
            
            ILREncrypter.EncryptHotFixFile(SlnBuildDllFullPath, OutputDllFullPath);

            AssetDatabase.DeleteAsset(SlnBuildDllPathInProject);
            
            AssetDatabase.ImportAsset(OutputDllPathInProject);
            AssetDatabase.Refresh();
        }

        private static void HandleHotFixPdb() {
            if (!File.Exists(SlnBuildPdbFullPath)) return;
            
            var outputDir = Path.GetDirectoryName(OutputPdbFullPath);
            if (!Directory.Exists(outputDir)) {
                Directory.CreateDirectory(outputDir);
            }
            
            ILREncrypter.EncryptHotFixFile(SlnBuildPdbFullPath, OutputPdbFullPath);
            
            AssetDatabase.DeleteAsset(SlnBuildPdbPathInProject);
            
            AssetDatabase.ImportAsset(OutputPdbPathInProject);
            AssetDatabase.Refresh();
        }
    }
}
