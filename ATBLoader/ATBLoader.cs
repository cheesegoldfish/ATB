using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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
        private static string? _latestVersion;
        private static readonly object Locker = new object();

        private static volatile bool _loaded;
        private static object Product { get; set; }

        private static MethodInfo StartFunc { get; set; }
        private static MethodInfo StopFunc { get; set; }
        private static MethodInfo ButtonFunc { get; set; }
        private static MethodInfo RootFunc { get; set; }
        private static MethodInfo InitFunc { get; set; }

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

        private static Assembly LoadAssembly(string path)
        {
            if (!File.Exists(path)) { return null; }

            Assembly assembly = null;
            try { assembly = Assembly.LoadFrom(path); }
            catch (Exception e) { Logging.WriteException(e); }

            return assembly;
        }

        private static object Load()
        {
            RedirectAssembly();

            var assembly = LoadAssembly(ProjectAssembly);
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
            }
        }

        private static void Log(string message)
        {
            message = "[Auto-Updater][" + ProjectName + "] " + message;
            Logging.Write(LogColor, message);
        }

        private static string? GetLocalVersion()
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

            if (local == latest || latest == null || local.StartsWith("pre-"))
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

        private static async Task<string?> GetLatestVersion()
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

        private static async Task<byte[]?> DownloadLatestVersion()
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
