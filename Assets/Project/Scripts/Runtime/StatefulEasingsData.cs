using UnityEngine;

namespace EasyStateful.Runtime {
    [CreateAssetMenu(fileName = "StatefulEasingsData", menuName = "Stateful UI/Easings Data", order = 10)]
    public class StatefulEasingsData : ScriptableObject
    {
        public AnimationCurve[] curves = new AnimationCurve[System.Enum.GetValues(typeof(Ease)).Length];

        private const float PI = Mathf.PI;
        private const float BIG_TANGENT = 100f; // Used for "infinite" or very large derivatives

        // Constants for Elastic Easing
        private const float ELASTIC_C4 = (2f * Mathf.PI) / 3f;
        private const float ELASTIC_C5 = (2f * Mathf.PI) / 4.5f;

        // Constants for Back Easing
        private const float BACK_C1 = 1.0f;
        private const float BACK_C3 = BACK_C1 + 1f;
        private const float BACK_C2 = BACK_C1 * 1.525f;
        private const float BACK_C4 = BACK_C2 + 1f;

        // Constants for Bounce Easing
        private const float BOUNCE_N1 = 7.5625f;
        private const float BOUNCE_D1 = 2.75f;

        private void OnValidate()
        {
            if (curves == null || curves.Length != System.Enum.GetValues(typeof(Ease)).Length)
            {
                var old = curves;
                curves = new AnimationCurve[System.Enum.GetValues(typeof(Ease)).Length];
                if (old != null)
                {
                    for (int i = 0; i < Mathf.Min(old.Length, curves.Length); i++)
                        curves[i] = old[i];
                }
            }
        }

        public void ResetToDefault()
        {
            for (int i = 0; i < curves.Length; i++)
            {
                curves[i] = GetDefaultCurve((Ease)i);
            }
        }

        public void InitializeWithDefaults()
        {
            ResetToDefault();
        }

        // Helper for creating keyframes
        // For smooth curves, inDerivFunc and outDerivFunc will be the same.
        // For curves with corners (like Bounce), they can be different.
        private static Keyframe[] CreateKeyframes(
            System.Func<float, float> valFunc, 
            System.Func<float, float> inDerivFunc, 
            System.Func<float, float> outDerivFunc, 
            float[] times, 
            bool forceFlatStart = false, 
            bool forceFlatEnd = false    
        )
        {
            Keyframe[] keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; ++i)
            {
                float t = times[i];
                float val = valFunc(t);
                float inT = inDerivFunc(t);
                float outT = outDerivFunc(t);
                
                float inW = Mathf.Approximately(inT, 0f) ? 0f : 1/3f;
                float outW = Mathf.Approximately(outT, 0f) ? 0f : 1/3f;
                
                keys[i] = new Keyframe(t, val, inT, outT, inW, outW);
                keys[i].weightedMode = WeightedMode.Both; 
            }

            if (keys.Length > 0) {
                if (forceFlatStart) {
                    keys[0].inTangent = 0f; keys[0].outTangent = 0f;
                    keys[0].inWeight = 0f; keys[0].outWeight = 0f;
                }
                if (forceFlatEnd && keys.Length > 1) {
                    keys[keys.Length-1].inTangent = 0f; keys[keys.Length-1].outTangent = 0f;
                    keys[keys.Length-1].inWeight = 0f; keys[keys.Length-1].outWeight = 0f;
                }
            }
            return keys;
        }

