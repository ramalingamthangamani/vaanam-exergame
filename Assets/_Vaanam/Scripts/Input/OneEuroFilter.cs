using UnityEngine;

namespace Vaanam.Input
{
    /// <summary>
    /// One Euro Filter (Casiez et al. 2012). Removes jitter from noisy tracking
    /// data while keeping fast movements responsive.
    ///
    /// Tuning:
    ///   minCutoff  lower  = smoother but laggier when still (try 1.0)
    ///   beta       higher = less lag during fast motion (try 0.01 to 0.1)
    /// </summary>
    public class OneEuroFilter
    {
        private readonly float _minCutoff;
        private readonly float _beta;
        private readonly float _dCutoff;

        private bool _hasPrev;
        private float _prevValue;
        private float _prevDerivative;

        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.05f, float dCutoff = 1.0f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dCutoff = dCutoff;
        }

        public void Reset() => _hasPrev = false;

        public float Filter(float value, float deltaTime)
        {
            if (deltaTime <= 0f) deltaTime = 1f / 60f;

            if (!_hasPrev)
            {
                _hasPrev = true;
                _prevValue = value;
                _prevDerivative = 0f;
                return value;
            }

            float derivative = (value - _prevValue) / deltaTime;
            float dAlpha = Alpha(_dCutoff, deltaTime);
            float smoothedDerivative = Mathf.Lerp(_prevDerivative, derivative, dAlpha);

            float cutoff = _minCutoff + _beta * Mathf.Abs(smoothedDerivative);
            float alpha = Alpha(cutoff, deltaTime);
            float result = Mathf.Lerp(_prevValue, value, alpha);

            _prevValue = result;
            _prevDerivative = smoothedDerivative;
            return result;
        }

        private static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}
