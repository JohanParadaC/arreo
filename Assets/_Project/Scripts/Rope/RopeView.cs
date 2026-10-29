using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>
    /// Draws the rope from a <see cref="RopeSnapshot"/> only: the two ends, the tension and the
    /// state. It never touches the physics segments, so a client can draw a rope simulated on the host.
    /// </summary>
    public sealed class RopeView : MonoBehaviour
    {
        private const int CurvePoints = 24;
        private const int LoopPoints = 20;

        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private LineRenderer _line;
        [SerializeField] private LineRenderer _loop;
        [Tooltip("Sag of a slack rope as a fraction of its length.")]
        [SerializeField, Range(0f, 0.5f)] private float _slackSag = 0.12f;
        [Tooltip("Tension (fraction of the break limit) at which the rope is drawn straight.")]
        [SerializeField, Range(0.01f, 1f)] private float _straightAt = 0.15f;
        [SerializeField] private Color _ropeColor = new Color(0.85f, 0.75f, 0.55f);
        [SerializeField] private Color _strainColor = new Color(0.9f, 0.1f, 0.05f);

        private readonly Vector3[] _curve = new Vector3[CurvePoints];
        private readonly Vector3[] _ring = new Vector3[LoopPoints];

        private void LateUpdate()
        {
            // Nothing to draw until the rope is tied to a horse (never on clients this week).
            if (!_rope.IsConfigured)
            {
                _line.enabled = false;
                _loop.enabled = false;
                return;
            }

            RopeSnapshot snapshot = _rope.Snapshot;
            switch (snapshot.State)
            {
                case RopeState.Coiled:
                    _line.enabled = false;
                    DrawLoop(snapshot.Start + Vector3.right * 0.35f, 0.25f, _ropeColor);
                    break;
                case RopeState.Broken:
                    // A short torn end dangling from the saddle.
                    DrawCurve(snapshot.Start, snapshot.Start + new Vector3(0f, -1.2f, -0.4f), 0.2f, _strainColor);
                    _loop.enabled = false;
                    break;
                default:
                    float sag = snapshot.State == RopeState.Throwing ? 0.03f
                        : _slackSag * (1f - Mathf.Clamp01(snapshot.Tension01 / _straightAt));
                    Color color = Color.Lerp(_ropeColor, _strainColor, Mathf.InverseLerp(0.5f, 1f, snapshot.Tension01));
                    DrawCurve(snapshot.Start, snapshot.End, sag, color);
                    bool showLoop = snapshot.State != RopeState.Released;
                    _loop.enabled = showLoop;
                    if (showLoop)
                        DrawLoop(snapshot.End, snapshot.State == RopeState.Throwing ? 0.7f : 0.35f, color);
                    break;
            }
        }

        private void DrawCurve(Vector3 start, Vector3 end, float sagFraction, Color color)
        {
            Vector3 control = (start + end) / 2f + Vector3.down * (Vector3.Distance(start, end) * sagFraction * 2f);
            for (int i = 0; i < CurvePoints; i++)
            {
                float t = i / (CurvePoints - 1f);
                _curve[i] = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
            }
            _line.enabled = true;
            _line.positionCount = CurvePoints;
            _line.SetPositions(_curve);
            _line.startColor = _line.endColor = color;
        }

        private void DrawLoop(Vector3 center, float radius, Color color)
        {
            for (int i = 0; i < LoopPoints; i++)
            {
                float angle = i / (float)LoopPoints * Mathf.PI * 2f;
                _ring[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            _loop.enabled = true;
            _loop.loop = true;
            _loop.positionCount = LoopPoints;
            _loop.SetPositions(_ring);
            _loop.startColor = _loop.endColor = color;
        }
    }
}