        // InElastic easing functions
        private static float CalculateInElastic_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            return -Mathf.Pow(2f, 10f * x - 10f) * Mathf.Sin((x * 10f - 10.75f) * ELASTIC_C4);
        }

        private static float CalculateInElastic_Deriv(float x) {
            if (x == 0f) return 0f; 
            
            float exp_val = Mathf.Pow(2f, 10f * x - 10f);
            float sin_arg = (x * 10f - 10.75f) * ELASTIC_C4;
            float sin_component = Mathf.Sin(sin_arg);
            float cos_component = Mathf.Cos(sin_arg);

            float deriv_val = -exp_val * (10f * Mathf.Log(2f) * sin_component + 10f * ELASTIC_C4 * cos_component);
            return Mathf.Clamp(deriv_val, -BIG_TANGENT, BIG_TANGENT);
        }

        // OutElastic easing functions
        private static float CalculateOutElastic_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * ELASTIC_C4) + 1f;
        }

        private static float CalculateOutElastic_Deriv(float x) {
            if (x == 1f) return 0f; 

            float exp_val = Mathf.Pow(2f, -10f * x);
            float sin_arg = (x * 10f - 0.75f) * ELASTIC_C4;
            float sin_component = Mathf.Sin(sin_arg);
            float cos_component = Mathf.Cos(sin_arg);

            float deriv_A = -10f * Mathf.Log(2f) * exp_val;
            float deriv_B = 10f * ELASTIC_C4 * cos_component;
            
            float deriv_val = deriv_A * sin_component + exp_val * deriv_B;
            return Mathf.Clamp(deriv_val, -BIG_TANGENT, BIG_TANGENT);
        }
        
        // InOutElastic easing functions
        private static float CalculateInOutElastic_Val_Internal(float x) {
            float sin_arg_val = (20f * x - 11.125f) * ELASTIC_C5;
            float sin_component = Mathf.Sin(sin_arg_val);

            if (x < 0.5f) {
                return -0.5f * Mathf.Pow(2f, 20f * x - 10f) * sin_component;
            } else { 
                return 0.5f * Mathf.Pow(2f, -20f * x + 10f) * sin_component + 1f;
            }
        }
        private static float CalculateInOutElastic_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            return CalculateInOutElastic_Val_Internal(x);
        }

        private static float CalculateInOutElastic_Deriv_Internal(float x) {
            float sin_arg_val = (20f * x - 11.125f) * ELASTIC_C5;
            float sin_component = Mathf.Sin(sin_arg_val);
            float cos_component = Mathf.Cos(sin_arg_val);
            float deriv_val;

            if (Mathf.Approximately(x, 0.5f)) {
                return 10f * Mathf.Log(2f); // Approx 6.93
            } else if (x < 0.5f) {
                float exp_val = Mathf.Pow(2f, 20f * x - 10f);
                float deriv_A_factor = -10f * Mathf.Log(2f) * exp_val;
                float deriv_B_term = 20f * ELASTIC_C5 * cos_component;
                deriv_val = deriv_A_factor * sin_component + (-0.5f * exp_val) * deriv_B_term;
            } else { // x > 0.5f
                float exp_val = Mathf.Pow(2f, -20f * x + 10f);
                float deriv_A_factor = -10f * Mathf.Log(2f) * exp_val;
                float deriv_B_term = 20f * ELASTIC_C5 * cos_component;
                deriv_val = deriv_A_factor * sin_component + (0.5f * exp_val) * deriv_B_term;
            }
            return Mathf.Clamp(deriv_val, -BIG_TANGENT, BIG_TANGENT);
        }
        private static float CalculateInOutElastic_Deriv(float x) {
            if (x == 0f || x == 1f) return 0f;
            return CalculateInOutElastic_Deriv_Internal(x);
        }

        // InBack easing functions
        private static float CalculateInBack_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            return BACK_C3 * x * x * x - BACK_C1 * x * x;
        }
        private static float CalculateInBack_Deriv(float x) {
            // dy/dx = 3 * BACK_C3 * x^2 - 2 * BACK_C1 * x
            if (x == 0f) return 0f;
            // if (x == 1f) return BACK_C1 + 3f; // Tangent at x=1
            return 3f * BACK_C3 * x * x - 2f * BACK_C1 * x;
        }

        // OutBack easing functions
        private static float CalculateOutBack_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            float p = x - 1f;
            return 1f + BACK_C3 * p * p * p + BACK_C1 * p * p;
        }
        private static float CalculateOutBack_Deriv(float x) {
            // dy/dx = 3 * BACK_C3 * (x-1)^2 + 2 * BACK_C1 * (x-1)
            if (x == 1f) return 0f;
            // if (x == 0f) return BACK_C1 + 3f; // Tangent at x=0
            float p = x - 1f;
            return 3f * BACK_C3 * p * p + 2f * BACK_C1 * p;
        }

        // InOutBack easing functions
        private static float CalculateInOutBack_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            if (x < 0.5f) {
                float t = x * 2f;
                return 0.5f * (t * t * (BACK_C4 * t - BACK_C2));
            } else {
                float t = (x * 2f) - 2f;
                return 0.5f * (t * t * (BACK_C4 * t + BACK_C2) + 2f);
            }
        }
        private static float CalculateInOutBack_Deriv(float x) {
            // dy/dx for x < 0.5: 12 * BACK_C4 * x^2 - 4 * BACK_C2 * x
            // dy/dx for x >= 0.5: 3 * BACK_C4 * (2x-2)^2 + 2 * BACK_C2 * (2x-2)
            if (x == 0f || x == 1f) return 0f;
            
            if (x < 0.5f) {
                // Original formula: 0.5 * ( (c2+1) * 8x^3 - c2 * 4x^2 )'
                // y = 4 * (c2+1) * x^3 - 2 * c2 * x^2
                // dy/dx = 12 * (c2+1) * x^2 - 4 * c2 * x
                // Here, c2 is BACK_C2, (c2+1) is BACK_C4
                return 12f * BACK_C4 * x * x - 4f * BACK_C2 * x;
            } else {
                // Original formula: 0.5 * ( (c2+1)*(2x-2)^3 + c2*(2x-2)^2 + 2 )'
                // dy/dx = (c2+1) * 3 * (2x-2)^2 + c2 * 2 * (2x-2)
                float t = (x * 2f) - 2f;
                return 3f * BACK_C4 * t * t + 2f * BACK_C2 * t;
            }
        }

        // Bounce easing value functions
        private static float CalculateOutBounce_Val(float x) {
            if (x < 1f / BOUNCE_D1) {
                return BOUNCE_N1 * x * x;
            } else if (x < 2f / BOUNCE_D1) {
                float x_shifted = x - (1.5f / BOUNCE_D1);
                return BOUNCE_N1 * x_shifted * x_shifted + 0.75f;
            } else if (x < 2.5f / BOUNCE_D1) {
                float x_shifted = x - (2.25f / BOUNCE_D1);
                return BOUNCE_N1 * x_shifted * x_shifted + 0.9375f;
            } else {
                float x_shifted = x - (2.625f / BOUNCE_D1);
                return BOUNCE_N1 * x_shifted * x_shifted + 0.984375f;
            }
        }

        private static float CalculateInBounce_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            return 1f - CalculateOutBounce_Val(1f - x);
        }

        private static float CalculateInOutBounce_Val(float x) {
            if (x == 0f) return 0f;
            if (x == 1f) return 1f;
            if (x < 0.5f) {
                return (1f - CalculateOutBounce_Val(1f - 2f * x)) / 2f;
            } else {
                return (1f + CalculateOutBounce_Val(2f * x - 1f)) / 2f;
            }
        }

        // Bounce easing derivative functions
        private static float CalculateOutBounce_Deriv(float x) {
            if (x < 0f) x = 0f; // Clamp for safety if called outside [0,1]
            if (x > 1f) x = 1f;

            if (x < 1f / BOUNCE_D1) { // Segment 1: 0 to 1/D1
                // y = N1*t^2
                // y' = 2*N1*t
                return 2f * BOUNCE_N1 * x;
            } else if (x < 2f / BOUNCE_D1) { // Segment 2: 1/D1 to 2/D1
                // y = N1*(t-k1)^2 + c1, k1 = 1.5/D1
                // y' = 2*N1*(t-k1)
                return 2f * BOUNCE_N1 * (x - 1.5f / BOUNCE_D1);
            } else if (x < 2.5f / BOUNCE_D1) { // Segment 3: 2/D1 to 2.5/D1
                // y = N1*(t-k2)^2 + c2, k2 = 2.25/D1
                // y' = 2*N1*(t-k2)
                return 2f * BOUNCE_N1 * (x - 2.25f / BOUNCE_D1);
            } else { // Segment 4: 2.5/D1 to 1
                // y = N1*(t-k3)^2 + c3, k3 = 2.625/D1
                // y' = 2*N1*(t-k3)
                return 2f * BOUNCE_N1 * (x - 2.625f / BOUNCE_D1);
            }
        }

        private static float CalculateOutBounce_InDeriv(float t) {
            if (Mathf.Approximately(t, 0f)) return 0f;

            // Derivative of segment ending at t
            if (t > 2.5f / BOUNCE_D1) { // Segment 4 (ending at t, or t is inside)
                return 2f * BOUNCE_N1 * (t - 2.625f / BOUNCE_D1);
            } else if (t > 2f / BOUNCE_D1) { // Segment 3
                return 2f * BOUNCE_N1 * (t - 2.25f / BOUNCE_D1);
            } else if (t > 1f / BOUNCE_D1) { // Segment 2
                return 2f * BOUNCE_N1 * (t - 1.5f / BOUNCE_D1);
            } else { // Segment 1 (t > 0)
                return 2f * BOUNCE_N1 * t;
            }
        }
        
        private static float CalculateOutBounce_OutDeriv(float t) { // Renamed for clarity
            return CalculateOutBounce_Deriv(t);
        }

        private static float CalculateInBounce_InDeriv(float x) {
            // InDeriv_g(x) = OutDeriv_f(1-x)
            return CalculateOutBounce_OutDeriv(1f - x);
        }
        private static float CalculateInBounce_OutDeriv(float x) {
            // OutDeriv_g(x) = InDeriv_f(1-x)
            return CalculateOutBounce_InDeriv(1f - x);
        }

        private static float CalculateInOutBounce_InDeriv(float x) {
            if (x < 0.5f) { // Corresponds to InBounce part: InDeriv_g(x) = OutDeriv_f(1-2x)
                return CalculateOutBounce_OutDeriv(1f - 2f * x);
            } else { // Corresponds to OutBounce part: InDeriv_g(x) = InDeriv_f(2x-1)
                return CalculateOutBounce_InDeriv(2f * x - 1f);
            }
        }
        private static float CalculateInOutBounce_OutDeriv(float x) {
            if (x < 0.5f) { // Corresponds to InBounce part: OutDeriv_g(x) = InDeriv_f(1-2x)
                return CalculateOutBounce_InDeriv(1f - 2f * x);
            } else { // Corresponds to OutBounce part: OutDeriv_g(x) = OutDeriv_f(2x-1)
                return CalculateOutBounce_OutDeriv(2f * x - 1f);
            }
        }

        private static float[] GetUniqueSortedTimes(System.Collections.Generic.List<float> timesList)
        {
            if (timesList == null || timesList.Count == 0) return new float[0];
            
            timesList.Sort();
            System.Collections.Generic.List<float> unique_times = new System.Collections.Generic.List<float>();
            if (timesList.Count > 0) {
                unique_times.Add(timesList[0]);
                for (int i = 1; i < timesList.Count; ++i) {
                    if (!Mathf.Approximately(timesList[i], timesList[i-1])) {
                        unique_times.Add(timesList[i]);
                    }
                }
            }
            return unique_times.ToArray();
        }

        public static AnimationCurve GetDefaultCurve(Ease ease)
        {
            // Local static functions for derivative calculations
            static float CalculateInCirc_Val(float x) {
                return 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - x * x));
            }
            static float CalculateInCirc_Deriv(float x) {
                if (x <= 0f) return 0f; // Derivative at x=0 is 0
                if (x >= 1f) return BIG_TANGENT; // Derivative at x=1 is infinite
                float val_in_sqrt = 1f - x * x;
                if (val_in_sqrt < 1e-7f) return BIG_TANGENT;
                return Mathf.Min(BIG_TANGENT, x / Mathf.Sqrt(val_in_sqrt));
            }

            static float CalculateOutCirc_Val(float x) {
                float term_1_minus_x = 1f - x;
                return Mathf.Sqrt(Mathf.Max(0f, 1f - term_1_minus_x * term_1_minus_x));
            }
            static float CalculateOutCirc_Deriv(float x) {
                if (x <= 0f) return BIG_TANGENT; // Derivative at x=0 is infinite
                if (x >= 1f) return 0f; // Derivative at x=1 is 0
                float term_1_minus_x = 1f - x;
                float val_in_sqrt = 1f - term_1_minus_x * term_1_minus_x;
                if (val_in_sqrt < 1e-7f) return BIG_TANGENT;
                return Mathf.Min(BIG_TANGENT, term_1_minus_x / Mathf.Sqrt(val_in_sqrt));
            }
            
            static float CalculateInOutCirc_Val(float x) {
                if (x < 0.5f) {
                    float term_2x = 2f * x;
                    return (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - term_2x * term_2x))) / 2f;
                } else {
                    float term_2x_minus_2 = 2f * x - 2f;
                    return (Mathf.Sqrt(Mathf.Max(0f, 1f - term_2x_minus_2 * term_2x_minus_2)) + 1f) / 2f;
                }
            }
            static float CalculateInOutCirc_Deriv(float x) {
                if (x == 0f || x == 1f) return 0f;
                if (Mathf.Approximately(x, 0.5f)) return 0;

                if (x < 0.5f) {
                    float term_2x = 2f * x;
                    float val_in_sqrt = 1f - term_2x * term_2x;
                    if (val_in_sqrt < 1e-7f) return BIG_TANGENT;
                    return Mathf.Min(BIG_TANGENT, term_2x / Mathf.Sqrt(val_in_sqrt));
                } else { // x > 0.5f
                    float term_2x_minus_2 = 2f * x - 2f;
                    float val_in_sqrt = 1f - term_2x_minus_2 * term_2x_minus_2;
                    if (val_in_sqrt < 1e-7f) return BIG_TANGENT;
                    return Mathf.Min(BIG_TANGENT, (2f - 2f * x) / Mathf.Sqrt(val_in_sqrt));
                }
            }

            switch (ease)
            {
                case Ease.Linear:
                    return AnimationCurve.Linear(0, 0, 1, 1);
                case Ease.InSine:
                    // y = 1 - cos((x * PI) / 2)
                    // dy/dx = sin((x*PI)/2) * PI/2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0), // x=0: dy/dx=0
                        new Keyframe(1, 1, PI / 2f, 0) // x=1: dy/dx=PI/2
                    );
                case Ease.OutSine:
                    // y = sin((x * PI) / 2)
                    // dy/dx = cos((x*PI)/2) * PI/2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, PI / 2f), // x=0: dy/dx=PI/2
                        new Keyframe(1, 1, 0, 0)    // x=1: dy/dx=0
                    );
                case Ease.InOutSine:
                    // y = 0.5 * (1 - cos(PI * x))
                    // dy/dx = 0.5 * sin(PI * x) * PI
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f), // x=0: dy/dx=0
                        new Keyframe(0.5f, 0.5f, PI / 2f, PI / 2f), // x=0.5: dy/dx=PI/2
                        new Keyframe(1f, 1f, 0f, 0f)  // x=1: dy/dx=0
                    );
                case Ease.InQuad:
                    // y = x^2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(1, 1, 2, 0)
                    );
                case Ease.OutQuad:
                    // y = 1 - (1 - x)^2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 2),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Ease.InOutQuad:
                    // y = 2x^2 (x<0.5), 1-2(1-x)^2 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.5f, 0.5f, 2, 2),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Ease.InCubic:
                    // y = x^3
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(1, 1, 3, 0)
                    );
                case Ease.OutCubic:
                    // y = 1 - (1-x)^3
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 3),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Ease.InOutCubic:
                    // y = 4x^3 (x<0.5), 1-4(1-x)^3 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.5f, 0.5f, 3, 3),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Ease.InQuart:
                    // y = x^4, dy/dx = 4x^3
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, Mathf.Pow(0.25f, 4), 4 * Mathf.Pow(0.25f, 3), 4 * Mathf.Pow(0.25f, 3)),
                        new Keyframe(0.5f, Mathf.Pow(0.5f, 4), 4 * Mathf.Pow(0.5f, 3), 4 * Mathf.Pow(0.5f, 3)),
                        new Keyframe(0.75f, Mathf.Pow(0.75f, 4), 4 * Mathf.Pow(0.75f, 3), 4 * Mathf.Pow(0.75f, 3)),
                        new Keyframe(1f, 1f, 4f, 4f)
                    );
                case Ease.OutQuart:
                    // y = 1 - (1-x)^4, dy/dx = 4(1-x)^3
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 4f, 4f),
                        new Keyframe(0.25f, 1f - Mathf.Pow(1f - 0.25f, 4), 4 * Mathf.Pow(1f - 0.25f, 3), 4 * Mathf.Pow(1f - 0.25f, 3)),
                        new Keyframe(0.5f, 1f - Mathf.Pow(1f - 0.5f, 4), 4 * Mathf.Pow(1f - 0.5f, 3), 4 * Mathf.Pow(1f - 0.5f, 3)),
                        new Keyframe(0.75f, 1f - Mathf.Pow(1f - 0.75f, 4), 4 * Mathf.Pow(1f - 0.75f, 3), 4 * Mathf.Pow(1f - 0.75f, 3)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Ease.InOutQuart:
                    // y = 8x^4 (x<0.5), 1-8(1-x)^4 (x>=0.5)
                    // dy/dx = 32x^3 (x<0.5), 32(1-x)^3 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, 8f * Mathf.Pow(0.25f, 4), 32f * Mathf.Pow(0.25f, 3), 32f * Mathf.Pow(0.25f, 3)),
                        new Keyframe(0.5f, 0.5f, 32f * Mathf.Pow(0.5f, 3), 32f * Mathf.Pow(0.5f, 3)),
                        new Keyframe(0.75f, 1f - 8f * Mathf.Pow(1f - 0.75f, 4), 32f * Mathf.Pow(1f - 0.75f, 3), 32f * Mathf.Pow(1f - 0.75f, 3)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Ease.InQuint:
                    // y = x^5, dy/dx = 5x^4
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, Mathf.Pow(0.25f, 5), 5 * Mathf.Pow(0.25f, 4), 5 * Mathf.Pow(0.25f, 4)),
                        new Keyframe(0.5f, Mathf.Pow(0.5f, 5), 5 * Mathf.Pow(0.5f, 4), 5 * Mathf.Pow(0.5f, 4)),
                        new Keyframe(0.75f, Mathf.Pow(0.75f, 5), 5 * Mathf.Pow(0.75f, 4), 5 * Mathf.Pow(0.75f, 4)),
                        new Keyframe(1f, 1f, 5f, 5f)
                    );
                case Ease.OutQuint:
                    // y = 1 - (1-x)^5, dy/dx = 5(1-x)^4
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 5f, 5f),
                        new Keyframe(0.25f, 1f - Mathf.Pow(1f - 0.25f, 5), 5 * Mathf.Pow(1f - 0.25f, 4), 5 * Mathf.Pow(1f - 0.25f, 4)),
                        new Keyframe(0.5f, 1f - Mathf.Pow(1f - 0.5f, 5), 5 * Mathf.Pow(1f - 0.5f, 4), 5 * Mathf.Pow(1f - 0.5f, 4)),
                        new Keyframe(0.75f, 1f - Mathf.Pow(1f - 0.75f, 5), 5 * Mathf.Pow(1f - 0.75f, 4), 5 * Mathf.Pow(1f - 0.75f, 4)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Ease.InOutQuint:
                    // y = 16x^5 (x<0.5), 1-16(1-x)^5 (x>=0.5)
                    // dy/dx = 80x^4 (x<0.5), 80(1-x)^4 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, 16f * Mathf.Pow(0.25f, 5), 80f * Mathf.Pow(0.25f, 4), 80f * Mathf.Pow(0.25f, 4)),
                        new Keyframe(0.5f, 0.5f, 80f * Mathf.Pow(0.5f, 4), 80f * Mathf.Pow(0.5f, 4)),
                        new Keyframe(0.75f, 1f - 16f * Mathf.Pow(1f - 0.75f, 5), 80f * Mathf.Pow(1f - 0.75f, 4), 80f * Mathf.Pow(1f - 0.75f, 4)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Ease.InExpo:
                    float[] inExpoTimes = {0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f};
                     // For InExpo, deriv at 0 is 0, deriv at 1 is large
                    Keyframe[] inExpoKeys = CreateKeyframes(
                        x => (x == 0f) ? 0f : Mathf.Pow(2f, 10f * (x - 1f)),
                        x => (x == 0f) ? 0f : 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (x-1f)),
                        x => (x == 0f) ? 0f : 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (x-1f)),
                        inExpoTimes, forceFlatStart: true, forceFlatEnd: false);
                    return new AnimationCurve(inExpoKeys);
                case Ease.OutExpo:
                    float[] outExpoTimes = {0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f};
                    // For OutExpo, deriv at 0 is large, deriv at 1 is 0
                    Keyframe[] outExpoKeys = CreateKeyframes(
                        x => (x == 1f) ? 1f : 1f - Mathf.Pow(2f, -10f * x),
                        x => (x == 1f) ? 0f : 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * x),
                        x => (x == 1f) ? 0f : 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * x),
                        outExpoTimes, forceFlatStart: false, forceFlatEnd: true);
                    return new AnimationCurve(outExpoKeys);
                case Ease.InOutExpo:
                    // y = x<0.5 ? 0.5 * 2^(20x-10) : 1-0.5*2^(-20x+10)
                    // dy/dx = x<0.5 ? 10*ln2*2^(20x-10) : 10*ln2*2^(-20x+10)
                    // Deriv at 0 is 0, at 0.5 is 10*ln2, at 1 is 0.
                    System.Func<float,float> inOutExpoVal = x => {
                        if (x == 0f) return 0f; if (x == 1f) return 1f; if (x == 0.5f) return 0.5f;
                        if (x < 0.5f) return 0.5f * Mathf.Pow(2f, 20f * x - 10f);
                        return 1f - 0.5f * Mathf.Pow(2f, -20f * x + 10f);
                    };
                     System.Func<float,float> inOutExpoDeriv = x => {
                        if (x == 0f || x == 1f) return 0f;
                        if (Mathf.Approximately(x,0.5f)) return 10f * Mathf.Log(2f); // Peak derivative
                        if (x < 0.5f) return 10f * Mathf.Log(2f) * Mathf.Pow(2f, 20f * x - 10f);
                        return 10f * Mathf.Log(2f) * Mathf.Pow(2f, -20f * x + 10f);
                    };
                    float[] inOutExpoTimes = {0f, 0.05f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 0.95f, 1f};
                    Keyframe[] inOutExpoKeys = CreateKeyframes(inOutExpoVal, inOutExpoDeriv, inOutExpoDeriv, inOutExpoTimes, forceFlatStart: true, forceFlatEnd: true);
                    return new AnimationCurve(inOutExpoKeys);
                case Ease.InCirc:
                    {
                        float[] times = {0f, 0.25f, 0.5f, 0.75f, 0.9f, 0.95f, 0.99f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateInCirc_Val, CalculateInCirc_Deriv, CalculateInCirc_Deriv, times, true, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.OutCirc:
                     {
                        float[] times = {0f, 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 0.75f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateOutCirc_Val, CalculateOutCirc_Deriv, CalculateOutCirc_Deriv, times, true, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.InOutCirc:
                    {
                        float[] times = {0f, 0.1f, 0.25f, 0.4f, 0.45f, 0.49f, 0.5f, 0.51f, 0.55f, 0.6f, 0.75f, 0.9f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateInOutCirc_Val, CalculateInOutCirc_Deriv, CalculateInOutCirc_Deriv, times, true, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.InElastic:
                    {
                        float[] times = {0f, 0.05f, 0.15f, .3f, .45f, 0.6f, 0.7f, 0.775f, 0.85f, 0.925f, 0.97f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateInElastic_Val, CalculateInElastic_Deriv, CalculateInElastic_Deriv, times, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.OutElastic:
                    {
                        float[] times = {0f, 0.03f, 0.075f, 0.15f, 0.225f, 0.3f, 0.4f, .55f, .7f, 0.85f, 0.95f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateOutElastic_Val, CalculateOutElastic_Deriv, CalculateOutElastic_Deriv, times, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.InOutElastic:
                    {
                        float[] times = {0f, 0.05f, 0.15f, 0.25f, 0.35f, 0.44375f, 0.5f, 0.55625f, 0.65f, 0.75f, 0.85f, 0.95f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateInOutElastic_Val, CalculateInOutElastic_Deriv, CalculateInOutElastic_Deriv, times, true, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.InBack:
                    {
                        float overshootTime = (2f * BACK_C1) / (3f * BACK_C3); // approx 0.4199f
                        float[] times = {0f, 0.1f, overshootTime, overshootTime + (1f - overshootTime) * 0.5f, 1f}; 
                        Keyframe[] keys = CreateKeyframes(CalculateInBack_Val, CalculateInBack_Deriv, CalculateInBack_Deriv, times, true, false);
                        return new AnimationCurve(keys);
                    }
                case Ease.OutBack:
                    {
                        float overshootCalc = (2f * BACK_C1) / (3f * BACK_C3);
                        float overshootTime = 1f - overshootCalc; // approx 0.5801f
                        float[] times = {0f, overshootTime * 0.5f, overshootTime, 0.9f, 1f}; 
                        Keyframe[] keys = CreateKeyframes(CalculateOutBack_Val, CalculateOutBack_Deriv, CalculateOutBack_Deriv, times, false, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.InOutBack:
                    {
                        float overshoot1Time = BACK_C2 / (3f * BACK_C4); 
                        float actualOvershoot1Time = overshoot1Time * 0.5f; 
                        float overshoot2TimeRelative = BACK_C2 / (3f * BACK_C4); 
                        float actualOvershoot2Time = 0.5f + (overshoot2TimeRelative * 0.5f); 
                        float[] times = {0f, 0.05f, actualOvershoot1Time, 0.5f, actualOvershoot2Time, 0.95f, 1f};
                        Keyframe[] keys = CreateKeyframes(CalculateInOutBack_Val, CalculateInOutBack_Deriv, CalculateInOutBack_Deriv, times, true, true);
                        return new AnimationCurve(keys);
                    }
                case Ease.InBounce:
                    {
                        float[] p_out_crit_times = {
                            0f, 1f/BOUNCE_D1, 1.5f/BOUNCE_D1, 2f/BOUNCE_D1, 
                            2.25f/BOUNCE_D1, 2.5f/BOUNCE_D1, 2.625f/BOUNCE_D1, 1f
                        };
                        System.Collections.Generic.List<float> times_list = new System.Collections.Generic.List<float>();
                        for(int i=0; i<p_out_crit_times.Length; ++i) times_list.Add(1f - p_out_crit_times[p_out_crit_times.Length - 1 - i]);
                        
                        // Focus sampling in the latter part (after ~0.4) where bounces occur
                        // Add intermediary points between bounce peaks for smoother curves
                        times_list.Add(0.4f);
                        times_list.Add(0.5f);
                        times_list.Add(0.6f);
                        times_list.Add(0.7f);
                        times_list.Add(0.8f);
                        times_list.Add(0.85f);
                        times_list.Add(0.9f);
                        times_list.Add(0.95f);
                        
                        // Add extra points around the bounce peaks
                        float peak1 = 1f - (2.5f/BOUNCE_D1);
                        float peak2 = 1f - (2f/BOUNCE_D1);
                        float peak3 = 1f - (1f/BOUNCE_D1);
                        
                        times_list.Add(peak1 - 0.03f);
                        times_list.Add(peak1 + 0.03f);
                        times_list.Add(peak2 - 0.03f);
                        times_list.Add(peak2 + 0.03f);
                        times_list.Add(peak3 - 0.03f);
                        times_list.Add(peak3 + 0.03f);
                        
                        float[] uniqueTimes = GetUniqueSortedTimes(times_list);
                        Keyframe[] keys = CreateKeyframes(CalculateInBounce_Val, CalculateInBounce_InDeriv, CalculateInBounce_OutDeriv, uniqueTimes, false, true);
                        
                        // Adjust weights at peaks for circular arcs
                        for (int i = 0; i < keys.Length; i++) {
                            float t = keys[i].time;
                            if (Mathf.Approximately(t, peak1) || 
                                Mathf.Approximately(t, peak2) || 
                                Mathf.Approximately(t, peak3)) {
                                keys[i].inWeight = 0.33f;
                                keys[i].outWeight = 0.33f;
                            }
                        }
                        
                        return new AnimationCurve(keys);
                    }
                case Ease.OutBounce:
                    {
                        System.Collections.Generic.List<float> times_list = new System.Collections.Generic.List<float> {
                            0f, 1f/BOUNCE_D1, 1.5f/BOUNCE_D1, 2f/BOUNCE_D1, 
                            2.25f/BOUNCE_D1, 2.5f/BOUNCE_D1, 2.625f/BOUNCE_D1, 1f
                        };
                        
                        // Add extra points near the bounce peaks for better circular shape
                        times_list.Add(0.2f/BOUNCE_D1 - 0.05f);
                        times_list.Add(0.2f/BOUNCE_D1 + 0.05f);
                        times_list.Add(0.5f/BOUNCE_D1 - 0.05f);
                        times_list.Add(0.5f/BOUNCE_D1 + 0.05f);
                        times_list.Add(1f/BOUNCE_D1 - 0.05f);
                        times_list.Add(1f/BOUNCE_D1 + 0.05f);
                        times_list.Add(1.5f/BOUNCE_D1 - 0.05f);
                        times_list.Add(1.5f/BOUNCE_D1 + 0.05f);
                        times_list.Add(2f/BOUNCE_D1 - 0.05f);
                        times_list.Add(2f/BOUNCE_D1 + 0.05f);
                        times_list.Add(2.5f/BOUNCE_D1 - 0.05f);
                        times_list.Add(2.5f/BOUNCE_D1 + 0.05f);
                        
                        float[] uniqueTimes = GetUniqueSortedTimes(times_list);
                        Keyframe[] keys = CreateKeyframes(CalculateOutBounce_Val, CalculateOutBounce_InDeriv, CalculateOutBounce_OutDeriv, uniqueTimes, true, false);
                        
                        // Adjust tangent weights at bounce peaks for more circular arcs
                        for (int i = 0; i < keys.Length; i++) {
                            float t = keys[i].time;
                            if (Mathf.Approximately(t, 1f/BOUNCE_D1) || 
                                Mathf.Approximately(t, 2f/BOUNCE_D1) || 
                                Mathf.Approximately(t, 2.5f/BOUNCE_D1)) {
                                keys[i].inWeight = 0.33f;
                                keys[i].outWeight = 0.33f;
                            }
                        }
                        
                        return new AnimationCurve(keys);
                    }
                case Ease.InOutBounce:
                    {
                        float[] p_out_crit_times = { 
                            0f, 1f/BOUNCE_D1, 1.5f/BOUNCE_D1, 2f/BOUNCE_D1, 
                            2.25f/BOUNCE_D1, 2.5f/BOUNCE_D1, 2.625f/BOUNCE_D1, 1f
                        };
                        System.Collections.Generic.List<float> times_list = new System.Collections.Generic.List<float>();
                        
                        // First half (InBounce scaled to [0, 0.5])
                        for(int i=0; i < p_out_crit_times.Length; ++i) {
                            times_list.Add((1f - p_out_crit_times[p_out_crit_times.Length - 1 - i]) / 2f);
                        }
                        
                        // Add extra points for first half - focus on area closer to 0.5 (end of in-part)
                        times_list.Add(0.2f);
                        times_list.Add(0.25f);
                        times_list.Add(0.3f);
                        times_list.Add(0.35f);
                        times_list.Add(0.4f);
                        times_list.Add(0.425f);
                        times_list.Add(0.45f);
                        times_list.Add(0.475f);
                        
                        // Add extra points around the bounce peaks in first half
                        float peak1 = (1f - (2.5f/BOUNCE_D1)) / 2f;
                        float peak2 = (1f - (2f/BOUNCE_D1)) / 2f;
                        float peak3 = (1f - (1f/BOUNCE_D1)) / 2f;
                        
                        times_list.Add(peak1 - 0.03f);
                        times_list.Add(peak1 + 0.03f);
                        times_list.Add(peak2 - 0.03f);
                        times_list.Add(peak2 + 0.03f);
                        times_list.Add(peak3 - 0.03f);
                        times_list.Add(peak3 + 0.03f);
                        
                        // Second half (OutBounce scaled to [0.5, 1])
                        for(int i=0; i < p_out_crit_times.Length; ++i) {
                            times_list.Add((p_out_crit_times[i] + 1f) / 2f);
                        }
                        
                        // Add extra points for second half
                        times_list.Add(0.525f);
                        times_list.Add(0.55f);
                        times_list.Add(0.575f);
                        times_list.Add(0.6f);
                        times_list.Add(0.65f);
                        times_list.Add(0.7f);
                        times_list.Add(0.75f);
                        times_list.Add(0.8f);
                        
                        // Add extra points around the bounce peaks in second half
                        float peak4 = (1f/BOUNCE_D1 + 1f) / 2f;
                        float peak5 = (2f/BOUNCE_D1 + 1f) / 2f;
                        float peak6 = (2.5f/BOUNCE_D1 + 1f) / 2f;
                        
                        times_list.Add(peak4 - 0.03f);
                        times_list.Add(peak4 + 0.03f);
                        times_list.Add(peak5 - 0.03f);
                        times_list.Add(peak5 + 0.03f);
                        times_list.Add(peak6 - 0.03f);
                        times_list.Add(peak6 + 0.03f);
                        
                        // Ensure key points are included
                        if (!times_list.Exists(t => Mathf.Approximately(t,0f))) times_list.Add(0f);
                        if (!times_list.Exists(t => Mathf.Approximately(t,0.5f))) times_list.Add(0.5f);
                        if (!times_list.Exists(t => Mathf.Approximately(t,1f))) times_list.Add(1f);
                        
                        float[] uniqueTimes = GetUniqueSortedTimes(times_list);
                        Keyframe[] keys = CreateKeyframes(CalculateInOutBounce_Val, CalculateInOutBounce_InDeriv, CalculateInOutBounce_OutDeriv, uniqueTimes, false, false);
                        
                        // Adjust weights at peaks for circular arcs on both halves
                        float[] peakPoints = {peak1, peak2, peak3, peak4, peak5, peak6};
                        
                        for (int i = 0; i < keys.Length; i++) {
                            float t = keys[i].time;
                            for (int p = 0; p < peakPoints.Length; p++) {
                                if (Mathf.Approximately(t, peakPoints[p])) {
                                    keys[i].inWeight = 0.33f;
                                    keys[i].outWeight = 0.33f;
                                    break;
                                }
                            }
                        }
                        
                        return new AnimationCurve(keys);
                    }
                case Ease.Flash:
                    // Changes instantly midway (0 for x<0.5, 1 for x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.499f, 0f, 0f, 0f),
                        new Keyframe(0.5f, 1f, 0f, 0f),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Ease.InFlash:
                    // Always 1
                    return new AnimationCurve(
                        new Keyframe(0f, 1f, 0f, 0f),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Ease.OutFlash:
                    // 0, but jumps to 1 at end
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.999f, 0f, 0f, 0f),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                default:
                    return AnimationCurve.Linear(0, 0, 1, 1);
            }
        }
    }
}
