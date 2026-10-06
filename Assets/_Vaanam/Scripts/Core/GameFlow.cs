using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Vaanam.Flight;
using Vaanam.Input;
using Vaanam.UI;
using Vaanam.World;

namespace Vaanam.Core
{
    public enum GameState { Welcome, RaiseHands, Fly, Playing, Paused, LevelComplete, Sunset }

    /// <summary>
    /// The story of one session, as a small state machine:
    ///
    ///   Welcome      "Stand in front of the camera"   (until someone is seen)
    ///   RaiseHands   "Raise your hands"               (bird's wings mirror the arms; hold 1 s = calibrated)
    ///   Fly          "Fly!"                            (the world starts moving)
    ///   Playing      rings, stars, obstacles, 2-3 minute levels
    ///   Paused       "Where did you go?"               (camera lost the child; world slows to a stop)
    ///   LevelComplete confetti, then the next sky
    ///   Sunset       the session is over; one real-world task; waits for a parent
    ///
    /// The first three states are the whole tutorial. They take about 30 seconds
    /// and they double as calibration.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [Header("Scene references (found automatically if empty)")]
        [SerializeField] private FlightController[] players;
        [SerializeField] private LevelGenerator level;
        [SerializeField] private SessionTimer timer;
        [SerializeField] private SkyController sky;
        [SerializeField] private FlightCamera flightCamera;
        [SerializeField] private VoiceCommands voice;
        [SerializeField] private MessageBoard board;
        [SerializeField] private HudController hud;
        [SerializeField] private SunsetScreen sunsetScreen;

        [Header("Tuning")]
        [Tooltip("How long the child must hold arms up and open before we say Fly!")]
        [SerializeField] private float raiseHoldSeconds = 1.0f;
        [Tooltip("Seconds without any tracked player before the world pauses.")]
        [SerializeField] private float lostPauseAfter = 2.5f;
        [SerializeField] private float worldRampSeconds = 2.0f;
        [SerializeField] private float celebrationSeconds = 5f;
        [SerializeField] private Vector3 startPosition = new Vector3(0f, 6f, 0f);

        public GameState State { get; private set; } = GameState.Welcome;
        public event Action<GameState> StateChanged;
        public float StateTime => Time.unscaledTime - _stateEntered;

