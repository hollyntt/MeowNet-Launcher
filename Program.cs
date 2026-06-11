using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using Valve.VR;

namespace MeowNet_Launcher
{
    class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        private static string _gameDir = "";
        private static string _status = "Environment ready.";
        private static bool _isError = false;
        private static bool _hasPatch = false;
        private static bool _wasRunning = false;
        private static bool _hasDotNet6 = false;
        private static bool _hasVcRedist = false;
        private static bool _isUpdating = false;
        private static bool _needsUpdate = false;
        private static string _localVersion = "0";
        private static string _remoteVersion = "0";
        private static float _updateProgress = 0f;
        private static float _animTime = 0f;
        private static readonly Vector4 MeowPink = new Vector4(1.00f, 0.27f, 0.43f, 1f);

        static void Main()
        {
            HideConsoleInRelease();
            FindGameDirectory();
            RegisterVrManifest();
            _hasPatch = CheckForWoofPatch();
            _hasDotNet6 = IsDotNet6Installed();
            _hasVcRedist = IsVcRedistInstalled();
            _localVersion = GetLocalVersion();

            Task.Run(() => CheckForUpdates());

            Raylib.InitWindow(720, 520, "MeowNet Launcher");
            Raylib.SetTargetFPS(60);
            rlImGui.Setup(true);

            LoadEmbeddedIcon();
            ApplyMeowStyle();

            while (!Raylib.WindowShouldClose())
            {
                _animTime += Raylib.GetFrameTime();
                
                bool isRunning = Process.GetProcessesByName("RecRoom").Length > 0;
                if (isRunning != _wasRunning)
                {
                    if (isRunning)
                    {
                        Raylib.MinimizeWindow();
                    }
                    else
                    {
                        Raylib.RestoreWindow();
                    }
                    _wasRunning = isRunning;
                }

                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color(26, 26, 36, 255));
                rlImGui.Begin();
                DrawMeowUI(isRunning);
                rlImGui.End();
                Raylib.EndDrawing();
            }

