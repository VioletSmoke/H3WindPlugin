using HarmonyLib;
using UnityEngine;

namespace visSpace.Patches
{
    [HarmonyPatch(typeof(FistVR.BallisticProjectile), "ApplyDrag")]
    public class BulletDragPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref Vector3 velocity)
        {
            // Apply wind force before drag calculations
            velocity += WindPlugin.WindVector * Time.deltaTime;


        }
    }
}