        private float _stateEntered;
        private float _holdT;
        private float _lostT;
        private float _secondPlayerLostT;
        private float _speedTarget;
        private float _hintClock;
        private int _ringsSinceCheer;
        private bool _firstFlight = true;
        private GameSession _session;
        private PoseProvider _pose;
        private AudioDirector _audio;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            L10n.Current = (Language)SaveData.Current.Language;
        }

        private void Start()
        {
            // Find what was not wired in the Inspector, so the scene is forgiving too.
            if (players == null || players.Length == 0) players = FindObjectsByType<FlightController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(players, (a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            if (level == null) level = FindFirstObjectByType<LevelGenerator>();
            if (timer == null) timer = FindFirstObjectByType<SessionTimer>();
            if (sky == null) sky = FindFirstObjectByType<SkyController>();
            if (flightCamera == null) flightCamera = FindFirstObjectByType<FlightCamera>();
            if (voice == null) voice = FindFirstObjectByType<VoiceCommands>();
            if (board == null) board = FindFirstObjectByType<MessageBoard>();
            if (hud == null) hud = FindFirstObjectByType<HudController>();
            if (sunsetScreen == null) sunsetScreen = FindFirstObjectByType<SunsetScreen>();
            _session = GameSession.Instance ?? FindFirstObjectByType<GameSession>();
            _pose = PoseProvider.Instance;
            _audio = AudioDirector.Instance;

            for (int i = 0; i < players.Length; i++)
            {
                players[i].ResetTo(startPosition + Vector3.right * (i * 3f));
                players[i].Active = i == 0;
            }
            level?.SetPlayers(players);
            flightCamera?.SetPlayers(players);
            FlightController.GlobalSpeed = 0f;

            if (timer != null)
            {
                timer.OnWarning.AddListener(OnSessionWarning);
                timer.OnSessionEnd.AddListener(() => Enter(GameState.Sunset));
            }
            if (level != null) level.LevelFinished += _ => { if (State == GameState.Playing) Enter(GameState.LevelComplete); };
            if (voice != null) voice.OnCommand += OnVoice;
            if (_pose != null) { _pose.PlayerFound += OnPlayerFound; _pose.PlayerLost += OnPlayerLost; }
            if (_session != null) _session.Collected += OnCollected;

            Enter(GameState.Welcome);
        }

        // ------------------------------------------------------------------ states

        private void Enter(GameState s)
        {
            State = s;
            _stateEntered = Time.unscaledTime;
            switch (s)
            {
                case GameState.Welcome:
                    _speedTarget = 0f;
                    board?.Show("stand_here", "stand");
                    _audio?.Say("stand_here", important: true);
                    break;

                case GameState.RaiseHands:
                    _holdT = 0f;
                    _audio?.Play(AudioDirector.Sfx.Hello);
                    _audio?.Say("i_see_you", important: true);
                    board?.Show("raise_hands", "raise");
                    StartCoroutine(SayLater("raise_hands", 1.6f));
                    break;

                case GameState.Fly:
                    for (int i = 0; i < players.Length; i++) if (players[i].Active) _pose?.CalibrateFromCurrentPose(i);
                    board?.Show("fly", "bird", 2.5f);
                    _audio?.Say("fly", important: true);
                    _audio?.Play(AudioDirector.Sfx.Sparkle);
                    _speedTarget = 1f;
                    break;

                case GameState.Playing:
                    _speedTarget = 1f;
                    _lostT = 0f;
                    if (timer != null) timer.Running = true;
                    if (level != null && !level.Running && _session != null) level.StartLevel(_session.LevelIndex);
                    _audio?.SetMusicLevel(1f);
                    if (_firstFlight) { _firstFlight = false; _hintClock = 0f; }
                    break;

                case GameState.Paused:
                    _speedTarget = 0f;
                    board?.Show("where_are_you", "stand");
                    _audio?.Say("where_are_you", important: true);
                    _audio?.SetMusicLevel(0.4f);
                    break;

                case GameState.LevelComplete:
                    _speedTarget = 0.3f;
                    if (timer != null) timer.Running = false;    // celebrating is free time
                    _session?.LevelCompleted();
                    board?.Show("level_done", "star", celebrationSeconds - 1f);
                    _audio?.Play(AudioDirector.Sfx.Fanfare);
                    _audio?.Say("level_done", important: true);
                    StartCoroutine(Celebrate());
                    break;

                case GameState.Sunset:
                    _speedTarget = 0f;
                    level?.StopLevel();
                    if (timer != null) timer.Running = false;
                    sky?.SetSunset(1f);
                    _audio?.SetSunset(true);
                    board?.Hide();
                    _session?.Commit(timer != null ? timer.ElapsedMinutes : 0f);
                    sunsetScreen?.Show(_session != null ? _session.Stars : 0);
                    hud?.SetVisible(false);
                    break;
            }
            StateChanged?.Invoke(s);
        }

        private IEnumerator SayLater(string key, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _audio?.Say(key);
        }

        private IEnumerator Celebrate()
        {
            float end = Time.unscaledTime + celebrationSeconds;
            while (Time.unscaledTime < end && State == GameState.LevelComplete)
            {
                foreach (var p in players) if (p.Active) Effects.Confetti(p.transform.position + Vector3.up * 3f + Vector3.forward * 4f, 60);
                yield return new WaitForSecondsRealtime(0.7f);
            }
            if (State != GameState.LevelComplete) yield break;
            if (_session != null) _session.LevelIndex++;
            level?.StartLevel(_session != null ? _session.LevelIndex : 0);
            board?.Show(level != null && level.Current != null ? level.Current.nameKey : "next_sky", null, 2.5f);
            _audio?.Say("next_sky");
            Enter(GameState.Playing);
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            FlightController.GlobalSpeed = Mathf.MoveTowards(FlightController.GlobalSpeed, _speedTarget, dt / worldRampSeconds);

            int tracked = _pose != null ? _pose.TrackedCount : 0;
            bool camera = _pose != null && _pose.UsingCamera;

            switch (State)
            {
                case GameState.Welcome:
                    if (tracked > 0) Enter(GameState.RaiseHands);
                    break;

                case GameState.RaiseHands:
                {
                    if (tracked == 0) { if (StateTime > 4f && camera) Enter(GameState.Welcome); break; }
                    var p = _pose.Current;
                    bool posing = p.IsTracked && p.WingsOpen && p.Lift > 0.45f;
                    _holdT = posing ? _holdT + dt : Mathf.Max(0f, _holdT - dt * 2f);
                    if (posing && _holdT > 0.3f && _holdT < 0.35f) _audio?.Say("hold_it");
                    if (_holdT >= raiseHoldSeconds) Enter(GameState.Fly);
                    else if (StateTime > 12f && Mathf.Approximately(_holdT, 0f) && StateTime % 10f < dt) _audio?.Say("raise_hands");
                    break;
                }

                case GameState.Fly:
                    if (StateTime > 1.5f) Enter(GameState.Playing);
                    break;

                case GameState.Playing:
                    if (camera && tracked == 0) { _lostT += dt; if (_lostT > lostPauseAfter) Enter(GameState.Paused); }
                    else _lostT = 0f;
                    FirstFlightHints(dt);
                    SunsetBlend();
                    break;

                case GameState.Paused:
                    if (tracked > 0) { board?.Hide(); _audio?.Say("fly", important: true); Enter(GameState.Playing); }
                    break;

                case GameState.LevelComplete:
                    SunsetBlend();
                    break;

                case GameState.Sunset:
                    break;
            }

            SecondPlayerHousekeeping(dt);
            DebugKeys();
        }

        private void FirstFlightHints(float dt)
        {
            if (_session == null || _session.LevelIndex > 0) return;
            float before = _hintClock; _hintClock += dt;
            if (before < 7f && _hintClock >= 7f) { _audio?.Say("tilt_to_turn"); hud?.FlashIcon("tilt"); }
            if (before < 16f && _hintClock >= 16f) { _audio?.Say("flap_to_rise"); hud?.FlashIcon("raise"); }
        }

        private void SunsetBlend()
        {
            if (timer == null || !timer.InWarning || sky == null) return;
            // During the last minutes the sky slides from day (0) to golden (0.5).
            float warnSeconds = 120f;
            float k = 1f - Mathf.Clamp01(timer.Remaining / warnSeconds);
            sky.SetSunset(k * 0.5f);
        }

        private void OnSessionWarning()
        {
            _audio?.Say("almost_time", important: true);
            _audio?.SetSunset(true);
        }

        // ------------------------------------------------------------------ players

        private void OnPlayerFound(int index)
        {
            if (index == 0 || index >= players.Length) return;
            if (!players[index].Active)
            {
                var anchor = players[0].transform.position;
                players[index].Active = true;
                players[index].ResetTo(new Vector3(Mathf.Clamp(anchor.x + 4f, -8f, 8f), anchor.y, anchor.z));
                _audio?.Say("two_players", important: true);
                Effects.Confetti(players[index].transform.position + Vector3.forward * 3f, 80);
            }
            _secondPlayerLostT = 0f;
        }

        private void OnPlayerLost(int index) { }

        private void SecondPlayerHousekeeping(float dt)
        {
            if (players.Length < 2 || !players[1].Active || _pose == null) return;
            if (_pose.Players[1].IsTracked) { _secondPlayerLostT = 0f; return; }
            _secondPlayerLostT += dt;
            if (_secondPlayerLostT > 6f) players[1].Active = false;   // the friend left; tidy up quietly
        }

        private void OnCollected(string what, Vector3 at, int player)
        {
            if (what == "ring")
            {
                _ringsSinceCheer++;
                if (_ringsSinceCheer >= 3) { _ringsSinceCheer = 0; _audio?.Say(UnityEngine.Random.value < 0.5f ? "great" : "wow"); }
            }
        }

        // ------------------------------------------------------------------ voice & keys

        private void OnVoice(VoiceCommands.Command cmd)
        {
            switch (cmd)
            {
                case VoiceCommands.Command.Go:
                    if (State == GameState.Welcome || State == GameState.RaiseHands) Enter(GameState.Fly);
                    else if (State == GameState.Paused) Enter(GameState.Playing);
                    break;
                case VoiceCommands.Command.Jump:
                    foreach (var p in players) if (p.Active) p.ExternalJump();
                    break;
                case VoiceCommands.Command.Stop:
                    if (State == GameState.Playing) Enter(GameState.Paused);
                    break;
            }
        }

        private void DebugKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.nKey.wasPressedThisFrame && State == GameState.Playing) Enter(GameState.LevelComplete);   // skip level
            if (kb.f2Key.wasPressedThisFrame && State != GameState.Sunset) Enter(GameState.Sunset);           // test the ending
            if (kb.f3Key.wasPressedThisFrame) NewSession();
            if (kb.f12Key.wasPressedThisFrame)
                DebugScreenshot.Capture(System.IO.Path.Combine(Application.persistentDataPath, "screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png"));
        }

        /// <summary>Testing hook: jump straight to a state (used by the editor command queue and debug keys).</summary>
        public void ForceState(GameState state) => Enter(state);

        /// <summary>Testing hook: behave as if a voice command was heard.</summary>
        public void SimulateVoice(VoiceCommands.Command cmd) => OnVoice(cmd);

        /// <summary>Parent action: start over with a fresh timer.</summary>
        public void NewSession()
        {
            StopAllCoroutines();
            _session?.ResetSession();
            timer?.Restart();
            sky?.SetSunset(0f);
            _audio?.SetSunset(false);
            _audio?.SetMusicLevel(1f);
            sunsetScreen?.Hide();
            hud?.SetVisible(true);
            level?.StopLevel();
            _pose?.ResetCalibration();
            for (int i = 0; i < players.Length; i++)
            {
                players[i].Active = i == 0;
                players[i].ResetTo(startPosition + Vector3.forward * players[0].transform.position.z + Vector3.right * (i * 3f));
            }
            _firstFlight = true;
            Enter(GameState.Welcome);
        }
    }
}
