using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vaanam.Editor
{
    /// <summary>
    /// Lets tools outside Unity ask the OPEN editor to do things, by dropping a
    /// small file into Temp/vaanam-commands/. Unity refuses to run two editors on
    /// one project, so this is how a script (or an AI assistant in a terminal)
    /// builds the scene or a player without you clicking menus.
    ///
    ///   echo build-scene  > Temp/vaanam-commands/1.cmd
    ///   echo build-player > Temp/vaanam-commands/2.cmd
    ///   echo play         > Temp/vaanam-commands/3.cmd
    ///
    /// Results are written next to the command as .done or .err. Harmless to
    /// leave in the project; it only ever reads the Temp folder.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorCommandQueue
    {
        private static readonly string Dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "vaanam-commands");
        private static double _nextPoll;

        static EditorCommandQueue()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!Directory.Exists(Dir)) return;

            foreach (string file in Directory.GetFiles(Dir, "*.cmd"))
            {
                string command = File.ReadAllText(file).Trim();
                string result = Path.ChangeExtension(file, ".done");
                try
                {
                    File.Delete(file);
                    Debug.Log("[Vaanam] Command: " + command);
                    string output = Run(command);
                    File.WriteAllText(result, output ?? "ok");
                }
                catch (Exception e)
                {
                    Debug.LogError("[Vaanam] Command failed: " + command + "\n" + e);
                    File.WriteAllText(Path.ChangeExtension(file, ".err"), e.ToString());
                }
                break;   // one command per poll so the editor can settle between them
            }
        }

        private static string Run(string command)
        {
            switch (command)
            {
                case "build-scene": return VaanamSceneBuilder.BuildMainScene();
                case "build-player": return VaanamBuild.BuildWindows();
                case "play": EditorApplication.EnterPlaymode(); return "entering play mode";
                case "stop": EditorApplication.ExitPlaymode(); return "exiting play mode";
                case "refresh": AssetDatabase.Refresh(); return "refreshed";
                case "screenshot":
                {
                    // Game view capture; only meaningful in play mode.
                    string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "vaanam-screenshot-" + DateTime.Now.ToString("HHmmss") + ".png");
                    if (!EditorApplication.isPlaying) throw new InvalidOperationException("screenshot needs play mode");
                    EditorApplication.ExecuteMenuItem("Window/General/Game");   // make sure the game is being rendered
                    Vaanam.Core.DebugScreenshot.Capture(path);
                    return "capturing " + path;
                }
                case "ping": return "pong " + Application.unityVersion;
                default:
                    if (command.StartsWith("state:"))
                    {
                        // e.g. state:Fly, state:Sunset. Play mode only.
                        var flow = Vaanam.Core.GameFlow.Instance;
                        if (flow == null) throw new InvalidOperationException("GameFlow not running (enter play mode first)");
                        var state = (Vaanam.Core.GameState)Enum.Parse(typeof(Vaanam.Core.GameState), command.Substring(6), true);
                        flow.ForceState(state);
                        return "state -> " + state;
                    }
                    if (command.StartsWith("voice:"))
                    {
                        var flow = Vaanam.Core.GameFlow.Instance;
                        if (flow == null) throw new InvalidOperationException("GameFlow not running (enter play mode first)");
                        var cmd = (Vaanam.Input.VoiceCommands.Command)Enum.Parse(typeof(Vaanam.Input.VoiceCommands.Command), command.Substring(6), true);
                        flow.SimulateVoice(cmd);
                        return "voice -> " + cmd;
                    }
                    throw new ArgumentException("Unknown command: " + command);
            }
        }
    }
}
