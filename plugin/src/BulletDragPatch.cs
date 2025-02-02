using HarmonyLib;
using UnityEngine;

namespace visSpace.Patches
{
    [HarmonyPatch(typeof(FistVR.BallisticProjectile), "ApplyDrag")]
    public class BulletDragPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref Vector3 velocity, ref float time)
        {
            // Apply wind force before drag calculations
            velocity += (WindPlugin.ActiveGustVector + WindPlugin.WindVector) * time;


        }
    }
}