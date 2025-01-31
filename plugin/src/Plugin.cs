using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using BepInEx.Configuration;
using System;

namespace visSpace
{

    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    public partial class WindPlugin : BaseUnityPlugin
    {
        /* == Quick Start == 
         * Your plugin class is a Unity MonoBehaviour that gets added to a global game object when the game starts.
         * You should use Awake to initialize yourself, read configs, register stuff, etc.
         * If you need to use Update or other Unity event methods those will work too.
         *
         * Some references on how to do various things:
         * Adding config settings to your plugin: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/4_configuration.html
         * Hooking / Patching game methods: https://harmony.pardeike.net/articles/patching.html
         * Also check out the Unity documentation: https://docs.unity3d.com/560/Documentation/ScriptReference/index.html
         * And the C# documentation: https://learn.microsoft.com/en-us/dotnet/csharp/
         */
        internal static Vector3 WindVector;
        // Configurable wind settings
        internal static float WindStrength;   // Multiplier for wind force
        internal static float WindChangeTime;
        internal static bool AffectsRb;
        internal static ConfigEntry<float> WindStrConfig;
        internal static ConfigEntry<float> WindTimeConfig;
        internal static ConfigEntry<bool> WindRBConfig;

        private void LoadConfig()
        {
            // Config system automatically loads these settings from a .cfg file
            //ConfigDefinition.Bind()

            WindStrConfig = Config.Bind("Wind Settings", "Wind Strength", 3.0f, "Controls the strength of the wind effect.");
            WindTimeConfig = Config.Bind("Wind Settings", "Wind Change Time", 30f, "Controls how often wind direction changes in minimum seconds.");
            WindRBConfig = Config.Bind("Wind Settings", "Wind Affects Rigidbodies", true, "Controls if wind affects all objects and not only bullets.");
            AffectsRb = WindRBConfig.Value;
            WindStrength = WindStrConfig.Value;
            WindChangeTime = WindTimeConfig.Value;

            WindStrConfig.SettingChanged += OnSettingChanged;
            WindRBConfig.SettingChanged += OnSettingChanged;
            WindTimeConfig.SettingChanged += OnSettingChanged;
        }
        private void OnSettingChanged(object sender, EventArgs e)
        {
            // Check if the changed setting is one of the relevant ones
            //if (e.ChangedSetting.Definition.Section == "Wind Settings")
            //{

            WindStrength = WindStrConfig.Value;
            AffectsRb = WindRBConfig.Value;
            WindChangeTime = WindTimeConfig.Value;
            GenerateWind();

            //}
        }
        private void Awake()
        {
            Logger = base.Logger;
            Harmony harmony = new Harmony("com.vi.windplugin");
            harmony.PatchAll();
            LoadConfig();
            

            // Your plugin's ID, Name, and Version are available here.
            //Logger.LogMessage($"Hello, world! Sent from {Id} {Name} {Version}");

            GenerateWind();
            StartCoroutine(UpdateWind());
        }
        private void OnDestroy()
        {
            // Unsubscribe from the SettingChanged event to avoid memory leaks
            WindStrConfig.SettingChanged -= OnSettingChanged;
            WindRBConfig.SettingChanged -= OnSettingChanged;
            WindTimeConfig.SettingChanged -= OnSettingChanged;
        }
        private IEnumerator SmoothChangeWind(Vector3 targetValue, float duration)
        {
            Vector3 startValue = WindVector;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                WindVector = Vector3.Lerp(startValue, targetValue, elapsedTime / duration);
                elapsedTime += Time.deltaTime;

                yield return null; // Wait until the next frame
            }

            WindVector = targetValue; // Ensure it reaches the exact target value
        }
        private void FixedUpdate()
        {
            if(AffectsRb) {
                
                foreach (Rigidbody rb in FindObjectsOfType<Rigidbody>()) // Apply wind to all rigidbodies
                {
                    ApplyWindForce(rb);
                }
            }
        }
        private void ApplyWindForce(Rigidbody rb)
        {
            // Get surface area from collider
            float surfaceArea = GetColliderSurfaceArea(rb);

            Vector3 windDirection = WindVector.normalized;
            float windSpeedSqr = WindVector.sqrMagnitude;
            //Vector3 relativeVelocity = rb.velocity;
            //float velocityInWindDirection = Vector3.Dot(relativeVelocity.normalized, WindVector.normalized) * relativeVelocity.sqrMagnitude;
            float velocityInWindDirection = Vector3.Dot(rb.velocity, windDirection);
            float remainingWindSpeedSqr = Mathf.Max(0, windSpeedSqr - velocityInWindDirection * velocityInWindDirection);

            
           
            if (remainingWindSpeedSqr >0)
            {
                //float forceScale = (1f - (relativeSpeed / windSpeed)) * 1; // Scale force based on velocity
                Vector3 windForce = windDirection * Mathf.Sqrt(remainingWindSpeedSqr) * surfaceArea;


                rb.AddForce(windForce, ForceMode.Force); // Apply force to the rigidbody
            }
        }

        private float GetColliderSurfaceArea(Rigidbody rb)
        {
            float totalSurfaceArea = 0f;

            // Loop through all colliders attached to the Rigidbody
            foreach (Collider collider in rb.GetComponents<Collider>())
            {
                // Get the bounds of the collider and approximate surface area as a box
                Bounds bounds = collider.bounds;
                // Surface area of a box = 2 * (width * height + width * depth + height * depth)
                float surfaceArea = 2f * (bounds.size.x * bounds.size.y + bounds.size.x * bounds.size.z + bounds.size.y * bounds.size.z);
                totalSurfaceArea += surfaceArea;
            }
 
            return totalSurfaceArea;
        }
        private IEnumerator UpdateWind()
        {
            while (true)
            {
                float windX = UnityEngine.Random.Range(-1f, 1f);
                float windY = UnityEngine.Random.Range(-0.1f, 0.1f);
                float windZ = UnityEngine.Random.Range(-1f, 1f);

                Vector3 newWind = new Vector3(windX, windY, windZ) * UnityEngine.Random.Range(0.01f, WindStrength);
                float randTime = WindChangeTime * UnityEngine.Random.Range(1, 4);
                yield return StartCoroutine(SmoothChangeWind(newWind, randTime));
                if (Logger != null)
                    Logger.LogInfo($"Generated Wind Vector: {newWind}");
                // Wait for 5 seconds before running again
                yield return new WaitForSeconds(randTime /8);
            }
        }

        public static void GenerateWind()
        {
            // Random wind direction and magnitude

            float windX = UnityEngine.Random.Range(-1f, 1f);
            float windY = UnityEngine.Random.Range(-0.1f, 0.1f);
            float windZ = UnityEngine.Random.Range(-1f, 1f);

            WindVector = new Vector3(windX, windY, windZ) * UnityEngine.Random.Range(0.01f, WindStrength);
            if (Logger != null)
                Logger.LogInfo($"Generated Wind Vector: {WindVector}");
        }
        // The line below allows access to your plugin's logger from anywhere in your code, including outside of this file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)
        internal new static ManualLogSource Logger { get; private set; }
    }

}