            rlImGui.Shutdown();
            Raylib.CloseWindow();
        }

        [Conditional("RELEASE")]
        private static void HideConsoleInRelease() => FreeConsole();

        private static void FindGameDirectory()
        {
            _gameDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? "";
        }

        private static string GetLocalVersion()
        {
            string versionPath = Path.Combine(_gameDir, "version.txt");
            if (File.Exists(versionPath))
            {
                try
                {
                    return File.ReadAllText(versionPath).Trim();
                }
                catch { }
            }
            return "0";
        }

        private static void CheckForUpdates()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Head, "https://ahhhhhhhhhhhhhhhhhhh.b-cdn.net/Meow!Beta.7z");
                    using (var response = client.Send(request))
                    {
                        response.EnsureSuccessStatusCode();
                        
                        string remoteTag = "";
                        if (response.Headers.ETag != null)
                        {
                            remoteTag = response.Headers.ETag.Tag.Trim('\"');
                        }
                        else if (response.Content.Headers.LastModified.HasValue)
                        {
                            remoteTag = response.Content.Headers.LastModified.Value.UtcDateTime.Ticks.ToString();
                        }

                        if (!string.IsNullOrEmpty(remoteTag))
                        {
                            _remoteVersion = remoteTag;
                            _needsUpdate = _localVersion != _remoteVersion;
                        }
                    }
                }
            }
            catch
            {
                _needsUpdate = true;
            }
        }

        private static bool IsDotNet6Installed()
        {
            string pathApp = @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App";
            string pathDesktop = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";

            bool appOk = Directory.Exists(pathApp) && Directory.GetDirectories(pathApp).Any(d => Path.GetFileName(d).StartsWith("6.0."));
            bool desktopOk = Directory.Exists(pathDesktop) && Directory.GetDirectories(pathDesktop).Any(d => Path.GetFileName(d).StartsWith("6.0."));

            return appOk || desktopOk;
        }

        private static bool IsVcRedistInstalled()
        {
            return File.Exists(@"C:\Windows\System32\vcruntime140.dll");
        }

        private static void InstallPrerequisite(string url, string exeName, string silentArgs, string displayName)
        {
            _status = $"Downloading {displayName}...";
            _isError = false;
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), exeName);
                using (var client = new HttpClient())
                {
                    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        using (var streamToRead = response.Content.ReadAsStream())
                        using (var streamToWrite = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            streamToRead.CopyTo(streamToWrite);
                        }
                    }
                }
                _status = $"Installing {displayName} silently...";
                var psi = new ProcessStartInfo(tempPath, silentArgs)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                var process = Process.Start(psi);
                process?.WaitForExit();
                _status = $"{displayName} successfully installed!";
            }
            catch (Exception ex)
            {
                _status = $"Failed to install {displayName}: {ex.Message}";
                _isError = true;
            }
        }

        private static bool CheckForWoofPatch()
        {
            string pluginPath = Path.Combine(_gameDir, "BepInEx", "plugins", "WoofPatch.dll");
            if (!File.Exists(pluginPath))
            {
                _status = "WoofPatch Missing!";
                _isError = true;
                return false;
            }
            _status = "WoofPatch detected!";
            return true;
        }

        private static void RegisterVrManifest()
        {
            const string appKey = "meownet.launcher.meow";
            string manifestPath = Path.Combine(_gameDir, "meownet.vrmanifest");
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            try
            {
                var error = EVRInitError.None;
                OpenVR.Init(ref error, EVRApplicationType.VRApplication_Utility);
                if (error == EVRInitError.None)
                {
                    if (!OpenVR.Applications.IsApplicationInstalled(appKey))
                    {
                        string json = $@"{{ ""source"": ""builtin"", ""applications"": [ {{ ""app_key"": ""{appKey}"", ""launch_type"": ""binary"", ""binary_path_windows"": ""{exePath.Replace("\\", "\\\\")}"", ""working_directory"": ""{_gameDir.Replace("\\", "\\\\")}"", ""strings"": {{ ""en_us"": {{ ""name"": ""MeowNet Launcher"" }} }} }} ] }}";
                        File.WriteAllText(manifestPath, json);
                        OpenVR.Applications.AddApplicationManifest(manifestPath, false);
                    }
                    OpenVR.Shutdown();
                }
            }
            catch { }
        }

        private static unsafe void LoadEmbeddedIcon()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();
            string resourceName = names.FirstOrDefault(n => n.EndsWith("meow.png"));
            if (resourceName == null) return;

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            fixed (byte* pData = data)
            {
                byte[] ext = { (byte)'.', (byte)'p', (byte)'n', (byte)'g', 0 };
                fixed (byte* pExt = ext)
                {
                    Image img = Raylib.LoadImageFromMemory((sbyte*)pExt, pData, data.Length);
                    if ((IntPtr)img.Data != IntPtr.Zero) 
                    { 
                        Raylib.SetWindowIcon(img); 
                        Raylib.UnloadImage(img); 
                    }
                }
            }
        }

        private static void DrawMeowUI(bool isRunning)
        {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
            
            UpdateTitleBarStyle(isRunning);

            ImGui.Begin("MeowNet Launcher", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            ImGui.TextDisabled("Game Path:");
            ImGui.TextWrapped(_gameDir);
            ImGui.Spacing();
            ImGui.TextDisabled("WoofPatch Status:");
            ImGui.SameLine(130);
            ImGui.TextColored(_hasPatch ? new Vector4(0.5f, 1f, 0.5f, 1f) : MeowPink, _hasPatch ? "Patch Detected" : "WoofPatch Missing!");

            ImGui.Dummy(new Vector2(0, 10));

            if (!_hasDotNet6)
            {
                ImGui.TextColored(MeowPink, "Prerequisite Missing: .NET 6.0");
                ImGui.SameLine(250);
                if (ImGui.Button("Auto Install .NET", new Vector2(150, 20)))
                {
                    Task.Run(() =>
                    {
                        InstallPrerequisite("https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe", "dotnet6-installer.exe", "/install /quiet /norestart", ".NET 6.0");
                        _hasDotNet6 = IsDotNet6Installed();
                    });
                }
            }

            if (!_hasVcRedist)
            {
                ImGui.TextColored(MeowPink, "Prerequisite Missing: VC++ Redist");
                ImGui.SameLine(250);
                if (ImGui.Button("Auto Install VC++", new Vector2(150, 20)))
                {
                    Task.Run(() =>
                    {
                        InstallPrerequisite("https://aka.ms/vs/17/release/vc_redist.x64.exe", "vc_redist.x64.exe", "/install /quiet /norestart", "VC++ Redistributable");
                        _hasVcRedist = IsVcRedistInstalled();
                    });
                }
            }

            ImGui.Dummy(new Vector2(0, 10));

            if (isRunning)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.1f, 0.1f, 1f));
                if (ImGui.Button("Kill Rec Room", new Vector2(ImGui.GetContentRegionAvail().X, 50))) KillRecRoom();
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.10f, 0.15f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, MeowPink);
                if (ImGui.Button("Launch MeowNet", new Vector2(ImGui.GetContentRegionAvail().X, 50)))
                {
                    if (!_hasPatch)
                    {
                        _status = "Error: WoofPatch not found! Try updating.";
                        _isError = true;
                    }
                    else if (!IsSteamRunning())
                    {
                        _status = "Error: Steam is not running! Open Steam first.";
                        _isError = true;
                    }
                    else
                    {
                        LaunchGame();
                    }
                }
                ImGui.PopStyleColor(2);
            }

            if (_needsUpdate && !_isUpdating)
            {
                ImGui.Dummy(new Vector2(0, 10));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.1f, 0.2f, 0.4f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, MeowPink);
                if (ImGui.Button("Update Game", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    Task.Run(() => UpdateGame());
                }
                ImGui.PopStyleColor(2);
            }

            if (_isUpdating)
            {
                ImGui.Dummy(new Vector2(0, 8));
                ImGui.ProgressBar(_updateProgress, new Vector2(ImGui.GetContentRegionAvail().X, 22), $"{(_updateProgress * 100):0.0}%");
            }

            ImGui.Dummy(new Vector2(0, 10));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.3f, 1f));
            if (ImGui.Button("Join Discord", new Vector2(ImGui.GetContentRegionAvail().X, 30))) 
                Process.Start(new ProcessStartInfo("https://discord.gg/recnet") { UseShellExecute = true });
            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(_status))
            {
                ImGui.Dummy(new Vector2(0, 10));
                ImGui.TextColored(_isError ? MeowPink : new Vector4(0.5f, 1f, 0.5f, 1f), _status);
            }

            ImGui.End();
        }

        private static void UpdateTitleBarStyle(bool isRunning)
        {
            var colors = ImGui.GetStyle().Colors;
            float pulse = (float)(0.5f + 0.5f * Math.Sin(_animTime * 3));
            if (isRunning)
            {
                colors[(int)ImGuiCol.TitleBg] = new Vector4(0.1f + pulse * 0.1f, 0.4f + pulse * 0.1f, 0.1f, 1f);
                colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.2f + pulse * 0.2f, 0.6f + pulse * 0.2f, 0.2f, 1f);
            }
            else
            {
                colors[(int)ImGuiCol.TitleBg] = new Vector4(0.4f + pulse * 0.1f, 0.1f + pulse * 0.05f, 0.1f, 1f);
                colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.6f + pulse * 0.2f, 0.2f + pulse * 0.1f, 0.2f, 1f);
            }
        }

        private static void ApplyMeowStyle()
        {
            ImGui.StyleColorsDark();
            var style = ImGui.GetStyle();
            style.WindowRounding = 0f;
            style.ChildRounding = 0f;
            style.FrameRounding = 0f;
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.08f, 0.10f, 1f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = MeowPink;
            style.Colors[(int)ImGuiCol.Separator] = MeowPink;
            style.Colors[(int)ImGuiCol.PlotHistogram] = MeowPink;
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = MeowPink;
        }

        private static bool IsSteamRunning()
        {
            return Process.GetProcessesByName("steam").Length > 0;
        }

        private static void KillRecRoom()
        {
            foreach (var p in Process.GetProcessesByName("RecRoom")) { p.Kill(); p.WaitForExit(); }
            _status = "Rec Room closed.";
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                try
                {
                    File.Copy(file, targetFile, true);
                }
                catch { }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, targetSubDir);
            }
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }

        private static void UpdateGame()
        {
            if (Process.GetProcessesByName("RecRoom").Length > 0)
            {
                _status = "Error: Rec Room is running! Close it before updating.";
                _isError = true;
                return;
            }

            _isUpdating = true;
            _updateProgress = 0f;
            _status = "Downloading game files...";
            _isError = false;
            try
            {
                string url = "https://ahhhhhhhhhhhhhhhhhhh.b-cdn.net/Meow!Beta.7z";
                string tempPath = Path.Combine(Path.GetTempPath(), "MeowBeta.7z");

                using (var client = new HttpClient())
                {
                    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;

                        using (var streamToRead = response.Content.ReadAsStream())
                        using (var streamToWrite = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;
                            while ((bytesRead = streamToRead.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                streamToWrite.Write(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;
                                if (totalBytes.HasValue && totalBytes.Value > 0)
                                {
                                    _updateProgress = (float)totalBytesRead / totalBytes.Value;
                                }
                            }
                        }
                    }
                }

                _status = "Extracting game files...";
                _updateProgress = 1f;

                var psi = new ProcessStartInfo("tar", $"-xf \"{tempPath}\" -C \"{_gameDir}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                var process = Process.Start(psi);
                process?.WaitForExit();

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                foreach (var subDir in Directory.GetDirectories(_gameDir))
                {
                    if (File.Exists(Path.Combine(subDir, "RecRoom.exe")) || Directory.Exists(Path.Combine(subDir, "BepInEx")))
                    {
                        _status = "Merging extracted files...";
                        CopyDirectory(subDir, _gameDir);
                        SafeDeleteDirectory(subDir);
                        break;
                    }
                }

                string versionPath = Path.Combine(_gameDir, "version.txt");
                File.WriteAllText(versionPath, _remoteVersion);
                _localVersion = _remoteVersion;
                _needsUpdate = false;

                _hasPatch = CheckForWoofPatch();
                _status = "Game updated successfully!";
                _isError = false;
            }
            catch (Exception ex)
            {
                _status = $"Update failed: {ex.Message}";
                _isError = true;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private static void LaunchGame()
        {
            try 
            {
                bool isVr = Process.GetProcessesByName("vrserver").Length > 0 || Process.GetProcessesByName("OVRServer_x64").Length > 0;
                Process.Start(new ProcessStartInfo(Path.Combine(_gameDir, "RecRoom.exe")) { Arguments = $"+forcemode:{(isVr ? "vr" : "screen")}", WorkingDirectory = _gameDir });
                _status = "Launched! Stay pawsome.";
                _isError = false;
            } catch (Exception ex) { _status = $"Error: {ex.Message}"; _isError = true; }
        }
    }
}