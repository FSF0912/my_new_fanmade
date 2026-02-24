using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FSF.CollectionFrame
{
    public enum VisualizationMode
    {
        ByAudioSource,
        ByMicrophone
    };
    
    // New: Enum to define the bar generation shape
    public enum GenerationShape
    {
        Linear,     // Linear
        Circular    // Circular
    };

    /// <summary>
    /// Creates an audio visualization effect by analyzing the spectrum of an AudioSource.
    /// Can be driven by a preset AudioClip or real-time microphone input.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVisualizer : MonoBehaviour
    {
        #region Variables

        public VisualizationMode visualizationMode = VisualizationMode.ByAudioSource;

        [Range(64, 8192)]
        [Tooltip("The number of samples for spectrum analysis. Must be a power of 2.")]
        public int LengthSample = 256;

        [Range(0.01f, 30f)]
        public float lerpSpeed = 12f;

        [Min(0.01f)]
        public float intensity = 1f;
        
        [Min(1f)]
        [Tooltip("The maximum scale/height the visualization bars can reach.")]
        public float MaxBarHeight = 50f;

        [Space(15.00f)]
        public Transform[] BarsList;

        // Fields for generating visualization bars in the editor
        [HideInInspector] public GenerationShape Shape = GenerationShape.Linear;
        [HideInInspector] public int ElementCount = 32;
        [HideInInspector] public RectTransform ElementHolder;
        [HideInInspector] public GameObject VisualBarPrefab;

        // --- Linear Shape Settings ---
        [HideInInspector] public float BarWidth = 10f;
        [HideInInspector] public float BarSpacing = 5f;
        [HideInInspector] public float PaddingTop = 10f;
        [HideInInspector] public float PaddingBottom = 10f;

        // --- Circular Shape Settings ---
        [HideInInspector] public float CircleRadius = 150f;
        [HideInInspector] [Range(1, 360)] public float CircleAngle = 360f;

        private float[] spectrumData;
        private AudioSource audioSource;

        #endregion

        #region Unity Methods

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            spectrumData = new float[LengthSample];

            // If not specified in the editor, generate bars automatically at runtime
            // (Note: Runtime generation requires all parameters to be set correctly)
            if (BarsList == null || BarsList.Length == 0)
            {
                // We check ElementHolder/Prefab at generation time
                // GenerateVisualizationBar(); // Commented out to avoid errors if not set up
            }

            if (visualizationMode == VisualizationMode.ByMicrophone)
            {
                InitializeMicrophone();
            }
        }

        private void Update()
        {
            if (!audioSource.isPlaying || spectrumData == null) return;
            
            audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
            UpdateVisualizerBars();
        }

        #endregion

        #region Core Logic

        private void InitializeMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("AudioVisualizer: No microphone device found for visualization.");
                return;
            }
            string device = Microphone.devices[0];
            audioSource.clip = Microphone.Start(device, true, 10, 44100);
            audioSource.loop = true;
            while (!(Microphone.GetPosition(device) > 0)) { }
            audioSource.Play();
        }

        private void UpdateVisualizerBars()
        {
            if (BarsList == null || BarsList.Length == 0) return;

            for (int i = 0;  i < BarsList.Length; i++)
            {
                if (BarsList[i] == null) continue;
                
                // This multiplier boosts the amplitude of higher frequencies
                float multiplier = 50f + i * i * 0.5f;
                // Clamp the target height (scale) to the public MaxBarHeight
                float targetHeight = Mathf.Clamp(spectrumData[i] * multiplier * intensity, 0f, MaxBarHeight);

                Transform bar = BarsList[i];
                Vector3 currentScale = bar.localScale;
                // We only animate the Y-axis scale
                Vector3 targetScale = new Vector3(currentScale.x, targetHeight, currentScale.z);

                bar.localScale = Vector3.Lerp(currentScale, targetScale, Time.deltaTime * lerpSpeed);
            }
        }

        #endregion

        #region Editor Methods

        /// <summary>
        /// Instantiates and lays out visualization bars based on the selected shape and parameters.
        /// This method is called from the custom editor.
        /// </summary>
        public void GenerateVisualizationBar()
        {
            if (ElementHolder == null || VisualBarPrefab == null || ElementCount <= 0)
            {
                Debug.LogError("Please set ElementHolder, VisualBarPrefab, and ElementCount must be greater than 0.");
                return;
            }
            
            // Clear old bars
            foreach (Transform child in ElementHolder)
            {
#if UNITY_EDITOR
                // Use DestroyImmediate in editor scripts
                DestroyImmediate(child.gameObject);
#else
                // Fallback for runtime
                Destroy(child.gameObject);
#endif
            }

            Transform[] newBars = new Transform[ElementCount];
            for (int i = 0; i < ElementCount; i++)
            {
                GameObject go = Instantiate(VisualBarPrefab, ElementHolder);
                go.name = $"VisualizationBar_{i + 1}";

#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
#endif

                // Configure based on the selected shape
                switch (Shape)
                {
                    case GenerationShape.Linear:
                        ConfigureLinearBar(go, i);
                        break;
                    case GenerationShape.Circular:
                        ConfigureCircularBar(go, i);
                        break;
                }
                newBars[i] = go.transform;
            }
            BarsList = newBars;
        }

        /// <summary>
        /// Configures a bar in a linear layout based on new parameters.
        /// </summary>
        private void ConfigureLinearBar(GameObject bar, int index)
        {
            var barRect = bar.GetComponent<RectTransform>();
            if (barRect == null)
            {
                Debug.LogError("Instantiated bar must have a RectTransform component.");
                return;
            }

            float holderWidth = ElementHolder.rect.width;
            float holderHeight = ElementHolder.rect.height;

            // --- Sizing ---
            // Calculate the available height inside the padding
            float availableHeight = holderHeight - PaddingTop - PaddingBottom;
            if (availableHeight <= 0) availableHeight = 1f; // Avoid division by zero

            // Calculate the base height (scale 1) so that at MaxBarHeight it fills the available space
            float baseHeight = availableHeight / MaxBarHeight; 
            
            // Set the bar's size
            barRect.sizeDelta = new Vector2(BarWidth, baseHeight);

            // --- Positioning ---
            // Force pivot to bottom-center for correct scaling from the bottom
            barRect.pivot = new Vector2(0.5f, 0f);
            // Ensure anchors are centered (for anchoredPosition to work relative to center)
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            
            // Calculate total width of all bars and spaces to center the group
            float totalBarsWidth = (ElementCount * BarWidth) + (Mathf.Max(0, ElementCount - 1) * BarSpacing);
            
            // Calculate the starting X position to center the group
            float startX = -totalBarsWidth / 2f + BarWidth / 2f;
            float posX = startX + index * (BarWidth + BarSpacing);

            // Calculate the Y position (bottom of the holder + bottom padding)
            float posY = -holderHeight / 2f + PaddingBottom;

            barRect.anchoredPosition = new Vector2(posX, posY);
            
            // Reset scale to 1 (or 0 if you want them to animate in)
            barRect.localScale = new Vector3(1, 0, 1);
        }

        /// <summary>
        /// Configures a bar in a circular layout.
        /// </summary>
        private void ConfigureCircularBar(GameObject bar, int index)
        {
            // Important: For best results, set the bar prefab's Pivot to the bottom center (X=0.5, Y=0).
            if (!bar.TryGetComponent<RectTransform>(out var barRect))
            {
                Debug.LogError("Instantiated bar must have a RectTransform component.");
                return;
            }

            float angleStep = CircleAngle / ElementCount;
            float currentAngle = angleStep * index;

            // Positioning
            float posX = CircleRadius * Mathf.Cos(currentAngle * Mathf.Deg2Rad);
            float posY = CircleRadius * Mathf.Sin(currentAngle * Mathf.Deg2Rad);
            barRect.anchoredPosition = new Vector2(posX, posY);

            // Rotate to make the bar face outwards
            barRect.localRotation = Quaternion.Euler(0, 0, currentAngle-90f);

            // Reset scale
            barRect.localScale = new Vector3(1, 0, 1);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears all generated visualization bars from the holder and the list.
        /// </summary>
        public void ClearGeneratedBars()
        {
            if (BarsList == null) return;

            // Destroy all GameObjects referenced in the array
            foreach (Transform bar in BarsList)
            {
                if (bar != null && bar.gameObject != null)
                {
                    // Record the destruction for the Undo system
                    Undo.DestroyObjectImmediate(bar.gameObject);
                }
            }
            
            // Set the list to null or empty. This will trigger the editor to show the "Generate" tool again.
            BarsList = null; 
        }
        
        /// <summary>
        /// Resets the LengthSample value to its default (256).
        /// </summary>
        public void ResetSampleValue()
        {
            LengthSample = 256;
        }
#endif

        #endregion
    }

