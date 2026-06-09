using System.Diagnostics;
using System.Numerics;
using System.IO;
using ImGuiNET;
using Raylib_cs;
using RecRoomLauncher;
using rlImGui_cs;
using Valve.VR; // ✔ HUDCenter provides this namespace

namespace MeowNet_Launcher
{
    class Program
    {
        private static string gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\RecRoom - Meow";
        private static string revivalExe = "RecRoom.exe";

        private static Vector4 meowPink = new Vector4(1.00f, 0.27f, 0.43f, 1f);

        private static string statusMessage = "";
        private static bool statusIsError = false;

        private static bool vrDetected = false;

        // Animation timer
        private static float animTime = 0f;

        // Dashboard guard
        private static bool dashboardOpened = false;

        static void Main()
        {
            DetectVR();
            CheckIntegrity();

#if RELEASE
            unsafe { Raylib.SetTraceLogCallback(&NativeMethods.RaylibLogCallback); }
            Raylib.SetTraceLogLevel(TraceLogLevel.None);
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);

#if WINDOWS_BUILD
            NativeMethods.FreeConsole();
#endif
#endif

            Raylib.InitWindow(720, 520, "MeowNet Launcher");
            Raylib.SetTargetFPS(60);
            rlImGui.Setup(true);

            SetupStyle();

            while (true)
            {
                if (Raylib.WindowShouldClose())
                    break;

                // Auto-open SteamVR dashboard logic
                if (vrDetected)
                    TryOpenSteamVRDashboard();

                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color(26, 26, 36, 255));

                rlImGui.Begin();
                DrawUI();
                rlImGui.End();

                Raylib.EndDrawing();
            }

