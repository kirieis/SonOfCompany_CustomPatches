using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SonOfCompany_CustomPatches.Patches
{
    public static class RouletteAudioManager
    {
        public static AudioClip SpinClip { get; private set; }
        private static bool _isLoading = false;

        public static void Initialize(MonoBehaviour host)
        {
            if (SpinClip != null || _isLoading || host == null) return;
            _isLoading = true;

            string pluginDir = Path.GetDirectoryName(typeof(CustomPatchesPlugin).Assembly.Location);
            string localMp3 = Path.Combine(pluginDir, "roulette_spin.mp3");
            string editMp3 = @"F:\EDIT\sound effects\freesound_community-revolver-chamber-spin-ratchet-sound.mp3";

            string targetPath = File.Exists(localMp3) ? localMp3 : editMp3;

            if (File.Exists(targetPath))
            {
                host.StartCoroutine(LoadClip(targetPath));
            }
            else
            {
                CustomPatchesPlugin.Log.LogWarning($"[Roulette] Spin audio file not found at {targetPath}");
                _isLoading = false;
            }
        }

        private static IEnumerator LoadClip(string path)
        {
            string uri = "file:///" + path.Replace("\\", "/");
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    SpinClip = DownloadHandlerAudioClip.GetContent(www);
                    SpinClip.name = "RussianRouletteSpinSFX";
                    CustomPatchesPlugin.Log.LogInfo($"[Roulette] Successfully loaded spin audio clip from {path}!");
                }
                else
                {
                    CustomPatchesPlugin.Log.LogWarning($"[Roulette] Failed to load audio clip: {www.error}");
                }
            }
            _isLoading = false;
        }

        public static void PlaySpin(AudioSource source)
        {
            if (source != null && SpinClip != null)
            {
                source.PlayOneShot(SpinClip, 1.0f);
            }
        }
    }
}

