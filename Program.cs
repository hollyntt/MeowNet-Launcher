using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

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
        private static float _animTime = 0f;
        private static readonly Vector4 MeowPink = new Vector4(1.00f, 0.27f, 0.43f, 1f);

        private static unsafe void LoadEmbeddedIcon()
        {
            var assembly = Assembly.GetExecutingAssembly();
    
            // We get the list of resources to see exactly what the name is in the compiled EXE
            string[] names = assembly.GetManifestResourceNames();
            string? resourceName = names.FirstOrDefault(n => n.EndsWith("meow.png"));

            if (resourceName == null) return;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return;

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                fixed (byte* pData = data)
                {
                    byte[] extBytes = System.Text.Encoding.ASCII.GetBytes(".png\0");
                    fixed (byte* pExt = extBytes)
                    {
                        Image img = Raylib.LoadImageFromMemory((sbyte*)pExt, pData, data.Length);
                        if (img.Data != (void*)IntPtr.Zero)
                        {
                            Raylib.SetWindowIcon(img);
                            Raylib.UnloadImage(img);
                        }
                    }
                }
            }
        }
        
        static void Main()
        {
            HideConsoleInRelease();
            
            FindGameDirectory();
            _hasPatch = CheckForWoofPatch();

            Raylib.InitWindow(720, 520, "");
            LoadEmbeddedIcon();
            Raylib.SetTargetFPS(60);
            rlImGui.Setup(true);

            ApplyMeowStyle();

            while (!Raylib.WindowShouldClose())
            {
                _animTime += Raylib.GetFrameTime();
                
                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color(26, 26, 36, 255));
                rlImGui.Begin();
                
                DrawMeowUI();
                
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

        private static void DrawMeowUI()
        {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
            
            bool isRunning = Process.GetProcessesByName("RecRoom").Length > 0;
            UpdateTitleBarStyle(isRunning);

            ImGui.Begin("MeowNet Launcher", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            ImGui.TextDisabled("Game Path:");
            ImGui.TextWrapped(_gameDir);
    
            ImGui.Spacing();
            ImGui.TextDisabled("WoofPatch Status:");
            ImGui.SameLine(130);
            ImGui.TextColored(_hasPatch ? new Vector4(0.5f, 1f, 0.5f, 1f) : MeowPink, 
                _hasPatch ? "Patch Detected" : "WoofPatch Missing!");

            ImGui.Dummy(new Vector2(0, 20));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.10f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, MeowPink);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.00f, 0.20f, 0.35f, 1f));

            if (ImGui.Button("Launch MeowNet", new Vector2(ImGui.GetContentRegionAvail().X, 50)))
            {
                if (_hasPatch) LaunchGame();
                else { _status = "Error: WoofPatch not found!"; _isError = true; }
            }
            
            ImGui.PopStyleColor(3);

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
            
            // Set to 0 for sharp, non-rounded edges
            style.WindowRounding = 0f;
            style.ChildRounding = 0f;
            style.FrameRounding = 0f;
            style.GrabRounding = 0f;
            style.TabRounding = 0f;

            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.08f, 0.10f, 1f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = MeowPink;
            style.Colors[(int)ImGuiCol.Separator] = MeowPink;
        }

        private static void LaunchGame()
        {
            try 
            {
                bool isVrRunning = Process.GetProcessesByName("vrserver").Length > 0 || 
                                   Process.GetProcessesByName("OVRServer_x64").Length > 0;
                
                string mode = isVrRunning ? "vr" : "screen";
                
                Process.Start(new ProcessStartInfo(Path.Combine(_gameDir, "RecRoom.exe")) 
                { 
                    Arguments = $"+forcemode:{mode}", 
                    WorkingDirectory = _gameDir 
                });
                _status = $"Launched in {mode.ToUpper()}! Stay pawsome.";
                _isError = false;
            } 
            catch (Exception ex) 
            {
                _status = $"Launch error: {ex.Message}";
                _isError = true;
            }
        }
    }
}