#if UNITY_EDITOR
    /// <summary>
    /// Custom editor to provide a user-friendly UI for generating bars
    /// and validating the LengthSample property.
    /// </summary>
    [CustomEditor(typeof(AudioVisualizer))]
    public class AudioVisualizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI() draws all public fields (like MaxBarHeight)
            base.OnInspectorGUI(); 
            var T = target as AudioVisualizer;
            if (T == null) return;

            serializedObject.Update();

            // --- 'MaxBarHeight' is no longer drawn here, base.OnInspectorGUI() handles it ---

            // Show EITHER the generation tool OR the clear button
            if (T.BarsList == null || T.BarsList.Length == 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Bar Generation Tool", EditorStyles.boldLabel);

                // Check if the target object is a persistent asset (a prefab) on disk
                if (EditorUtility.IsPersistent(T))
                {
                    EditorGUILayout.HelpBox(
                        "Bar generation must be run on a scene instance, not on the Prefab asset itself. " +
                        "Please drag this prefab into your scene and generate the bars on that instance.", 
                        MessageType.Warning);
                }
                else // It's a scene instance, so it's safe to show the generation tool
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("ElementCount"), new GUIContent("Bar Count"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("ElementHolder"), new GUIContent("Parent Container"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("VisualBarPrefab"), new GUIContent("Bar Prefab"));
                        
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("Shape"), new GUIContent("Layout Mode"));

                        // Show different settings based on the selected layout mode
                        if (T.Shape == GenerationShape.Linear)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("BarWidth"), new GUIContent("Bar Width"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("BarSpacing"), new GUIContent("Bar Spacing"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("PaddingTop"), new GUIContent("Padding Top"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("PaddingBottom"), new GUIContent("Padding Bottom"));
                            EditorGUI.indentLevel--;
                        }
                        else if (T.Shape == GenerationShape.Circular)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("CircleRadius"), new GUIContent("Radius"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("CircleAngle"), new GUIContent("Circle Angle"));
                            EditorGUI.indentLevel--;
                        }
                        
                        EditorGUILayout.Space(5);
                        if (GUILayout.Button("Generate Visualization Bars"))
                        {
                            // Register the component for Undo, so clearing/re-generating can be undone
                            Undo.RecordObject(T, "Generate Visualization Bars");
                            T.GenerateVisualizationBar();
                        }
                    }
                }
            }
            else // --- NEW: Show Clear button if bars already exist ---
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Bar Management", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox(
                        $"There are {T.BarsList.Length} bars currently generated. " +
                        "Clear them to access the generation tool again.", 
                        MessageType.Info);
                    
                    // Set button color to be slightly "destructive" (reddish)
                    Color originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); 
                    
                    if (GUILayout.Button("Clear Generated Bars"))
                    {
                        // Record the change (setting BarsList to null) for the Undo system
                        Undo.RecordObject(T, "Clear Generated Bars");
                        T.ClearGeneratedBars();
                    }
                    
                    GUI.backgroundColor = originalColor; // Reset color
                }
            }

            // Validate if LengthSample is a power of 2
            if (!EditorUtility.IsPersistent(T) && (T.LengthSample & (T.LengthSample - 1)) != 0)
            {
                EditorGUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox("LengthSample must be a power of 2 (e.g., 64, 128, 256, 512).", MessageType.Error);
                    if (GUILayout.Button("Reset to Default (256)"))
                    {
                        Undo.RecordObject(T, "Reset Sample Value");
                        T.ResetSampleValue();
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}