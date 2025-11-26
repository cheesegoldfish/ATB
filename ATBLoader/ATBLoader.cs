using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Windows.Media;
using ff14bot;
using ff14bot.AClasses;
using ff14bot.Helpers;
using TreeSharp;
using Action = TreeSharp.Action;
using ICSharpCode.SharpZipLib.Zip;
using ff14bot.Behavior;
using ff14bot.Managers;
using Newtonsoft.Json.Linq;

namespace ATBLoader
{
    /// <summary>
    /// Collectible AssemblyLoadContext for hot-reloading ATB.dll
    /// </summary>
    internal class ATBLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ATBLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // Let RebornBuddy assemblies resolve from the main context
            if (assemblyName.Name == "ff14bot" ||
                assemblyName.Name == "TreeSharp" ||
                assemblyName.Name == "GreyMagic" ||
                assemblyName.Name.StartsWith("RebornBuddy") ||
                assemblyName.Name.StartsWith("System") ||
                assemblyName.Name.StartsWith("Microsoft") ||
                assemblyName.Name == "Newtonsoft.Json" ||
                assemblyName.Name == "PresentationFramework" ||
                assemblyName.Name == "PresentationCore" ||
                assemblyName.Name == "WindowsBase")
            {
                return null; // Use default resolution
            }

            // Try to resolve using the dependency resolver
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    public class ATBLoader : BotBase
    {
        // Change this settings to reflect your project!
        private const string ProjectName = "ATB";
        private const string ProjectMainType = "ATB.ATB";
        private const string ProjectAssemblyName = "ATB.dll";
        private const string ZipUrl = "https://github.com/cheesegoldfish/ATB/releases/latest/download/ATB.zip";
        private const string VersionUrl = "https://github.com/cheesegoldfish/ATB/releases/latest/download/Version.txt";
        private static readonly Color LogColor = Colors.LawnGreen;

        private static readonly string ProjectAssembly = Path.Combine(Environment.CurrentDirectory, $@"BotBases\{ProjectName}\{ProjectAssemblyName}");
        private static readonly string GreyMagicAssembly = Path.Combine(Environment.CurrentDirectory, @"GreyMagic.dll");
        private static readonly string VersionPath = Path.Combine(Environment.CurrentDirectory, $@"BotBases\{ProjectName}\Version.txt");
        private static readonly string BaseDir = Path.Combine(Environment.CurrentDirectory, $@"BotBases\{ProjectName}");
        private static readonly string ProjectTypeFolder = Path.Combine(Environment.CurrentDirectory, @"BotBases");

        // Hot-reload: Load from temp copy so original file isn't locked
        private static readonly string TempDir = Path.Combine(BaseDir, "Temp");
        private static readonly string TempAssembly = Path.Combine(TempDir, ProjectAssemblyName);

        private static string _latestVersion;
        private static readonly object Locker = new object();

        // Hot reload support
        private static FileSystemWatcher _fileWatcher;
        private static DateTime _lastReloadTime = DateTime.MinValue;
        private static bool _isReloading = false;
        private static WeakReference _loadContextRef;
        private static bool _isTestVersion = false; // Track if we're in test/dev mode

        private static volatile bool _loaded;
        private static object Product { get; set; }

        private static MethodInfo StartFunc { get; set; }
        private static MethodInfo StopFunc { get; set; }
        private static MethodInfo ButtonFunc { get; set; }
        private static MethodInfo RootFunc { get; set; }
        private static MethodInfo InitFunc { get; set; }
        private static MethodInfo ShutdownFunc { get; set; }

        public override string Name => ProjectName;

        public override PulseFlags PulseFlags => PulseFlags.All;
        public override bool IsAutonomous => false;
        public override bool WantButton => true;
        public override bool RequiresProfile => false;

        public override Composite Root
        {
            get
            {
                if (!_loaded && Product == null) { LoadProduct(); }
                return Product != null ? (Composite)RootFunc.Invoke(Product, null) : new Action();
            }
        }

        public override void OnButtonPress()
        {
            if (!_loaded && Product == null) { LoadProduct(); }
            if (Product != null) { ButtonFunc.Invoke(Product, null); }
        }

        public override void Start()
        {
            if (!_loaded && Product == null) { LoadProduct(); }
            if (Product != null) { StartFunc.Invoke(Product, null); }
        }

        public override void Stop()
        {
            if (!_loaded && Product == null) { LoadProduct(); }
            if (Product != null) { StopFunc.Invoke(Product, null); }
        }

        public ATBLoader()
        {
            Task.Factory.StartNew(AutoUpdate).Wait();
            // LlamaLibrary loads before BotBases, so it's already available
            TryAddQuickStartButton();
        }

        private static bool _buttonAdded = false;

        private static void TryAddQuickStartButton()
        {
            if (_buttonAdded) return;

            try
            {
                // Check if user wants the button by reading settings JSON
                var settingsPath = Path.Combine(Environment.CurrentDirectory, $@"Settings\{Core.Me?.Name}\ATB\Main_Settings.json");
                if (!File.Exists(settingsPath)) return; // No settings file yet

                var json = File.ReadAllText(settingsPath);
                var settings = JObject.Parse(json);
                var useButton = settings["UseQuickStartButton"]?.Value<bool>() ?? false;

                if (!useButton) return; // Setting is disabled

                // LlamaLibrary is compiled as part of "Quest Behaviors_[hash].dll"
                var llamaAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a =>
                    {
                        var name = a.GetName().Name;
                        return name.StartsWith("Quest Behaviors", StringComparison.OrdinalIgnoreCase) ||
                               name.StartsWith("Quest_Behaviors", StringComparison.OrdinalIgnoreCase);
                    });

                if (llamaAssembly == null) return; // Quest Behaviors not installed

                var rbButtonHelperType = llamaAssembly.GetType("LlamaLibrary.Helpers.RbButtonHelper");
                if (rbButtonHelperType == null) return;

                var addButtonMethod = rbButtonHelperType.GetMethod("AddButton",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), typeof(System.Action) },
                    null);

