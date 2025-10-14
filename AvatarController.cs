using UnityEngine;
using System.Linq;

// --- ВСТАВЬТЕ СЮДА СТРОКИ 'USING' ИЗ ФАЙЛА ANNOTATION CONTROLLER ---
// Например:
// using Mediapipe.Unity.Sample.Common; 
// -----------------------------------------------------------------

using Mediapipe.Unity.Sample.PoseLandmarkDetection; 
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.Core;

public class AvatarController : MonoBehaviour
{
    // Теперь компилятор точно найдет этот класс
    public PoseLandmarkerResultAnnotationController annotationController;

    private Animator animator;
    private Transform leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm;

    void Start()
    {
        animator = GetComponent<Animator>();

        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

        if (annotationController == null)
        {
            Debug.LogError("Поле 'Annotation Controller' не заполнено!", this);
        }
    }

    void Update()
    {
        if (annotationController == null) return;

        // Используем переменную, которую мы сделали публичной
        var result = annotationController._latestResult; 

        if (result == null || result.poseWorldLandmarks.Count == 0) return;

        var worldLandmarks = result.poseWorldLandmarks[0];
        var landmarks = worldLandmarks.landmarks;
        
        var pLeftShoulder = landmarks[11];
        var pRightShoulder = landmarks[12];
        var pLeftElbow = landmarks[13];
        var pRightElbow = landmarks[14];
        var pLeftWrist = landmarks[15];
        var pRightWrist = landmarks[16];

        Vector3 leftShoulderVec = new Vector3(pLeftShoulder.x, -pLeftShoulder.y, -pLeftShoulder.z);
        Vector3 rightShoulderVec = new Vector3(pRightShoulder.x, -pRightShoulder.y, -pRightShoulder.z);
        Vector3 leftElbowVec = new Vector3(pLeftElbow.x, -pLeftElbow.y, -pLeftElbow.z);
        Vector3 rightElbowVec = new Vector3(pRightElbow.x, -pRightElbow.y, -pRightElbow.z);
        Vector3 leftWristVec = new Vector3(pLeftWrist.x, -pLeftWrist.y, -pLeftWrist.z);
        Vector3 rightWristVec = new Vector3(pRightWrist.x, -pRightWrist.y, -pRightWrist.z);
        
        Vector3 rightUpperArmDirection = (rightElbowVec - rightShoulderVec).normalized;
        Vector3 leftUpperArmDirection = (leftElbowVec - leftShoulderVec).normalized;
        Vector3 rightLowerArmDirection = (rightWristVec - rightElbowVec).normalized;
        Vector3 leftLowerArmDirection = (leftWristVec - leftElbowVec).normalized;

        leftUpperArm.rotation = Quaternion.LookRotation(leftUpperArmDirection, rightShoulderVec - leftShoulderVec);
        rightUpperArm.rotation = Quaternion.LookRotation(rightUpperArmDirection, leftShoulderVec - rightShoulderVec);
        leftLowerArm.rotation = Quaternion.LookRotation(leftLowerArmDirection, leftUpperArmDirection);
        rightLowerArm.rotation = Quaternion.LookRotation(rightLowerArmDirection, rightUpperArmDirection);
    }
}