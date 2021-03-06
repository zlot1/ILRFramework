using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using com.aaframework.Runtime;
using ILRuntime.Mono.Cecil.Pdb;
using UnityEngine;
using AppDomain = ILRuntime.Runtime.Enviorment.AppDomain;

namespace com.ilrframework.Runtime
{
    public class ILRApp : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeInitializeOnLoad() {
            var go = new GameObject("ILRApp", typeof(ILRApp));
            DontDestroyOnLoad(go);
        }
        
        public static ILRApp Instance { get; private set; }
        
        private enum LoadState
        {
            NotLoad,
            Loading,
            Loaded,
        }
        
        public static bool UNITY_EDITOR { get; private set; }
        public static bool DEBUG { get; private set; }

        public ILRConfigurator Configurator { get; private set; }
        
        #region MonoBehaviour

        private void Awake() {
            Instance = this;
            
#if UNITY_EDITOR
            UNITY_EDITOR = true;
#else
            UNITY_EDITOR = false;
#endif

#if DEBUG
            DEBUG = true;
#else
            DEBUG = false;
#endif
            
            InitConfigurator();
        }

        private async void Start() {
            Configurator.OnAADownloadBefore();
            
            var info = await AADownloader.Instance.CheckCatalogUpdate();
            if (info.NeedUpdate) {
                Configurator.OnAANeedDownload(info, () => {
                    Configurator.OnAADownloadAfter();
                    Configurator.OnStartLoading();
                });
            }
            else {
                await Configurator.OnStartLoading();
            }
        }

        #endregion

        #region ILRConfigurator

        private void InitConfigurator() {
            var clazzType = typeof(ILRConfigurator);
            
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                foreach (var type in assembly.GetTypes()) {
                    if (type.IsClass && !type.IsAbstract && clazzType.IsAssignableFrom(type)) {
                        Configurator = (ILRConfigurator)Activator.CreateInstance(type);
                        return;
                    }
                }
            }

