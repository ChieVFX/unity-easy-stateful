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
            const float BIG_TANGENT = 100f; // Used for "infinite" derivatives

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
                        expoFrames3[i] = new Keyframe(x, y, key_inTangent, key_outTangent);
                    }
                    return new AnimationCurve(expoFrames3);
                case Stateful.Runtime.Ease.InCirc:
                    // y = 1 - sqrt(1 - x^2)
                    // dy/dx = x/sqrt(1-x^2)
                    {
                        Keyframe[] keys = new Keyframe[5];
                        float x_val, y_val, dy_val;

                        // Key 0: x=0
                        x_val = 0f;
                        y_val = 0f;
                        dy_val = 0f;
                        keys[0] = new Keyframe(x_val, y_val, dy_val, dy_val);

                        // Key 1: x=0.5
                        x_val = 0.5f;
                        y_val = 1f - Mathf.Sqrt(1f - x_val * x_val);
                        dy_val = x_val / Mathf.Max(1e-6f, Mathf.Sqrt(1f - x_val * x_val));
                        keys[1] = new Keyframe(x_val, y_val, dy_val, dy_val);
                        
                        // Key 2: x=0.8
                        x_val = 0.8f;
                        y_val = 1f - Mathf.Sqrt(1f - x_val * x_val);
                        dy_val = x_val / Mathf.Max(1e-6f, Mathf.Sqrt(1f - x_val * x_val));
                        keys[2] = new Keyframe(x_val, y_val, dy_val, dy_val);

                        // Key 3: x=0.95
                        x_val = 0.95f;
                        y_val = 1f - Mathf.Sqrt(1f - x_val * x_val);
                        dy_val = x_val / Mathf.Max(1e-6f, Mathf.Sqrt(1f - x_val * x_val));
                        keys[3] = new Keyframe(x_val, y_val, dy_val, dy_val);
                        
                        // Key 4: x=1
                        float x_near_1 = 0.999f; // Calculate tangent near end
                        float dy_near_1 = x_near_1 / Mathf.Max(1e-6f, Mathf.Sqrt(1f - x_near_1*x_near_1));
                        keys[4] = new Keyframe(1f, 1f, Mathf.Min(dy_near_1, BIG_TANGENT), 0f);
                        
                        return new AnimationCurve(keys);
                    }
                case Stateful.Runtime.Ease.OutCirc:
                    // y = sqrt(1 - (1-x)^2)
                    // dy/dx = (1-x)/sqrt(1-(1-x)^2)
                     {
                        Keyframe[] keys = new Keyframe[5];
                        float x_val, y_val, dy_val, term_1_minus_x;

                        // Key 0: x=0
                        float x_near_0 = 0.001f; // Calculate tangent near start
                        term_1_minus_x = 1f - x_near_0;
                        float dy_near_0 = term_1_minus_x / Mathf.Max(1e-6f, Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x));
                        keys[0] = new Keyframe(0f, 0f, 0f, Mathf.Min(dy_near_0, BIG_TANGENT));

                        // Key 1: x=0.05
                        x_val = 0.05f;
                        term_1_minus_x = 1f - x_val;
                        y_val = Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x);
                        dy_val = term_1_minus_x / Mathf.Max(1e-6f, Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x));
                        keys[1] = new Keyframe(x_val, y_val, dy_val, dy_val);
                        
                        // Key 2: x=0.2
                        x_val = 0.2f;
                        term_1_minus_x = 1f - x_val;
                        y_val = Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x);
                        dy_val = term_1_minus_x / Mathf.Max(1e-6f, Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x));
                        keys[2] = new Keyframe(x_val, y_val, dy_val, dy_val);

                        // Key 3: x=0.5
                        x_val = 0.5f;
                        term_1_minus_x = 1f - x_val;
                        y_val = Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x);
                        dy_val = term_1_minus_x / Mathf.Max(1e-6f, Mathf.Sqrt(1f - term_1_minus_x*term_1_minus_x));
                        keys[3] = new Keyframe(x_val, y_val, dy_val, dy_val);
                        
                        // Key 4: x=1
                        keys[4] = new Keyframe(1f, 1f, 0f, 0f);
                        
                        return new AnimationCurve(keys);
                    }
                case Stateful.Runtime.Ease.InOutCirc:
                    float[] circKeys3 = {0f, 0.05f, 0.1f, 0.2f, 0.3f, 0.5f, 0.7f, 0.8f, 0.9f, 0.95f, 1f};
                    Keyframe[] circFrames3 = new Keyframe[circKeys3.Length];
                    for (int i = 0; i < circKeys3.Length; i++) {
                        float x = circKeys3[i];
                        float y;
                        float key_inTangent = 0f, key_outTangent = 0f;

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
                                // y = (1 - sqrt(1 - (2x)^2)) / 2
                                // dy/dx = (2x) / sqrt(1 - (2x)^2)
                                float term_2x = 2f * x;
                                float val_in_sqrt = 1f - term_2x * term_2x;
                                y = (1f - Mathf.Sqrt(Mathf.Max(0f, val_in_sqrt))) / 2f;
                                if (val_in_sqrt < 1e-6f) tangentValue = BIG_TANGENT;
                                else tangentValue = term_2x / Mathf.Sqrt(val_in_sqrt);
                            } else if (x > 0.5f) {
                                // y = (sqrt(1 - (-2x+2)^2) + 1) / 2
                                // dy/dx = (2-2x) / sqrt(1 - (-2x+2)^2)
                                float term_2x_minus_2 = 2f * x - 2f;
                                float val_in_sqrt = 1f - term_2x_minus_2 * term_2x_minus_2;
                                y = (Mathf.Sqrt(Mathf.Max(0f, val_in_sqrt)) + 1f) / 2f;
                                if (val_in_sqrt < 1e-6f) tangentValue = BIG_TANGENT;
                                else tangentValue = (2f - 2f*x) / Mathf.Sqrt(val_in_sqrt);
                            } else { // x == 0.5
                                y = 0.5f;
                                tangentValue = BIG_TANGENT; 
                            }
                            key_inTangent = Mathf.Min(tangentValue, BIG_TANGENT); // Ensure tangent doesn't exceed BIG_TANGENT
                            key_outTangent = Mathf.Min(tangentValue, BIG_TANGENT);
                        }
                        circFrames3[i] = new Keyframe(x, y, key_inTangent, key_outTangent);
                    }
                    return new AnimationCurve(circFrames3);
                // For Elastic, Back, Bounce, Flash, etc., you may want to use custom or hand-tuned curves or leave as linear for now.
                default:
                    return AnimationCurve.Linear(0, 0, 1, 1);
            }
        }
    }
}
