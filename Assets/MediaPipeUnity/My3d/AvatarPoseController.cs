using UnityEngine;
using System.Collections.Generic;
using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class MediaPipeAvatarController : PoseLandmarkerResultAnnotationController
{
    private Animator animator;
    private readonly Dictionary<HumanBodyBones, Transform> boneMap = new Dictionary<HumanBodyBones, Transform>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone < HumanBodyBones.LastBone)
            {
                Transform boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null) { boneMap[bone] = boneTransform; }
            }
        }
    }

    public new void DrawNow(PoseLandmarkerResult result)
    {
        // Вызываем оригинальную функцию, чтобы скелет рисовался. 
        // Если будет ошибка, эту строку можно закомментировать.

        // Наша логика управления аватаром
        var landmarksList = result.poseWorldLandmarks;
        if (landmarksList == null || landmarksList.Count == 0 || animator == null) return;
        
        var landmarksContainer = landmarksList[0];
        if (landmarksContainer.landmarks == null || landmarksContainer.landmarks.Count == 0) return;

        var landmarks = landmarksContainer.landmarks;

        if (boneMap.ContainsKey(HumanBodyBones.LeftUpperArm) && boneMap.ContainsKey(HumanBodyBones.LeftLowerArm))
        {
            Transform leftUpperArm = boneMap[HumanBodyBones.LeftUpperArm];
            Transform leftLowerArm = boneMap[HumanBodyBones.LeftLowerArm];
            
            Vector3 p11 = GetLandmarkPosition(landmarks, 11);
            Vector3 p13 = GetLandmarkPosition(landmarks, 13);
            Vector3 p15 = GetLandmarkPosition(landmarks, 15);
            Vector3 p12 = GetLandmarkPosition(landmarks, 12);
            
            RotateBone(leftUpperArm, p13, p11, p12 - p11);
            RotateBone(leftLowerArm, p15, p13, p12 - p11);
        }
    }
    
    private Vector3 GetLandmarkPosition(IReadOnlyList<Landmark> landmarks, int index)
    {
        var landmark = landmarks[index];
        return new Vector3(landmark.x, -landmark.y, -landmark.z);
    }
    
    private void RotateBone(Transform bone, Vector3 targetPosition, Vector3 sourcePosition, Vector3 upVector)
    {
        Quaternion rotation = Quaternion.LookRotation(targetPosition - sourcePosition, upVector);
        bone.rotation = Quaternion.Slerp(bone.rotation, rotation, 0.5f);
    }
}