            throw new NotImplementedException("ILRConfigurator Not Implemented.");
        }

        #endregion

        #region HotFix

        private static LoadState _hotFixAssemblyLoadState = LoadState.NotLoad;

        private MemoryStream _dllStream;
        private MemoryStream _pdbStream;
    
        // AppDomain ??? ILRuntime ??????????????????????????????????????????????????????????????????????????????
        private AppDomain _domain = null;
        public AppDomain Domain {
            get {
                if (_domain == null) {
                    if (Application.isPlaying) {
                        throw new Exception("????????????????????? LoadHotFixAssembly");
                    }
                    else {
                        throw new Exception("???????????????????????????");
                    }
                }
                return _domain;
            }
            private set => _domain = value;
        }

        public void Dispose() {
            ILRRegister.ShutdownCLRBindings(Domain);
            
            if (_dllStream != null) _dllStream.Close();
            if (_pdbStream != null) _pdbStream.Close();
            _dllStream = null;
            _pdbStream = null;

            Domain = null;
        }

        /// <summary>
        /// ??????????????????
        /// </summary>
        public async Task LoadHotFixAssembly() {
            if (_hotFixAssemblyLoadState != LoadState.NotLoad) {
                var str = _hotFixAssemblyLoadState == LoadState.Loaded ? "Hotfix ?????????" : "???????????? Hotfix ???????????????";
                Debug.LogError($"{str}?????????????????????????????????????????????");
                return;
            }

            _hotFixAssemblyLoadState = LoadState.Loading;
        
            // ??????????????? ILRuntime ??? AppDomain???AppDomain?????????????????????????????????AppDomain???????????????????????????
            Domain = new AppDomain();
        
            // ??????DLL????????? DLL ????????????????????? HotFix_Project.sln ?????????
            var dll = await LoadDll(ILRConfig.DllPath);
            _dllStream = new MemoryStream(dll);
            
#if DEBUG
            // PDB ??????????????????????????????????????????????????????????????????????????????????????? PDB ??????
            // ????????????????????????????????????????????????????????? PDB ??????????????? LoadAssembly ????????? pdb ??? null ??????
            var pdb = await LoadDll(ILRConfig.PdbPath);
            _pdbStream = new MemoryStream(pdb);

            try {
                Domain.LoadAssembly(_dllStream, _pdbStream, new PdbReaderProvider());
            } catch {
                Debug.LogError($"????????????DLL?????????????????????????????? HotFix.sln ??????????????? DLL");
            }
#else
            try {
                Domain.LoadAssembly(_dllStream, null, new PdbReaderProvider());
            } catch {
                Debug.LogError($"????????????DLL?????????????????????????????? HotFix.sln ??????????????? DLL");
            }
#endif
            
            InitializeILRuntime();
            OnHotFixLoaded();
        }
        
        public static async Task<byte[]> LoadDll(string path) {
// #if UNITY_ANDROID
// #else
//             path = "file://" + path;
// #endif
//             var uri = new System.Uri(path);
//             
//             var getRequest = UnityWebRequest.Get(uri.AbsoluteUri);
//             await getRequest.SendWebRequest();
//             var data = getRequest.downloadHandler.data;
//             return data;

#if UNITY_EDITOR
            var asset = AAManager.Instance.LoadAssetSync<TextAsset>(path);
#else
            var asset = await AAManager.Instance.LoadAssetAsync<TextAsset>(path);
#endif
            return ILREncrypter.DecryptHotFixBytes(asset.bytes);
        }
        
        private void InitializeILRuntime() {
#if DEBUG && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IPHONE)
            // ?????? Unity ??? Profiler ???????????????????????????????????????????????????????????????????????? ILRuntime ?????????????????? ID ?????????????????????????????????????????? Profiler
            Domain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            
            Domain.DebugService.StartDebugService(56000);
             
//             // ???????????????????????????
//             // ???????????? Editor ???????????????????????????????????????????????????????????????
//             Domain.DebugService.OnBreakPoint += s => {
//                 var sb = new StringBuilder();
//
//                 var ds = Domain.DebugService;
//                 var t = ds.GetType();
//                 
//                 var curBreakpointFiled = t.GetField("curBreakpoint", BindingFlags.Instance | BindingFlags.Default | BindingFlags.Public | BindingFlags.NonPublic);
//                 var curBreakpoint = curBreakpointFiled.GetValue(ds);
//                 var breakpointType = curBreakpoint.GetType();
//
//                 var excProperty = breakpointType.GetProperty("Exception", BindingFlags.Instance | BindingFlags.Default | BindingFlags.Public | BindingFlags.NonPublic);
//                 var exception = (Exception) excProperty.GetValue(curBreakpoint);
//                 sb.AppendLine($"{exception.GetType().Name}: {exception.Message}");
//                 
//                 var intepreterProperty = breakpointType.GetProperty("Interpreter",
//                     BindingFlags.Instance | BindingFlags.Default | BindingFlags.Public | BindingFlags.NonPublic);
//                 var intepreter = (ILIntepreter) intepreterProperty.GetValue(curBreakpoint);
//                 var stackTrace = ds.GetStackTrace(intepreter);
//                 sb.AppendLine(stackTrace);
//                 
//                 Debug.LogError(sb.ToString());
//             };
#endif
            
            // ???????????????????????????
            Domain.DebugService.OnILRuntimeException += s => {
                var str = $"ILException:\n{s}";
                Debug.LogError(str);
                ILRExceptionPanel.Show(str);
#if !DEBUG
                IncLogger.Instance.PutLog(IncLogTopic.Log, str, IncLogger.MessageLevel.Error);
#endif
            };
            
            // ??????????????? ILRuntime ?????????
            ILRRegister.RegisterAll(Domain);
        }

        private void OnHotFixLoaded() {
            _hotFixAssemblyLoadState = LoadState.Loaded;
            
            Debug.Log("?????? HotFix ??????");
            
            // ?????? HotFix ????????????????????????????????? ILRBehaviour ?????????
            LoadHotFixIlrBehaviours();

            // ?????????????????????????????????
            EntryScene();
        }
        
        /// <summary>
        /// ?????????????????? ILRBehaviour ?????????
        /// </summary>
        private void LoadHotFixIlrBehaviours() {
            ILRComponent.AllIlrBehaviours.Clear();
            
            var hotFixTypes = Domain.LoadedTypes;
            var ilrBehaviourKey = "HotFix.Framework.ILRuntime.Core.ILRBehaviour";
            var exist = hotFixTypes.TryGetValue(ilrBehaviourKey, out _);
            if (!exist) {
                throw new Exception($"{ilrBehaviourKey} not exist in 'Domain.LoadedTypes'");
            }
                
            var keys = hotFixTypes.Keys.ToArray();
            var ilrBehaviour = hotFixTypes[ilrBehaviourKey].ReflectionType;
            foreach (var key in keys) {
                var t = hotFixTypes[key].ReflectionType;
                if (t.IsClass && !t.IsAbstract && IsSubclassOf(t, ilrBehaviour)) {
                    ILRComponent.AllIlrBehaviours.Add(t.FullName);
                }
            }
        }

        private bool IsSubclassOf(Type child, Type parent) {
            var ret = false;
            
            var t = child;
            while (!ret && t.BaseType != null) {
                t = t.BaseType;
                ret = t == parent;
            }

            return ret;
        }

        private async void EntryScene() {
            // await Task.Delay(5000);
            
            const string className = "HotFix.Framework.ILRuntime.Core.ILREntry";
            const string funcName = "EnterILRuntime";
            
            var type = Domain.LoadedTypes[className];
            var method = type.GetMethod(funcName, 1);
            using (var ctx = Domain.BeginInvoke(method)) {
                ctx.PushObject($"{Configurator.EntryScenePath()}");
                ctx.Invoke();
            }
        }

        #endregion
    }
}
