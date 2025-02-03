using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using BepInEx.Configuration;
using System;
using Valve.Newtonsoft.Json.Bson;

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
        public static Vector3 WindVector;
        public static Vector3 ActiveGustVector = Vector3.zero;
        // Configurable wind settings
        internal static float WindStrength;   // Multiplier for wind force
        internal static float WindChangeTime;
        internal static Vector3 GustVector;
        internal static float GustSpeed;
        internal static float GustRand;
        internal static float GustFreq;
        internal static float GustDuration;

        internal static bool AffectsRb;
        internal static ConfigEntry<float> WindStrConfig;
        internal static ConfigEntry<float> WindTimeConfig;
        internal static ConfigEntry<float> WindGustRandConfig;
        internal static ConfigEntry<float> WindGustIntConfig;
        internal static ConfigEntry<float> WindGustFreqConfig;
        internal static ConfigEntry<float> WindGustDurConfig;
        internal static ConfigEntry<bool> WindRBConfig;

        private void LoadConfig()
        {
            // Config system automatically loads these settings from a .cfg file
            //ConfigDefinition.Bind()

            WindStrConfig = Config.Bind("Wind Settings", "Wind Strength", 6.0f, "Controls the strength of the wind effect.");
            WindTimeConfig = Config.Bind("Wind Settings", "Wind Change Time", 150f, "Controls how often wind direction changes in minimum seconds.");
            WindRBConfig = Config.Bind("Wind Settings", "Wind Affects Rigidbodies", false, "Controls if wind affects all objects and not only bullets. EXPENSIVE, possibly annoying.");
            WindGustRandConfig = Config.Bind("Wind Settings", "Gust Randomness", 0.25f, "How random gusts are");
            WindGustIntConfig = Config.Bind("Wind Settings", "Gust Intensity", 3f, "How strong gusts are maximum");
            WindGustFreqConfig = Config.Bind("Wind Settings", "Gust Frequency", 5f, "How often gusts occur minimum");
            WindGustDurConfig = Config.Bind("Wind Settings", "Gust Duration", 2.5f, "How long gusts last minimum");
            AffectsRb = WindRBConfig.Value;
            WindStrength = WindStrConfig.Value;
            WindChangeTime = WindTimeConfig.Value;
            GustRand = WindGustRandConfig.Value;
            GustSpeed = WindGustIntConfig.Value;
            GustFreq = WindGustFreqConfig.Value;
            GustDuration = WindGustDurConfig.Value;

            WindStrConfig.SettingChanged += OnSettingChanged;
            WindRBConfig.SettingChanged += OnSettingChanged;
            WindTimeConfig.SettingChanged += OnSettingChanged;
            WindGustIntConfig.SettingChanged += OnSettingChanged;
            WindGustRandConfig.SettingChanged += OnSettingChanged;
            WindGustFreqConfig.SettingChanged += OnSettingChanged;
            WindGustDurConfig.SettingChanged += OnSettingChanged;
        }
        private void OnSettingChanged(object sender, EventArgs e)
        {

            WindStrength = WindStrConfig.Value;
            AffectsRb = WindRBConfig.Value;
            WindChangeTime = WindTimeConfig.Value;
            GustRand = WindGustRandConfig.Value;
            GustSpeed = WindGustIntConfig.Value;
            GustFreq = WindGustFreqConfig.Value;
            GustDuration = WindGustDurConfig.Value;

            //GenerateWind();

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
            StartCoroutine(UpdateGustVector());
            StartCoroutine(ManageRBWind());
        }
        private void OnDestroy()
        {
            // Unsubscribe from the SettingChanged event to avoid memory leaks
            WindStrConfig.SettingChanged -= OnSettingChanged;
            WindRBConfig.SettingChanged -= OnSettingChanged;
            WindTimeConfig.SettingChanged -= OnSettingChanged;
            WindGustRandConfig.SettingChanged -= OnSettingChanged;
            WindGustIntConfig.SettingChanged -= OnSettingChanged;
            WindGustFreqConfig.SettingChanged -= OnSettingChanged;
            WindGustDurConfig.SettingChanged -= OnSettingChanged;
        }
        private IEnumerator UpdateGustVector()
        {
            while (true)
            {
                GustRand = WindGustRandConfig.Value;
                GustSpeed = WindGustIntConfig.Value;
                GustFreq = WindGustFreqConfig.Value;
                GustDuration = WindGustDurConfig.Value;
                // Assume windVector is already set.
                GustVector = WindVector;

                // Create a random offset vector:
                // We'll generate a random vector (with a slight vertical constraint) and normalize it,
                // then scale it by 1/4 of the wind's magnitude.
                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-0.1f, 0.1f),
                    UnityEngine.Random.Range(-1f, 1f)
                ).normalized * (GustVector.magnitude * GustRand);

                // Start gustVector as windVector plus the random offset.
                GustVector += randomOffset;

                // Now scale the magnitude of gustVector by a random factor.
                // For example, you can choose a factor between 0.5 and 1.5.
                float scaleFactor = UnityEngine.Random.Range(0.01f, GustSpeed);
                GustVector *= scaleFactor;

                yield return StartCoroutine(SmoothDoGust(UnityEngine.Random.Range(GustDuration, GustDuration * 4)));

                yield return new WaitForSeconds(UnityEngine.Random.Range(GustFreq, GustFreq * 4));
            }
        }
        private IEnumerator SmoothDoGust(float duration)
        {
            Vector3 startValue = ActiveGustVector;

            float elapsedTime = 0f;
            float r1 = UnityEngine.Random.Range(duration / 5, duration - duration / 5);
            float r2 = UnityEngine.Random.Range(duration / 5, duration - duration / 5);

            float attackTime = Mathf.Min(r1, r2);
            float sustainTime = Mathf.Max(r1, r2) - attackTime;
            float decayTime = duration - (attackTime + sustainTime);

            while (elapsedTime < attackTime)
            {
                ActiveGustVector = Vector3.Slerp(startValue, GustVector, elapsedTime / attackTime);

                elapsedTime += Time.deltaTime;

                yield return null;
            }
            yield return new WaitForSeconds(sustainTime);
            elapsedTime = 0;
            while (elapsedTime < decayTime)
            {
                ActiveGustVector = Vector3.Slerp(ActiveGustVector, startValue, elapsedTime / decayTime);

                elapsedTime += Time.deltaTime;

                yield return null;
            }
            ActiveGustVector *= 0;
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


        private IEnumerator ManageRBWind()
        {
            float updateTime = 1.5f;
            while (true)
            {
                AffectsRb = WindRBConfig.Value;
                if (AffectsRb)
                {
                    foreach (Rigidbody rb in FindObjectsOfType<Rigidbody>()) // Apply wind to all rigidbodies
                    {
                        if (rb.mass > 0.001)
                        {
                            StartCoroutine(ApplyWindForce(rb, updateTime));
                        }
                    }

                }

                yield return new WaitForSeconds(updateTime);
            }
        }
        private IEnumerator ApplyWindForce(Rigidbody rb, float time)
        {

            float elapsedTime = 0f;

            // Get surface area from collider - in game drag values aren't always set correctly.
            float surfaceArea = GetColliderSurfaceArea(rb);
            if (surfaceArea > 0.025)
            {
                while (elapsedTime < time)
                {
                    if (rb == null)
                        break;
                    Vector3 windAndGust = WindVector + ActiveGustVector;
                    Vector3 windDirection = windAndGust.normalized;
                    float windSpeedSqr = windAndGust.sqrMagnitude;
                    //float randTime = Mathf.RoundToInt(UnityEngine.Random.Range(2, 6)) * Time.fixedDeltaTime;
                    //Vector3 relativeVelocity = rb.velocity;
                    //float velocityInWindDirection = Vector3.Dot(relativeVelocity.normalized, WindVector.normalized) * relativeVelocity.sqrMagnitude;
                    float velocityInWindDirection = Vector3.Dot(rb.velocity, windDirection);
                    float remainingWindSpeedSqr = Mathf.Max(0, windSpeedSqr - velocityInWindDirection * velocityInWindDirection);


                    if (remainingWindSpeedSqr > 0)
                    {
                        //float forceScale = (1f - (relativeSpeed / windSpeed)) * 1; // Scale force based on velocity
                        Vector3 windForce = windDirection * Mathf.Sqrt(remainingWindSpeedSqr) * surfaceArea;


                        rb.AddForce(windForce, ForceMode.Force); // Apply force to the rigidbody
                    }
                    yield return new WaitForFixedUpdate();
                    elapsedTime += Time.fixedDeltaTime;
                }
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
                yield return new WaitForSeconds(randTime / UnityEngine.Random.Range(1, 16));
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