            rlImGui.Shutdown();
            Raylib.CloseWindow();
        }

        // Detect if Rec Room is running
        private static bool IsRecRoomRunning()
        {
            return Process.GetProcessesByName("RecRoom").Length > 0 ||
                   Process.GetProcessesByName("RecRoom.exe").Length > 0;
        }

        // Auto-open SteamVR dashboard (after SteamVR loads, only if Rec Room not running)
        private static void TryOpenSteamVRDashboard()
        {
            if (dashboardOpened)
                return;

            // Rec Room must NOT be running
            if (IsRecRoomRunning())
                return;

            // SteamVR must be fully running
            bool steamvrRunning =
                Process.GetProcessesByName("vrserver").Length > 0 &&
                Process.GetProcessesByName("vrcompositor").Length > 0;

            if (!steamvrRunning)
                return;

            // Initialize OpenVR
            EVRInitError err = EVRInitError.None;
            OpenVR.Init(ref err, EVRApplicationType.VRApplication_Utility);

            if (err != EVRInitError.None)
                return;

            // Open dashboard silently
            OpenVR.Applications.LaunchDashboardOverlay("system.dashboard");

            dashboardOpened = true;
        }

        // Main UI (ImGui title bar shows status + animated color)
        private static void DrawUI()
        {
            int screenW = Raylib.GetScreenWidth();
            int screenH = Raylib.GetScreenHeight();

            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize(new Vector2(screenW, screenH));

            // STATUS
            bool running = IsRecRoomRunning();
            string rrStatus = running ? "Running" : "Not Running";

            // ANIMATION
            animTime += Raylib.GetFrameTime();
            float pulse = (float)(0.5f + 0.5f * Math.Sin(animTime * 3));

            // TITLE BAR COLOR OVERRIDE
            var colors = ImGui.GetStyle().Colors;

            if (running)
            {
                // Pulse between dark green and bright green
                colors[(int)ImGuiCol.TitleBg] = new Vector4(
                    0.10f + pulse * 0.20f,
                    0.40f + pulse * 0.20f,
                    0.10f,
                    1f
                );

                colors[(int)ImGuiCol.TitleBgActive] = new Vector4(
                    0.15f + pulse * 0.25f,
                    0.55f + pulse * 0.25f,
                    0.15f,
                    1f
                );
            }
            else
            {
                // Static red when not running
                colors[(int)ImGuiCol.TitleBg] = new Vector4(0.40f, 0.10f, 0.10f, 1f);
                colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.55f, 0.15f, 0.15f, 1f);
            }

            // Dynamic ImGui title bar text
            ImGui.Begin($"MeowNet Launcher - Rec Room: {rrStatus}",
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse);

            ImGui.TextDisabled("Game folder:");
            ImGui.SameLine(110);
            ImGui.TextWrapped(gameDir);

            ImGui.Spacing();
            ImGui.TextDisabled("VR Status:");
            ImGui.SameLine(110);
            ImGui.Text(vrDetected ? "VR Detected" : "No VR");

            ImGui.Dummy(new Vector2(0, 14));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 10));

            if (ImGui.Button("Launch Game", new Vector2(screenW - 40, 50)))
            {
                if (vrDetected)
                {
                    EnsureSteamVR();
                    TryLaunch("vr");
                }
                else
                {
                    TryLaunch("screen");
                }
            }

            // EXIT BUTTON REMOVED ✔

            if (!string.IsNullOrEmpty(statusMessage))
            {
                ImGui.Dummy(new Vector2(0, 8));
                var msgColor = statusIsError ? meowPink : new Vector4(0.55f, 1f, 0.55f, 1f);
                ImGui.TextColored(msgColor, statusMessage);
            }

            ImGui.End();
        }

        // Style
        private static void SetupStyle()
        {
            ImGui.StyleColorsDark();
            var style = ImGui.GetStyle();
            style.WindowRounding = 6f;
            style.FrameRounding = 4f;
            style.GrabRounding = 4f;
            style.ItemSpacing = new Vector2(10, 8);
            style.FramePadding = new Vector2(10, 6);
            style.WindowPadding = new Vector2(20, 20);

            var colors = style.Colors;
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.08f, 0.10f, 1f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.12f, 0.12f, 0.16f, 1f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.10f, 0.15f, 1f);
            colors[(int)ImGuiCol.ButtonHovered] = meowPink;
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(1.00f, 0.20f, 0.35f, 1f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.10f, 0.15f, 1f);
            colors[(int)ImGuiCol.HeaderHovered] = meowPink;
            colors[(int)ImGuiCol.Separator] = meowPink;
        }

        // VR Autodetect
        private static void DetectVR()
        {
            if (Process.GetProcessesByName("vrserver").Length > 0 ||
                Process.GetProcessesByName("vrcompositor").Length > 0 ||
                Process.GetProcessesByName("OVRServer_x64").Length > 0 ||
                Process.GetProcessesByName("MixedRealityPortal").Length > 0)
            {
                vrDetected = true;
            }
        }

        // Integrity Check
        private static void CheckIntegrity()
        {
            string exePath = Path.Combine(gameDir, revivalExe);

            if (!File.Exists(exePath))
            {
                statusMessage = "[ERROR] RecRoom.exe missing — MeowNet install may be broken.";
                statusIsError = true;
            }
        }

        // SteamVR Auto‑Start
        private static void EnsureSteamVR()
        {
            if (!vrDetected)
                return;

            if (Process.GetProcessesByName("vrserver").Length == 0)
            {
                try
                {
                    Process.Start("steam://rungameid/250820");
                }
                catch
                {
                    statusMessage = "Failed to auto‑start SteamVR.";
                    statusIsError = true;
                }
            }
        }

        // Launch Logic
        private static void TryLaunch(string mode)
        {
            string exePath = Path.Combine(gameDir, revivalExe);

            if (!File.Exists(exePath))
            {
                statusMessage = "[ERROR] RecRoom.exe missing.";
                statusIsError = true;
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"+forcemode:{mode}",
                    UseShellExecute = true,
                    WorkingDirectory = gameDir
                };
                Process.Start(psi);
                statusMessage = $"Launched MeowNet in {mode.ToUpper()} mode.";
                statusIsError = false;
            }
            catch (Exception ex)
            {
                statusMessage = $"Launch failed: {ex.Message}";
                statusIsError = true;
            }
        }
    }
}
