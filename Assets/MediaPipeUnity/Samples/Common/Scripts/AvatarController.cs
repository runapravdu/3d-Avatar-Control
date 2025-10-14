using UnityEngine;
using System.Linq;

using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.Core;

public class AvatarController : MonoBehaviour
{
    [Tooltip("Перетащите сюда объект со сцены с компонентом PoseLandmarkerResultAnnotationController")]
    public PoseLandmarkerResultAnnotationController annotationController;

    [Tooltip("Насколько плавным будет движение. 0 - без плавности, 1 - без движения.")]
    [Range(0, 1)]
    public float smoothness = 0.2f;

    private Animator animator;

    // Трансформы костей
    private Transform leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm;
    // ... здесь можно добавить другие кости: позвоночник, ноги, голову

    // Корректирующие вращения
    private Quaternion leftUpperArmOffset, rightUpperArmOffset, leftLowerArmOffset, rightLowerArmOffset;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Находим кости
        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

        if (annotationController == null)
        {
            Debug.LogError("Поле 'Annotation Controller' не заполнено! Перетащите сюда объект со сцены с этим компонентом.", this);
            this.enabled = false; // Отключаем скрипт, если нет контроллера
            return;
        }

        // --- ВАЖНАЯ ЧАСТЬ: ВЫЧИСЛЕНИЕ КОРРЕКЦИИ ---
        // Предполагаем, что модель находится в идеальной Т-позе при запуске
        // Вектор от правого плеча к левому будет нашим "верхним" направлением для рук
        Vector3 shoulderUp = (leftUpperArm.position - rightUpperArm.position).normalized;

        // Вычисляем начальные направления костей в Т-позе
        Vector3 initialLeftUpperArmDir = leftLowerArm.position - leftUpperArm.position;
        Vector3 initialRightUpperArmDir = rightLowerArm.position - rightUpperArm.position;
        // ... и для предплечий
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Vector3 initialLeftLowerArmDir = leftHand.position - leftLowerArm.position;
        Vector3 initialRightLowerArmDir = rightHand.position - rightLowerArm.position;

        // Вычисляем и сохраняем корректирующее вращение
        leftUpperArmOffset = Quaternion.Inverse(Quaternion.LookRotation(initialLeftUpperArmDir, shoulderUp)) * leftUpperArm.rotation;
        rightUpperArmOffset = Quaternion.Inverse(Quaternion.LookRotation(initialRightUpperArmDir, shoulderUp)) * rightUpperArm.rotation;
        leftLowerArmOffset = Quaternion.Inverse(Quaternion.LookRotation(initialLeftLowerArmDir, initialLeftUpperArmDir)) * leftLowerArm.rotation;
        rightLowerArmOffset = Quaternion.Inverse(Quaternion.LookRotation(initialRightLowerArmDir, initialRightUpperArmDir)) * rightLowerArm.rotation;
    }

    void LateUpdate() // Используем LateUpdate для работы с анимацией, чтобы не конфликтовать с Animator'ом
    {
        if (annotationController == null) return;

        var result = annotationController._currentTarget;

        if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0) return;

        var worldLandmarks = result.poseWorldLandmarks[0];
        if (worldLandmarks.landmarks.Count < 33) return; // Убедимся, что все точки на месте
        var landmarks = worldLandmarks.landmarks;

        // Получаем точки из MediaPipe
        var pLeftShoulder = landmarks[11];
        var pRightShoulder = landmarks[12];
        var pLeftElbow = landmarks[13];
        var pRightElbow = landmarks[14];
        var pLeftWrist = landmarks[15];
        var pRightWrist = landmarks[16];

        // Конвертируем в векторы Unity (с инверсией Y и Z)
        Vector3 leftShoulderVec = new Vector3(pLeftShoulder.x, -pLeftShoulder.y, -pLeftShoulder.z);
        Vector3 rightShoulderVec = new Vector3(pRightShoulder.x, -pRightShoulder.y, -pRightShoulder.z);
        Vector3 leftElbowVec = new Vector3(pLeftElbow.x, -pLeftElbow.y, -pLeftElbow.z);
        Vector3 rightElbowVec = new Vector3(pRightElbow.x, -pRightElbow.y, -pRightElbow.z);
        Vector3 leftWristVec = new Vector3(pLeftWrist.x, -pLeftWrist.y, -pLeftWrist.z);
        Vector3 rightWristVec = new Vector3(pRightWrist.x, -pRightWrist.y, -pRightWrist.z);

        // Направления костей из данных MediaPipe
        Vector3 rightUpperArmDirection = (rightElbowVec - rightShoulderVec).normalized;
        Vector3 leftUpperArmDirection = (leftElbowVec - leftShoulderVec).normalized;
        Vector3 rightLowerArmDirection = (rightWristVec - rightElbowVec).normalized;
        Vector3 leftLowerArmDirection = (leftWristVec - leftElbowVec).normalized;

        // Вектор "вверх" для плеч (от левого плеча к правому)
        Vector3 armUpVector = (rightShoulderVec - leftShoulderVec).normalized;

        // Вычисляем целевое вращение с помощью LookRotation
        Quaternion targetLeftUpperArmRotation = Quaternion.LookRotation(leftUpperArmDirection, armUpVector);
        Quaternion targetRightUpperArmRotation = Quaternion.LookRotation(rightUpperArmDirection, armUpVector);
        Quaternion targetLeftLowerArmRotation = Quaternion.LookRotation(leftLowerArmDirection, leftUpperArmDirection);
        Quaternion targetRightLowerArmRotation = Quaternion.LookRotation(rightLowerArmDirection, rightUpperArmDirection);
        
        // --- ПРИМЕНЯЕМ ВРАЩЕНИЕ С КОРРЕКЦИЕЙ И СГЛАЖИВАНИЕМ ---
        leftUpperArm.rotation = Quaternion.Slerp(leftUpperArm.rotation, targetLeftUpperArmRotation * leftUpperArmOffset, smoothness);
        rightUpperArm.rotation = Quaternion.Slerp(rightUpperArm.rotation, targetRightUpperArmRotation * rightUpperArmOffset, smoothness);
        leftLowerArm.rotation = Quaternion.Slerp(leftLowerArm.rotation, targetLeftLowerArmRotation * leftLowerArmOffset, smoothness);
        rightLowerArm.rotation = Quaternion.Slerp(rightLowerArm.rotation, targetRightLowerArmRotation * rightLowerArmOffset, smoothness);
    }
}