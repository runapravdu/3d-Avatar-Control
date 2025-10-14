using System.Collections;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Events;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class PoseLandmarkerRunner : VisionTaskApiRunner<PoseLandmarker>
    {
        public UnityEvent<PoseLandmarkerResult> OnPoseLandmarksOutput = new UnityEvent<PoseLandmarkerResult>();
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;

        private Experimental.TextureFramePool _textureFramePool;

        public readonly PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;
        }

        // 👇 основной цикл распознавания
        protected override IEnumerator Run()
        {
            config.Model = ModelType.BlazePoseFull;
            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Image Read Mode = {config.ImageReadMode}");
            Debug.Log($"Model = {config.ModelName}");
            Debug.Log($"Running Mode = {config.RunningMode}");
            Debug.Log($"NumPoses = {config.NumPoses}");
            Debug.Log($"MinPoseDetectionConfidence = {config.MinPoseDetectionConfidence}");
            Debug.Log($"MinPosePresenceConfidence = {config.MinPosePresenceConfidence}");
            Debug.Log($"MinTrackingConfidence = {config.MinTrackingConfidence}");
            Debug.Log($"OutputSegmentationMasks = {config.OutputSegmentationMasks}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            var options = config.GetPoseLandmarkerOptions(
                config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null
            );

            taskApi = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Logger.LogError(TAG, "Failed to start ImageSource, exiting...");
                yield break;
            }

            _textureFramePool = new Experimental.TextureFramePool(
                imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10
            );

            screen.Initialize(imageSource);

            SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            _poseLandmarkerResultAnnotationController.InitScreen(
                imageSource.textureWidth, imageSource.textureHeight
            );

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;

            var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);
            var waitForEndOfFrame = new WaitForEndOfFrame();
            var result = PoseLandmarkerResult.Alloc(options.numPoses, options.outputSegmentationMasks);

            var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
            using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

            while (true)
            {
                if (isPaused)
                    yield return new WaitWhile(() => isPaused);

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return waitForEndOfFrame;
                    continue;
                }

                Image image;
                switch (config.ImageReadMode)
                {
                    case ImageReadMode.GPU:
                        if (!canUseGpuImage)
                            throw new System.Exception("ImageReadMode.GPU is not supported");

                        textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        image = textureFrame.BuildGPUImage(glContext);
                        yield return waitForEndOfFrame;
                        break;

                    case ImageReadMode.CPU:
                        yield return waitForEndOfFrame;
                        textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        image = textureFrame.BuildCPUImage();
                        textureFrame.Release();
                        break;

                    case ImageReadMode.CPUAsync:
                    default:
                        req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                        yield return waitUntilReqDone;

                        if (req.hasError)
                        {
                            Debug.LogWarning("Failed to read texture from the image source");
                            continue;
                        }

                        image = textureFrame.BuildCPUImage();
                        textureFrame.Release();
                        break;
                }

                switch (taskApi.runningMode)
                {
                    case Tasks.Vision.Core.RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
                            _poseLandmarkerResultAnnotationController.DrawNow(result);
                        else
                            _poseLandmarkerResultAnnotationController.DrawNow(default);
                        DisposeAllMasks(result);
                        break;

                    case Tasks.Vision.Core.RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
                            _poseLandmarkerResultAnnotationController.DrawNow(result);
                        else
                            _poseLandmarkerResultAnnotationController.DrawNow(default);
                        DisposeAllMasks(result);
                        break;

                    case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
                        taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                        break;
                }
            }
        }

        // 👇 Start() теперь корутина, override базового метода
        protected override IEnumerator Start()
        {
            Debug.Log("PoseLandmarkerRunner запущен и инициализируется...");
            yield return base.Start();
        }

        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
        {
            if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
            {
                Debug.Log($"<color=cyan>RUNNER: Отправляю результат с {result.poseLandmarks[0].landmarks.Count} точками.</color>");
            }
            else
            {
                Debug.Log("<color=orange>RUNNER: Пытаюсь отправить ПУСТОЙ результат!</color>");
            }

            _poseLandmarkerResultAnnotationController.DrawLater(result);
            OnPoseLandmarksOutput?.Invoke(result);
            DisposeAllMasks(result);
        }

        private void DisposeAllMasks(PoseLandmarkerResult result)
        {
            if (result.segmentationMasks != null)
            {
                foreach (var mask in result.segmentationMasks)
                    mask.Dispose();
            }
        }
    }
}
