using System;
using System.Collections.Generic;
using UnityEngine;
using Vaanam.Flight;

namespace Vaanam.World
{
    /// <summary>
    /// Builds the world ahead of the birds and tidies it up behind them.
    ///
    /// The corridor runs along +Z forever. Every <see cref="segmentLength"/>
    /// metres we spawn one "segment": scenery on both sides always, and rings,
    /// stars and obstacles only while a level is running. Ground tiles leapfrog
    /// forward so the floor never ends. A level finishes after its duration.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [SerializeField] private LevelDefinition[] levels;
        [SerializeField] private SkyController sky;
        [SerializeField] private float segmentLength = 30f;
        [SerializeField] private float spawnAhead = 260f;
        [SerializeField] private float despawnBehind = 40f;
        [SerializeField] private float corridorHalfWidth = 10f;
        [SerializeField] private float groundTileSize = 240f;

        public LevelDefinition Current { get; private set; }
        public int LevelIndex { get; private set; }
        public int LevelCount => levels != null ? levels.Length : 0;
        public bool Running { get; private set; }
        public float Progress01 => Current != null && Current.durationSeconds > 0f ? Mathf.Clamp01(_elapsed / Current.durationSeconds) : 0f;
        public float CorridorHalfWidth => corridorHalfWidth;

        public event Action<LevelDefinition> LevelStarted;
        public event Action<LevelDefinition> LevelFinished;

        private FlightController[] _players;
        private readonly List<Transform> _spawned = new List<Transform>();
        private readonly List<Transform> _ground = new List<Transform>();
        private Material _groundMat, _groundDarkMat;
        private Transform _root;
        private float _nextSegmentZ;
        private float _elapsed;
        private System.Random _rnd = new System.Random(1);
        private int _segmentIndex;
        private bool _finishedFired;

        public void SetPlayers(FlightController[] players) => _players = players;

        private void Awake()
        {
            _root = new GameObject("World").transform;
            var palette = VaanamPalette.Get();
            _groundMat = new Material(palette.Ground);
            _groundDarkMat = new Material(palette.GroundDark);
            for (int i = 0; i < 3; i++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
                tile.name = "Ground" + i;
                Destroy(tile.GetComponent<Collider>());
                tile.SetParent(_root, false);
                tile.localScale = new Vector3(groundTileSize / 10f, 1f, groundTileSize / 10f);
                tile.position = new Vector3(0f, 0f, i * groundTileSize - groundTileSize);
                tile.GetComponent<Renderer>().sharedMaterial = _groundMat;
                _ground.Add(tile);
            }
            if (sky == null) sky = FindFirstObjectByType<SkyController>();
        }

        private void Start()
        {
            _nextSegmentZ = -segmentLength;
            if (levels != null && levels.Length > 0) ApplyLook(levels[0]);
            Fill();
        }

        /// <summary>Start (or restart) level <paramref name="index"/>. Wraps around, getting a bit faster each lap.</summary>
        public void StartLevel(int index)
        {
            if (levels == null || levels.Length == 0) { Debug.LogWarning("[Vaanam] No levels assigned to LevelGenerator"); return; }
            LevelIndex = index;
            Current = levels[index % levels.Length];
            int lap = index / levels.Length;
            float speed = Current.forwardSpeed + lap * 1.5f;
            _elapsed = 0f;
            Running = true;
            _finishedFired = false;
            _rnd = new System.Random(index * 7919 + 13);
            ApplyLook(Current);
            if (_players != null) foreach (var p in _players) if (p != null) p.SetForwardSpeed(speed);
            LevelStarted?.Invoke(Current);
        }

        public void StopLevel()
        {
            Running = false;
        }

        private void ApplyLook(LevelDefinition def)
        {
            if (sky != null) sky.ApplyLevel(def);
            Color g = VaanamPalette.DefaultColors["Ground"] * def.groundTint;
            Color gd = VaanamPalette.DefaultColors["GroundDark"] * def.groundTint;
            _groundMat.color = g; if (_groundMat.HasProperty("_BaseColor")) _groundMat.SetColor("_BaseColor", g);
            _groundDarkMat.color = gd; if (_groundDarkMat.HasProperty("_BaseColor")) _groundDarkMat.SetColor("_BaseColor", gd);
        }

        private float FrontZ()
        {
            float z = 0f; bool any = false;
            if (_players != null)
                foreach (var p in _players)
                    if (p != null && p.Active) { z = any ? Mathf.Max(z, p.transform.position.z) : p.transform.position.z; any = true; }
            return z;
        }

