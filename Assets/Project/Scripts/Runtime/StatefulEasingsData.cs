using UnityEngine;

namespace EasyStateful.Runtime {
    [CreateAssetMenu(fileName = "StatefulEasingsData", menuName = "Stateful UI/Easings Data", order = 10)]
    public class StatefulEasingsData : ScriptableObject
    {
        public AnimationCurve[] curves = new AnimationCurve[System.Enum.GetValues(typeof(Stateful.Runtime.Ease)).Length];

        private void OnValidate()
        {
            if (curves == null || curves.Length != System.Enum.GetValues(typeof(Stateful.Runtime.Ease)).Length)
            {
                var old = curves;
                curves = new AnimationCurve[System.Enum.GetValues(typeof(Stateful.Runtime.Ease)).Length];
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
                curves[i] = GetDefaultCurve((Stateful.Runtime.Ease)i);
            }
        }

        public static AnimationCurve GetDefaultCurve(Stateful.Runtime.Ease ease)
        {
            const float PI = Mathf.PI;
            const float BIG_TANGENT = 100f; // Used for "infinite" or very large derivatives

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
                if (Mathf.Approximately(x, 0.5f)) return BIG_TANGENT;

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
                case Stateful.Runtime.Ease.Linear:
                    return AnimationCurve.Linear(0, 0, 1, 1);
                case Stateful.Runtime.Ease.InSine:
                    // y = 1 - cos((x * PI) / 2)
                    // dy/dx = sin((x*PI)/2) * PI/2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0), // x=0: dy/dx=0
                        new Keyframe(1, 1, PI / 2f, 0) // x=1: dy/dx=PI/2
                    );
                case Stateful.Runtime.Ease.OutSine:
                    // y = sin((x * PI) / 2)
                    // dy/dx = cos((x*PI)/2) * PI/2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, PI / 2f), // x=0: dy/dx=PI/2
                        new Keyframe(1, 1, 0, 0)    // x=1: dy/dx=0
                    );
                case Stateful.Runtime.Ease.InOutSine:
                    // y = 0.5 * (1 - cos(PI * x))
                    // dy/dx = 0.5 * sin(PI * x) * PI
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f), // x=0: dy/dx=0
                        new Keyframe(0.5f, 0.5f, PI / 2f, PI / 2f), // x=0.5: dy/dx=PI/2
                        new Keyframe(1f, 1f, 0f, 0f)  // x=1: dy/dx=0
                    );
                case Stateful.Runtime.Ease.InQuad:
                    // y = x^2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(1, 1, 2, 0)
                    );
                case Stateful.Runtime.Ease.OutQuad:
                    // y = 1 - (1 - x)^2
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 2),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Stateful.Runtime.Ease.InOutQuad:
                    // y = 2x^2 (x<0.5), 1-2(1-x)^2 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.5f, 0.5f, 2, 2),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Stateful.Runtime.Ease.InCubic:
                    // y = x^3
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(1, 1, 3, 0)
                    );
                case Stateful.Runtime.Ease.OutCubic:
                    // y = 1 - (1-x)^3
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 3),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Stateful.Runtime.Ease.InOutCubic:
                    // y = 4x^3 (x<0.5), 1-4(1-x)^3 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.5f, 0.5f, 3, 3),
                        new Keyframe(1, 1, 0, 0)
                    );
                case Stateful.Runtime.Ease.InQuart:
                    // y = x^4, dy/dx = 4x^3
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, Mathf.Pow(0.25f, 4), 4 * Mathf.Pow(0.25f, 3), 4 * Mathf.Pow(0.25f, 3)),
                        new Keyframe(0.5f, Mathf.Pow(0.5f, 4), 4 * Mathf.Pow(0.5f, 3), 4 * Mathf.Pow(0.5f, 3)),
                        new Keyframe(0.75f, Mathf.Pow(0.75f, 4), 4 * Mathf.Pow(0.75f, 3), 4 * Mathf.Pow(0.75f, 3)),
                        new Keyframe(1f, 1f, 4f, 4f)
                    );
                case Stateful.Runtime.Ease.OutQuart:
                    // y = 1 - (1-x)^4, dy/dx = 4(1-x)^3
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 4f, 4f),
                        new Keyframe(0.25f, 1f - Mathf.Pow(1f - 0.25f, 4), 4 * Mathf.Pow(1f - 0.25f, 3), 4 * Mathf.Pow(1f - 0.25f, 3)),
                        new Keyframe(0.5f, 1f - Mathf.Pow(1f - 0.5f, 4), 4 * Mathf.Pow(1f - 0.5f, 3), 4 * Mathf.Pow(1f - 0.5f, 3)),
                        new Keyframe(0.75f, 1f - Mathf.Pow(1f - 0.75f, 4), 4 * Mathf.Pow(1f - 0.75f, 3), 4 * Mathf.Pow(1f - 0.75f, 3)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Stateful.Runtime.Ease.InOutQuart:
                    // y = 8x^4 (x<0.5), 1-8(1-x)^4 (x>=0.5)
                    // dy/dx = 32x^3 (x<0.5), 32(1-x)^3 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, 8f * Mathf.Pow(0.25f, 4), 32f * Mathf.Pow(0.25f, 3), 32f * Mathf.Pow(0.25f, 3)),
                        new Keyframe(0.5f, 0.5f, 32f * Mathf.Pow(0.5f, 3), 32f * Mathf.Pow(0.5f, 3)),
                        new Keyframe(0.75f, 1f - 8f * Mathf.Pow(1f - 0.75f, 4), 32f * Mathf.Pow(1f - 0.75f, 3), 32f * Mathf.Pow(1f - 0.75f, 3)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Stateful.Runtime.Ease.InQuint:
                    // y = x^5, dy/dx = 5x^4
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, Mathf.Pow(0.25f, 5), 5 * Mathf.Pow(0.25f, 4), 5 * Mathf.Pow(0.25f, 4)),
                        new Keyframe(0.5f, Mathf.Pow(0.5f, 5), 5 * Mathf.Pow(0.5f, 4), 5 * Mathf.Pow(0.5f, 4)),
                        new Keyframe(0.75f, Mathf.Pow(0.75f, 5), 5 * Mathf.Pow(0.75f, 4), 5 * Mathf.Pow(0.75f, 4)),
                        new Keyframe(1f, 1f, 5f, 5f)
                    );
                case Stateful.Runtime.Ease.OutQuint:
                    // y = 1 - (1-x)^5, dy/dx = 5(1-x)^4
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 5f, 5f),
                        new Keyframe(0.25f, 1f - Mathf.Pow(1f - 0.25f, 5), 5 * Mathf.Pow(1f - 0.25f, 4), 5 * Mathf.Pow(1f - 0.25f, 4)),
                        new Keyframe(0.5f, 1f - Mathf.Pow(1f - 0.5f, 5), 5 * Mathf.Pow(1f - 0.5f, 4), 5 * Mathf.Pow(1f - 0.5f, 4)),
                        new Keyframe(0.75f, 1f - Mathf.Pow(1f - 0.75f, 5), 5 * Mathf.Pow(1f - 0.75f, 4), 5 * Mathf.Pow(1f - 0.75f, 4)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Stateful.Runtime.Ease.InOutQuint:
                    // y = 16x^5 (x<0.5), 1-16(1-x)^5 (x>=0.5)
                    // dy/dx = 80x^4 (x<0.5), 80(1-x)^4 (x>=0.5)
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.25f, 16f * Mathf.Pow(0.25f, 5), 80f * Mathf.Pow(0.25f, 4), 80f * Mathf.Pow(0.25f, 4)),
                        new Keyframe(0.5f, 0.5f, 80f * Mathf.Pow(0.5f, 4), 80f * Mathf.Pow(0.5f, 4)),
                        new Keyframe(0.75f, 1f - 16f * Mathf.Pow(1f - 0.75f, 5), 80f * Mathf.Pow(1f - 0.75f, 4), 80f * Mathf.Pow(1f - 0.75f, 4)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Stateful.Runtime.Ease.InExpo:
                    // y = x==0 ? 0 : pow(2, 10 * (x - 1)), dy/dx = 10 * ln(2) * pow(2, 10 * (x - 1))
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.1f, Mathf.Pow(2f, 10f * (0.1f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.1f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.1f - 1f))),
                        new Keyframe(0.25f, Mathf.Pow(2f, 10f * (0.25f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.25f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.25f - 1f))),
                        new Keyframe(0.5f, Mathf.Pow(2f, 10f * (0.5f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.5f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.5f - 1f))),
                        new Keyframe(0.75f, Mathf.Pow(2f, 10f * (0.75f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.75f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.75f - 1f))),
                        new Keyframe(0.9f, Mathf.Pow(2f, 10f * (0.9f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.9f - 1f)), 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (0.9f - 1f))),
                        new Keyframe(1f, 1f, 10f * Mathf.Log(2f), 10f * Mathf.Log(2f))
                    );
                case Stateful.Runtime.Ease.OutExpo:
                    // y = x==1 ? 1 : 1 - pow(2, -10 * x), dy/dx = 10 * ln(2) * pow(2, -10 * x)
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 10f * Mathf.Log(2f), 10f * Mathf.Log(2f)),
                        new Keyframe(0.1f, 1f - Mathf.Pow(2f, -10f * 0.1f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.1f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.1f)),
                        new Keyframe(0.25f, 1f - Mathf.Pow(2f, -10f * 0.25f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.25f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.25f)),
                        new Keyframe(0.5f, 1f - Mathf.Pow(2f, -10f * 0.5f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.5f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.5f)),
                        new Keyframe(0.75f, 1f - Mathf.Pow(2f, -10f * 0.75f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.75f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.75f)),
                        new Keyframe(0.9f, 1f - Mathf.Pow(2f, -10f * 0.9f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.9f), 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * 0.9f)),
                        new Keyframe(1f, 1f, 0f, 0f)
                    );
                case Stateful.Runtime.Ease.InOutExpo:
                    float[] expoKeys3 = {0f, 0.05f, 0.1f, 0.2f, 0.3f, 0.5f, 0.7f, 0.8f, 0.9f, 0.95f, 1f};
                    Keyframe[] expoFrames3 = new Keyframe[expoKeys3.Length];
                    for (int i = 0; i < expoKeys3.Length; i++) {
                        float x = expoKeys3[i];
                        float y;
                        float key_inTangent = 0f, key_outTangent = 0f;
                        float key_inWeight = 1/3f, key_outWeight = 1/3f;
                        
                        if (x == 0f) { 
                            y = 0f; 
                            key_inTangent = 0f; 
                            key_outTangent = 0f; 
                        }
                        else if (x == 1f) { 
                            y = 1f; 
                            key_inTangent = 0f; 
                            key_outTangent = 0f; 
                        }
                        else { 
                            float tangentValue;
                            if (x < 0.5f) {
                                // y = 0.5 * 2^(20x - 10)
                                // dy/dx = 10 * ln(2) * 2^(20x - 10)
                                y = 0.5f * Mathf.Pow(2f, 20f * x - 10f);
                                tangentValue = 10f * Mathf.Log(2f) * Mathf.Pow(2f, 20f * x - 10f); 
                            } else if (x > 0.5f) {
                                // y = 1 - 0.5 * 2^(-20x + 10)
                                // dy/dx = 10 * ln(2) * 2^(-20x + 10)
                                y = 1f - 0.5f * Mathf.Pow(2f, -20f * x + 10f);
                                tangentValue = 10f * Mathf.Log(2f) * Mathf.Pow(2f, -20f * x + 10f); 
                            } else { // x == 0.5
                                y = 0.5f;
                                tangentValue = 10f * Mathf.Log(2f); // dy/dx at x=0.5
                            }
                            key_inTangent = tangentValue;
                            key_outTangent = tangentValue;
                        }

                        // Set weights for expo (generally smooth, but flat at ends)
                        if (Mathf.Approximately(key_inTangent, 0f)) key_inWeight = 0f;
                        if (Mathf.Approximately(key_outTangent, 0f)) key_outWeight = 0f;
                        
                        expoFrames3[i] = new Keyframe(x, y, key_inTangent, key_outTangent, key_inWeight, key_outWeight);
                        expoFrames3[i].weightedMode = WeightedMode.Both;
                    }
                    return new AnimationCurve(expoFrames3);
                case Stateful.Runtime.Ease.InCirc:
                    {
                        float[] times = {0f, 0.25f, 0.5f, 0.75f, 0.9f, 0.95f, 0.99f, 1f};
                        Keyframe[] keys = new Keyframe[times.Length];
                        for(int i=0; i < times.Length; ++i)
                        {
                            float t = times[i];
                            float val = CalculateInCirc_Val(t);
                            float deriv_val = CalculateInCirc_Deriv(t);
                            
                            float inT, outT;
                            float inW = 1/3f, outW = 1/3f;

                            if (t == 0f) {
                                inT = 0f; outT = 0f; // Start is flat
                            } else if (t == 1f) {
                                inT = deriv_val; // deriv_val will be BIG_TANGENT
                                outT = 0f;      // End is flat out
                            } else {
                                inT = deriv_val;
                                outT = deriv_val;
                            }

                            if (Mathf.Approximately(inT, 0f) || Mathf.Approximately(inT, BIG_TANGENT)) inW = 0f;
                            if (Mathf.Approximately(outT, 0f) || Mathf.Approximately(outT, BIG_TANGENT)) outW = 0f;
                            
                            keys[i] = new Keyframe(t, val, inT, outT, inW, outW);
                            keys[i].weightedMode = WeightedMode.Both;
                        }
                        return new AnimationCurve(keys);
                    }
                case Stateful.Runtime.Ease.OutCirc:
                     {
                        float[] times = {0f, 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 0.75f, 1f};
                        Keyframe[] keys = new Keyframe[times.Length];
                        for(int i=0; i < times.Length; ++i)
                        {
                            float t = times[i];
                            float val = CalculateOutCirc_Val(t);
                            float deriv_val = CalculateOutCirc_Deriv(t);

                            float inT, outT;
                            float inW = 1/3f, outW = 1/3f;

                            if (t == 0f) {
                                inT = 0f;       // Start is flat in
                                outT = deriv_val; // deriv_val will be BIG_TANGENT
                            } else if (t == 1f) {
                                inT = 0f; outT = 0f; // End is flat
                            } else {
                                inT = deriv_val;
                                outT = deriv_val;
                            }
                            
                            if (Mathf.Approximately(inT, 0f) || Mathf.Approximately(inT, BIG_TANGENT)) inW = 0f;
                            if (Mathf.Approximately(outT, 0f) || Mathf.Approximately(outT, BIG_TANGENT)) outW = 0f;

                            keys[i] = new Keyframe(t, val, inT, outT, inW, outW);
                            keys[i].weightedMode = WeightedMode.Both;
                        }
                        return new AnimationCurve(keys);
                    }
                case Stateful.Runtime.Ease.InOutCirc:
                    {
                        float[] times = {0f, 0.1f, 0.25f, 0.4f, 0.45f, 0.49f, 0.5f, 0.51f, 0.55f, 0.6f, 0.75f, 0.9f, 1f};
                        Keyframe[] keys = new Keyframe[times.Length];
                        for(int i=0; i < times.Length; ++i)
                        {
                            float t = times[i];
                            float val = CalculateInOutCirc_Val(t);
                            float deriv_val = CalculateInOutCirc_Deriv(t);
                            
                            float inT = deriv_val, outT = deriv_val;
                            float inW = 1/3f, outW = 1/3f;

                            if(t == 0f || t == 1f) { // Start and End are flat
                                inT = 0f; outT = 0f;
                            }
                            // For t = 0.5f, deriv_val is already BIG_TANGENT from CalculateInOutCirc_Deriv

                            if (Mathf.Approximately(inT, 0f) || Mathf.Approximately(inT, BIG_TANGENT)) inW = 0f;
                            if (Mathf.Approximately(outT, 0f) || Mathf.Approximately(outT, BIG_TANGENT)) outW = 0f;
                            
                            keys[i] = new Keyframe(t, val, inT, outT, inW, outW);
                            keys[i].weightedMode = WeightedMode.Both;
                        }
                        return new AnimationCurve(keys);
                    }
                // For Elastic, Back, Bounce, Flash, etc., you may want to use custom or hand-tuned curves or leave as linear for now.
                default:
                    return AnimationCurve.Linear(0, 0, 1, 1);
            }
        }
    }
}