                if (addButtonMethod == null) return;

                System.Action buttonAction = () =>
                {
                    try
                    {
                        if (TreeRoot.IsRunning)
                        {
                            TreeRoot.Stop($"Switching to {ProjectName}");
                            System.Threading.Thread.Sleep(500);
                        }

                        var atbBot = BotManager.Bots.FirstOrDefault(b => b.Name == ProjectName);
                        if (atbBot != null)
                        {
                            BotManager.SetCurrent(atbBot);
                            System.Threading.Thread.Sleep(300);

                            if (!TreeRoot.IsRunning)
                            {
                                TreeRoot.Start();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log($"Error: {e.Message}");
                    }
                };

                addButtonMethod.Invoke(null, new object[] { $"{ProjectName} Start", $"{ProjectName} Start", buttonAction });
                _buttonAdded = true;
            }
            catch
            {
                // Silently fail if LlamaLibrary not available
            }
        }

        private static void RedirectAssembly()
        {
            ResolveEventHandler handler = (sender, args) =>
            {
                string name = Assembly.GetEntryAssembly().GetName().Name;
                var requestedAssembly = new AssemblyName(args.Name);
                return requestedAssembly.Name != name ? null : Assembly.GetEntryAssembly();
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;

            ResolveEventHandler greyMagicHandler = (sender, args) =>
            {
                var requestedAssembly = new AssemblyName(args.Name);
                return requestedAssembly.Name != "GreyMagic" ? null : Assembly.LoadFrom(GreyMagicAssembly);
            };

            AppDomain.CurrentDomain.AssemblyResolve += greyMagicHandler;
        }

        private static Assembly LoadAssembly(string path, ATBLoadContext context)
        {
            if (!File.Exists(path)) { return null; }

            Assembly assembly = null;
            try
            {
                if (_isTestVersion)
                {
                    // Test mode: Load from bytes (no file lock!)
                    byte[] assemblyBytes = File.ReadAllBytes(path);

                    // Load with PDB if available for debugging
                    var pdbPath = Path.ChangeExtension(path, ".pdb");
                    if (File.Exists(pdbPath))
                    {
                        byte[] pdbBytes = File.ReadAllBytes(pdbPath);
                        assembly = context.LoadFromStream(new MemoryStream(assemblyBytes), new MemoryStream(pdbBytes));
                    }
                    else
                    {
                        assembly = context.LoadFromStream(new MemoryStream(assemblyBytes));
                    }
                }
                else
                {
                    // Production mode: Load from file path (standard, file will be locked)
                    assembly = context.LoadFromAssemblyPath(path);
                }
            }
            catch (Exception e) { Logging.WriteException(e); }

            return assembly;
        }

        /// <summary>
        /// Check if we're running a test/dev version (hot-reload enabled)
        /// </summary>
        private static bool IsTestVersion()
        {
            try
            {
                var localVersion = GetLocalVersion();
                return !string.IsNullOrEmpty(localVersion) && localVersion.StartsWith("test-", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static object Load()
        {
            RedirectAssembly();

            // Check if this is a test version (enables hot-reload)
            _isTestVersion = IsTestVersion();

            if (_isTestVersion)
            {
                Log("Test version detected - Hot-reload enabled (loading from bytes, no file lock)");

                // Clean up any old temp files from previous versions
                CleanupOldTempFiles();
            }
            else
            {
                Log("Production version - Hot-reload disabled");
            }

            // Create a new collectible AssemblyLoadContext
            var loadContext = new ATBLoadContext(ProjectAssembly);
            _loadContextRef = new WeakReference(loadContext, trackResurrection: true);

            // Load assembly (test mode loads from bytes, production loads from file path)
            var assembly = LoadAssembly(ProjectAssembly, loadContext);
            if (assembly == null) { return null; }

            Type baseType;
            try { baseType = assembly.GetType(ProjectMainType); }
            catch (Exception e)
            {
                Log(e.ToString());
                return null;
            }

            object bb;
            try { bb = Activator.CreateInstance(baseType); }
            catch (Exception e)
            {
                Log(e.ToString());
                return null;
            }

            if (bb != null) { Log(ProjectName + " was loaded successfully."); }
            else { Log("Could not load " + ProjectName + ". This can be due to a new version of Rebornbuddy being released. An update should be ready soon."); }

            return bb;
        }

        /// <summary>
        /// Hot reload ATB.dll without restarting RebornBuddy
        /// </summary>
        private static void ReloadProduct()
        {
            lock (Locker)
            {
                if (_isReloading)
                {
                    Log("Reload already in progress, skipping...");
                    return;
                }

                _isReloading = true;
                bool wasRunning = TreeRoot.IsRunning;

                try
                {
                    Log("Hot-reloading ATB.dll...");

                    // Stop TreeRoot completely FIRST
                    if (TreeRoot.IsRunning)
                    {
                        TreeRoot.Stop("Hot-reloading ATB");

                        // Wait for TreeRoot to fully stop
                        var stopTimeout = DateTime.Now.AddSeconds(5);
                        while (TreeRoot.IsRunning && DateTime.Now < stopTimeout)
                        {
                            System.Threading.Thread.Sleep(100);
                        }

                        if (TreeRoot.IsRunning)
                        {
                            Log("Warning: TreeRoot did not stop cleanly");
                        }
                    }

                    // Stop the current instance
                    if (Product != null)
                    {
                        if (StopFunc != null)
                        {
                            StopFunc.Invoke(Product, null);
                        }
                        if (ShutdownFunc != null)
                        {
                            ShutdownFunc.Invoke(Product, null);
                        }
                    }

                    // Clear all references to the old assembly
                    Product = null;
                    StartFunc = null;
                    StopFunc = null;
                    ButtonFunc = null;
                    RootFunc = null;
                    InitFunc = null;
                    ShutdownFunc = null;
                    _loaded = false;

                    // Unload the old AssemblyLoadContext (releases old temp file lock)
                    if (_loadContextRef != null && _loadContextRef.IsAlive)
                    {
                        var oldContext = _loadContextRef.Target as ATBLoadContext;
                        if (oldContext != null)
                        {
                            oldContext.Unload();
                        }
                    }

                    // Force garbage collection to clean up the old assembly
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Small delay before reloading
                    System.Threading.Thread.Sleep(300);

                    // Load the new version (loads from bytes in test mode, no file lock!)
                    LoadProduct();

                    Log("Hot-reload complete!");

                    // Restart TreeRoot if it was running before
                    if (wasRunning && Product != null && !TreeRoot.IsRunning)
                    {
                        // TreeRoot.Start() will call StartFunc automatically via the BotBase lifecycle
                        TreeRoot.Start();

                        // Wait for TreeRoot to start
                        System.Threading.Thread.Sleep(500);

                        Log("Bot automatically restarted after hot-reload.");
                    }
                }
                catch (Exception e)
                {
                    Log($"Hot-reload failed: {e.Message}");
                    Logging.WriteException(e);
                }
                finally
                {
                    _isReloading = false;
                    _lastReloadTime = DateTime.Now;
                }
            }
        }

        private static void LoadProduct()
        {
            lock (Locker)
            {
                if (Product != null) { return; }
                Product = Load();
                _loaded = true;
                if (Product == null) { return; }

                StartFunc = Product.GetType().GetMethod("Start");
                StopFunc = Product.GetType().GetMethod("Stop");
                ButtonFunc = Product.GetType().GetMethod("OnButtonPress");
                RootFunc = Product.GetType().GetMethod("GetRoot");
                InitFunc = Product.GetType().GetMethod("OnInitialize", new[] { typeof(int) });
                ShutdownFunc = Product.GetType().GetMethod("OnShutdown");

                if (InitFunc != null)
                {
#if RB_CN
                Log($"{ProjectName}CN loaded.");
                InitFunc.Invoke(Product, new[] {(object)2});
#else
                    Log($"{ProjectName}64 loaded.");
                    InitFunc.Invoke(Product, new[] { (object)1 });
#endif
                }

                // Set up file watcher for hot reload (only in test mode, only once)
                if (_isTestVersion && _fileWatcher == null)
                {
                    SetupFileWatcher();
                }
            }
        }

        /// <summary>
        /// Set up FileSystemWatcher to detect DLL changes for hot reload
        /// </summary>
        private static void SetupFileWatcher()
        {
            try
            {
                // Watch the original DLL location (not locked because we load from temp)
                _fileWatcher = new FileSystemWatcher
                {
                    Path = BaseDir,
                    Filter = ProjectAssemblyName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += OnDllChanged;
            }
            catch (Exception e)
            {
                Log($"Failed to set up hot-reload watcher: {e.Message}");
            }
        }

        /// <summary>
        /// Clean up old temp files from previous versions (no longer used with byte-loading)
        /// </summary>
        private static void CleanupOldTempFiles()
        {
            try
            {
                // Clean up any temp files from previous versions that used temp file strategy
                if (!Directory.Exists(TempDir))
                    return;

                var tempFiles = Directory.GetFiles(TempDir, "ATB_*.dll");

                foreach (var file in tempFiles)
                {
                    try
                    {
                        File.Delete(file);

                        // Also delete associated PDB
                        var pdb = Path.ChangeExtension(file, ".pdb");
                        if (File.Exists(pdb))
                        {
                            File.Delete(pdb);
                        }
                    }
                    catch
                    {
                        // File might still be locked - ignore
                    }
                }

                // Try to delete temp directory if empty
                try
                {
                    if (Directory.GetFiles(TempDir).Length == 0)
                    {
                        Directory.Delete(TempDir);
                    }
                }
                catch
                {
                    // Ignore if we can't delete the directory
                }
            }
            catch (Exception e)
            {
                Log($"Failed to clean up old temp files: {e.Message}");
            }
        }

        /// <summary>
        /// Handle DLL file changes
        /// </summary>
        private static void OnDllChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce: ignore if already reloading or too soon after last reload
            if (_isReloading || (DateTime.Now - _lastReloadTime).TotalSeconds < 3)
            {
                return;
            }

            Log($"Detected change in {ProjectAssemblyName}");

            // Disable watcher temporarily to prevent cascade
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
            }

            // Delay slightly to ensure file write is complete
            System.Threading.Thread.Sleep(500);

            Task.Run(() =>
            {
                ReloadProduct();

                // Re-enable watcher after reload
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = true;
                }
            });
        }

        /// <summary>
        /// Cleanup on disposal
        /// </summary>
        public static void Cleanup()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
        }

        private static void Log(string message)
        {
            message = "[Auto-Updater][" + ProjectName + "] " + message;
            Logging.Write(LogColor, message);
        }

        private static string GetLocalVersion()
        {
            if (!File.Exists(VersionPath)) return null;

            try
            {
                var version = File.ReadAllText(VersionPath).Trim();
                return version;
            }
            catch
            {
                return null;
            }
        }

        private static void AutoUpdate()
        {
            var stopwatch = Stopwatch.StartNew();
            var local = GetLocalVersion();
            _latestVersion = GetLatestVersion().Result;
            var latest = _latestVersion;

            if (local == latest || latest == null || local.StartsWith("pre-") || local.StartsWith("test-"))
            {
                LoadProduct();
                return;
            }

            Log($"Updating to Version: {latest}.");
            var bytes = DownloadLatestVersion().Result;

            if (bytes == null || bytes.Length == 0)
            {
                Log("[Error] Bad product data returned.");
                return;
            }

            if (!Clean(BaseDir))
            {
                Log("[Error] Could not clean directory for update.");
                return;
            }

            if (!Extract(bytes, ProjectTypeFolder + @"\ATB"))
            {
                Log("[Error] Could not extract new files.");
                return;
            }

            try
            {
                File.WriteAllText(VersionPath, latest);
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }

            stopwatch.Stop();
            Log($"Update complete in {stopwatch.ElapsedMilliseconds} ms.");
            LoadProduct();
        }

        private static async Task<string> GetLatestVersion()
        {
            using var client = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(VersionUrl);
            }
            catch (Exception e)
            {
                Log(e.Message);
                return null;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            string responseMessageBytes;
            try
            {
                responseMessageBytes = (await response.Content.ReadAsStringAsync()).Trim();
            }
            catch (Exception e)
            {
                Log(e.Message);
                return null;
            }

            return responseMessageBytes;
        }

        private static bool Clean(string directory)
        {
            foreach (var file in new DirectoryInfo(directory).GetFiles())
                try
                {
                    file.Delete();
                }
                catch
                {
                    return false;
                }

            foreach (var dir in new DirectoryInfo(directory).GetDirectories())
                try
                {
                    dir.Delete(true);
                }
                catch
                {
                    return false;
                }

            return true;
        }

        private static bool Extract(byte[] files, string directory)
        {
            using var stream = new MemoryStream(files);
            var zip = new FastZip();

            try
            {
                zip.ExtractZip(stream, directory, FastZip.Overwrite.Always, null, null, null, false, true);
            }
            catch (Exception e)
            {
                Log(e.ToString());
                return false;
            }

            return true;
        }

        private static async Task<byte[]> DownloadLatestVersion()
        {
            using var client = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(ZipUrl);
            }
            catch (Exception e)
            {
                Log(e.Message);
                return null;
            }

            if (!response.IsSuccessStatusCode)
                return null;

            byte[] responseMessageBytes;
            try
            {
                responseMessageBytes = await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception e)
            {
                Log(e.Message);
                return null;
            }

            return responseMessageBytes;
        }
    }
}