        private void Update()
        {
            if (Running)
            {
                _elapsed += Time.deltaTime;
                if (_elapsed >= Current.durationSeconds && !_finishedFired)
                {
                    _finishedFired = true;
                    Running = false;
                    LevelFinished?.Invoke(Current);
                }
            }
            Fill();
        }

        private void Fill()
        {
            float front = FrontZ();
            while (_nextSegmentZ < front + spawnAhead)
            {
                SpawnSegment(_nextSegmentZ);
                _nextSegmentZ += segmentLength;
            }

            // Despawn behind.
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var t = _spawned[i];
                if (t == null) { _spawned.RemoveAt(i); continue; }
                if (t.position.z < front - despawnBehind) { Destroy(t.gameObject); _spawned.RemoveAt(i); }
            }

            // Leapfrog ground tiles.
            foreach (var tile in _ground)
            {
                if (tile.position.z + groundTileSize / 2f < front - despawnBehind)
                {
                    float maxZ = float.MinValue;
                    foreach (var o in _ground) maxZ = Mathf.Max(maxZ, o.position.z);
                    tile.position = new Vector3(0f, 0f, maxZ + groundTileSize);
                }
            }
        }

        private void SpawnSegment(float z)
        {
            _segmentIndex++;
            var palette = VaanamPalette.Get();
            var seg = new GameObject("Segment " + Mathf.RoundToInt(z)).transform;
            seg.SetParent(_root, false);
            seg.position = new Vector3(0f, 0f, z);
            _spawned.Add(seg);

            // Corridor marker: a darker stripe across the path, so speed is visible.
            MeshBuilders.Prim(PrimitiveType.Cube, "Stripe", seg, new Vector3(0f, 0.02f, 0f), new Vector3(corridorHalfWidth * 2f + 2f, 0.04f, 1.5f), _groundDarkMat);

            SceneryTheme theme = Current != null ? Current.theme : (levels != null && levels.Length > 0 ? levels[0].theme : SceneryTheme.Fields);
            Scenery.Populate(seg, z, segmentLength, theme, corridorHalfWidth, _rnd);

            if (!Running || Current == null) return;
            // The first two segments of a level stay empty so the child can settle.
            if (_elapsed < 2f && _segmentIndex % 2 == 0) return;

            float hw = corridorHalfWidth - 3f;
            float R() => (float)_rnd.NextDouble();

            // Ring with a trail of stars leading into it.
            if (R() < Current.ringChance)
            {
                float x = Mathf.Lerp(-hw, hw, R());
                float y = Mathf.Lerp(4.5f, 12f, R());
                Ring.Create(seg, new Vector3(x, y, z + segmentLength * 0.75f), Current.kolamRings);
                if (R() < Current.starChance)
                    for (int i = 0; i < Current.starsPerCluster; i++)
                        Star.Create(seg, new Vector3(x, y, z + segmentLength * 0.75f - 6f - i * 4.5f), i);
            }
            else if (R() < Current.starChance)
            {
                // A gentle arc of stars.
                float x = Mathf.Lerp(-hw, hw, R());
                float y = Mathf.Lerp(3.5f, 11f, R());
                int n = Mathf.Max(3, Current.starsPerCluster + 2);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    Star.Create(seg, new Vector3(x + Mathf.Sin(t * Mathf.PI) * 3f, y + Mathf.Sin(t * Mathf.PI) * 2f, z + 4f + t * (segmentLength - 8f)), i);
                }
            }

            // At most one obstacle per segment, placed in the first part so it never overlaps the ring.
            float obstacleZ = z + segmentLength * 0.3f;
            if (R() < Current.jumpWallChance)
                Obstacle.Create(Obstacle.Kind.JumpWall, seg, new Vector3(0f, Mathf.Lerp(4f, 6f, R()), obstacleZ), corridorHalfWidth, _players);
            else if (R() < Current.duckBarChance)
                Obstacle.Create(Obstacle.Kind.DuckBar, seg, new Vector3(0f, Mathf.Lerp(6f, 10f, R()), obstacleZ), corridorHalfWidth, _players);
            else if (R() < Current.cloudChance)
                Obstacle.Create(Obstacle.Kind.Cloud, seg, new Vector3(Mathf.Lerp(-hw, hw, R()), Mathf.Lerp(3f, 12f, R()), obstacleZ), corridorHalfWidth, _players);

            if (R() < Current.kiteChance)
                Scenery.Kite(seg, new Vector3((R() < 0.5f ? -1f : 1f) * (corridorHalfWidth + 4f + R() * 10f), 8f + R() * 10f, z + R() * segmentLength), _rnd.Next(3), palette);
        }
    }
}
