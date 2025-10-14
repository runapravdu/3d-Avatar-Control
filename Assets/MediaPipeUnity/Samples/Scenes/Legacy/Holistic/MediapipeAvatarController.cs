using UnityEngine;
using Mediapipe;
using System.Collections.Generic;
using System;
using Mediapipe.Unity.Sample.Holistic;

public class MediapipeAvatarController : MonoBehaviour
{
    // (Класс AvatarTree и все поля [SerializeField] остались без изменений)
    #region Fields and Classes 
    public class AvatarTree
    {
        public Transform transf;
        public AvatarTree[] childs;
        public AvatarTree parent;
        public int idx;
        public Quaternion initialLocalRotation;

        public AvatarTree(Transform tf, int count, int idx, AvatarTree parent = null)
        {
            if (tf == null) return;
            this.transf = tf;
            this.parent = parent;
            this.idx = idx;
            this.initialLocalRotation = tf.localRotation;
            if (count > 0) childs = new AvatarTree[count];
        }
    }

    [Header("Кости Тела (Transforms)")]
    [SerializeField] private Transform hips;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform leftLowerArm;
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform rightLowerArm;
    [SerializeField] private Transform leftUpperLeg;
    [SerializeField] private Transform leftLowerLeg;
    [SerializeField] private Transform rightUpperLeg;
    [SerializeField] private Transform rightLowerLeg;

    [Header("Кости Пальцев Левой Руки")]
    [SerializeField] private Transform l_wrist;
    [SerializeField] private Transform l_thumb1; [SerializeField] private Transform l_thumb2; [SerializeField] private Transform l_thumb3; [SerializeField] private Transform l_thumb4;
    [SerializeField] private Transform l_index1; [SerializeField] private Transform l_index2; [SerializeField] private Transform l_index3; [SerializeField] private Transform l_index4;
    [SerializeField] private Transform l_middle1; [SerializeField] private Transform l_middle2; [SerializeField] private Transform l_middle3; [SerializeField] private Transform l_middle4;
    [SerializeField] private Transform l_ring1; [SerializeField] private Transform l_ring2; [SerializeField] private Transform l_ring3; [SerializeField] private Transform l_ring4;
    [SerializeField] private Transform l_pinky1; [SerializeField] private Transform l_pinky2; [SerializeField] private Transform l_pinky3; [SerializeField] private Transform l_pinky4;

    [Header("Кости Пальцев Правой Руки")]
    [SerializeField] private Transform r_wrist;
    [SerializeField] private Transform r_thumb1; [SerializeField] private Transform r_thumb2; [SerializeField] private Transform r_thumb3; [SerializeField] private Transform r_thumb4;
    [SerializeField] private Transform r_index1; [SerializeField] private Transform r_index2; [SerializeField] private Transform r_index3; [SerializeField] private Transform r_index4;
    [SerializeField] private Transform r_middle1; [SerializeField] private Transform r_middle2; [SerializeField] private Transform r_middle3; [SerializeField] private Transform r_middle4;
    [SerializeField] private Transform r_ring1; [SerializeField] private Transform r_ring2; [SerializeField] private Transform r_ring3; [SerializeField] private Transform r_ring4;
    [SerializeField] private Transform r_pinky1; [SerializeField] private Transform r_pinky2; [SerializeField] private Transform r_pinky3; [SerializeField] private Transform r_pinky4;

    [Header("Настройки Мимики (Блендшейпы)")]
    [Tooltip("Перетащите сюда объект с компонентом SkinnedMeshRenderer, на котором находятся блендшейпы лица.")]
    [SerializeField] private SkinnedMeshRenderer faceMeshRenderer;

    [Tooltip("Найдите в списке блендшейпов номер, отвечающий за моргание")]
    [SerializeField] private int blinkBlendshapeIndex = -1;
    
    [Tooltip("Найдите в списке блендшейпов номер, отвечающий за открытие рта ")]
    [SerializeField] private int mouthOpenBlendshapeIndex = -1;

    [Header("Тонкая настройка мимики")]
    [SerializeField, Range(0f, 0.1f)] private float minBlinkDistance = 0.01f;
    [SerializeField, Range(0f, 0.1f)] private float maxBlinkDistance = 0.04f;
    [SerializeField, Range(0f, 0.2f)] private float minMouthOpenDistance = 0.02f;
    [SerializeField, Range(0f, 0.2f)] private float maxMouthOpenDistance = 0.1f;

    [Header("Настройки")]
    [SerializeField] private bool mirrorX = true;
    [SerializeField, Range(0.01f, 1.0f)] private float smoothFactor = 0.5f;
    [SerializeField] private float modelScale = 100.0f;

    [Header("ГЛАВНЫЕ НАСТРОЙКИ ВРАЩЕНИЯ")]
    [SerializeField] private Vector3 modelRotationOffset = new Vector3(0, 180, 0);
    [SerializeField] private Vector3 armRotationCorrection = new Vector3(0, 90, 0);
    [SerializeField] private Vector3 legRotationCorrection = new Vector3(270, 0, 0);
    
    [Header("Коррекция Вращения Пальцев")]
    [SerializeField] private Vector3 fingerRotationCorrection = new Vector3(0, 0, 0);

    private readonly List<Landmark> currentLandmarks = new List<Landmark>(33);
    private Quaternion initialHipsRotation;
    private Quaternion initialSpineRotation;
    private Quaternion modelCorrectionQuat;

    private AvatarTree L_WristTree;
    private AvatarTree R_WristTree;
    #endregion

    void Start()
    {
        initialHipsRotation = hips != null ? hips.rotation : Quaternion.identity;
        initialSpineRotation = spine != null ? spine.rotation : Quaternion.identity;
        BuildHandTrees();
    }

    void LateUpdate()
    {
        if (HolisticTrackingSolution.instance == null) return;
        modelCorrectionQuat = Quaternion.Euler(modelRotationOffset);

        var poseLandmarks = HolisticTrackingSolution.instance.LastPoseWorldLandmarks;
        if (poseLandmarks != null && poseLandmarks.Landmark != null && poseLandmarks.Landmark.Count > 0) { UpdateBody(poseLandmarks); }

        var leftHandLandmarks = HolisticTrackingSolution.instance.LastLeftHandLandmarks;
        if (leftHandLandmarks != null && L_WristTree != null) { UpdateHand(L_WristTree, leftHandLandmarks, true); }

        var rightHandLandmarks = HolisticTrackingSolution.instance.LastRightHandLandmarks;
        if (rightHandLandmarks != null && R_WristTree != null) { UpdateHand(R_WristTree, rightHandLandmarks, false); }

        var faceLandmarks = HolisticTrackingSolution.instance.LastFaceLandmarks;
        if (faceLandmarks != null && faceMeshRenderer != null) { UpdateFace(faceLandmarks); }
    }
    
    #region  Body Tracking 
    void UpdateBody(LandmarkList landmarkList)
    {
        currentLandmarks.Clear();
        currentLandmarks.AddRange(landmarkList.Landmark);
        if (currentLandmarks.Count < 33) return;
        try
        {
            Vector3 lShoulder = GetWorldPosition(11), rShoulder = GetWorldPosition(12);
            Vector3 lHip = GetWorldPosition(23), rHip = GetWorldPosition(24);
            Vector3 lElbow = GetWorldPosition(13), rElbow = GetWorldPosition(14);
            Vector3 lWristPos = GetWorldPosition(15), rWristPos = GetWorldPosition(16);
            Vector3 lKnee = GetWorldPosition(25), rKnee = GetWorldPosition(26);
            Vector3 lAnkle = GetWorldPosition(27), rAnkle = GetWorldPosition(28);
            Vector3 nose = GetWorldPosition(0);
            Vector3 hipCenter = (lHip + rHip) * 0.5f;
            Vector3 scaledHipCenter = hipCenter * modelScale;
            hips.position = Vector3.Lerp(hips.position, modelCorrectionQuat * scaledHipCenter, smoothFactor);
            Vector3 shoulderCenter = (lShoulder + rShoulder) * 0.5f;
            Vector3 spineDir = (shoulderCenter - hipCenter).normalized;
            Vector3 shoulderDir = (rShoulder - lShoulder).normalized;
            Vector3 torsoFwd = Vector3.Cross(spineDir, shoulderDir).normalized;
            if (spineDir.sqrMagnitude > 0.01f)
            {
                Quaternion torsoRotation = Quaternion.LookRotation(-torsoFwd, spineDir);
                hips.rotation = Quaternion.Slerp(hips.rotation, modelCorrectionQuat * torsoRotation * initialHipsRotation, smoothFactor);
                spine.rotation = Quaternion.Slerp(spine.rotation, modelCorrectionQuat * torsoRotation * initialSpineRotation, smoothFactor);
            }
            if (head != null && (nose - shoulderCenter).sqrMagnitude > 0.01f)
            {
                Vector3 headDir = (nose - shoulderCenter).normalized;
                Quaternion headRotation = Quaternion.LookRotation(headDir, spineDir);
                head.rotation = Quaternion.Slerp(head.rotation, modelCorrectionQuat * headRotation, smoothFactor);
            }
            Vector3 torsoRight = Vector3.Cross(spineDir, -torsoFwd).normalized;
            Quaternion armCorrection = Quaternion.Euler(armRotationCorrection);
            ApplyArmIK(leftUpperArm, leftLowerArm, lShoulder, lElbow, lWristPos, torsoRight, -torsoFwd, armCorrection, true);
            ApplyArmIK(rightUpperArm, rightLowerArm, rShoulder, rElbow, rWristPos, torsoRight, -torsoFwd, armCorrection, false);
            Quaternion legCorrection = Quaternion.Euler(legRotationCorrection);
            ApplyLegIK(leftUpperLeg, leftLowerLeg, lHip, lKnee, lAnkle, torsoRight, torsoFwd, legCorrection, true);
            ApplyLegIK(rightUpperLeg, rightLowerLeg, rHip, rKnee, rAnkle, torsoRight, torsoFwd, legCorrection, false);
        }
        catch (ArgumentOutOfRangeException e) { Debug.LogWarning($"Пропущен кадр тела: {e.Message}"); }
    }

    private void ApplyArmIK(Transform upper, Transform lower, Vector3 start, Vector3 mid, Vector3 end, Vector3 bodyRight, Vector3 bodyFwd, Quaternion correction, bool isLeft)
    {
        Vector3 upperDir = (mid - start).normalized;
        if (upperDir.sqrMagnitude < 0.001f) return;
        Vector3 poleVector = isLeft ? -bodyRight : bodyRight;
        Vector3 upHint = Vector3.ProjectOnPlane(poleVector, upperDir).normalized;
        if (upHint.sqrMagnitude < 0.001f) { upHint = Vector3.ProjectOnPlane(bodyFwd, upperDir).normalized; }
        Quaternion upperRotation = Quaternion.LookRotation(upperDir, upHint);
        upper.rotation = Quaternion.Slerp(upper.rotation, modelCorrectionQuat * upperRotation * correction, smoothFactor);
        if (lower != null)
        {
            Vector3 lowerDir = (end - mid).normalized;
            if (lowerDir.sqrMagnitude > 0.001f)
            {
                Quaternion lowerRotation = Quaternion.LookRotation(lowerDir, upHint);
                lower.rotation = Quaternion.Slerp(lower.rotation, modelCorrectionQuat * lowerRotation * correction, smoothFactor);
            }
        }
    }

    private void ApplyLegIK(Transform upper, Transform lower, Vector3 start, Vector3 mid, Vector3 end, Vector3 bodyRight, Vector3 bodyFwd, Quaternion correction, bool isLeft)
    {
        Vector3 upperDir = (mid - start).normalized;
        if (upperDir.sqrMagnitude < 0.001f) return;
        Vector3 poleVector = bodyFwd;
        Vector3 upHint = Vector3.ProjectOnPlane(poleVector, upperDir).normalized;
        if (upHint.sqrMagnitude < 0.001f) { upHint = Vector3.ProjectOnPlane(isLeft ? -bodyRight : bodyRight, upperDir).normalized; }
        Quaternion upperRotation = Quaternion.LookRotation(upperDir, upHint);
        upper.rotation = Quaternion.Slerp(upper.rotation, modelCorrectionQuat * upperRotation * correction, smoothFactor);
        if (lower != null)
        {
            Vector3 lowerDir = (end - mid).normalized;
            if (lowerDir.sqrMagnitude > 0.001f)
            {
                Quaternion lowerRotation = Quaternion.LookRotation(lowerDir, upHint);
                lower.rotation = Quaternion.Slerp(lower.rotation, modelCorrectionQuat * lowerRotation * correction, smoothFactor);
            }
        }
    }
    #endregion
    
    #region  Hand and Face Tracking 
    private void UpdateHand(AvatarTree wristNode, NormalizedLandmarkList landmarks, bool isLeft)
    {
        if (wristNode?.transf == null || landmarks.Landmark.Count < 21) return;
        
        // --- ИЗМЕНЕНИЕ ЗДЕСЬ: Получаем трансформ предплечья ---
        Transform forearm = isLeft ? leftLowerArm : rightLowerArm;
        if (forearm == null) return; // Если предплечье не назначено, ничего не делаем

        Quaternion fingerCorrection = Quaternion.Euler(fingerRotationCorrection);
        
        // Сначала ориентируем само запястье
        Vector3 wristPos = GetScreenPosition(landmarks, 0, isLeft);
        Vector3 middleBasePos = GetScreenPosition(landmarks, 9, isLeft);
        Vector3 handForward = (middleBasePos - wristPos).normalized;

        if (handForward != Vector3.zero)
        {
            // Используем forearm.up как стабильный ориентир "вверх"
            Quaternion wristRotation = Quaternion.LookRotation(handForward, forearm.up);
            wristNode.transf.rotation = Quaternion.Slerp(wristNode.transf.rotation, wristRotation * fingerCorrection, smoothFactor);
        }

        // Затем обновляем каждый палец, передавая ему стабильный ориентир
        if (wristNode.childs != null)
        {
            foreach (var fingerBase in wristNode.childs)
            {
                if (fingerBase != null) { UpdateFinger(fingerBase, landmarks, isLeft, forearm.up, fingerCorrection); }
            }
        }
    }
    
    private void UpdateFinger(AvatarTree node, NormalizedLandmarkList landmarks, bool isLeft, Vector3 stableUp, Quaternion fingerCorrection)
    {
        if (node?.transf == null) return;
        
        // Сначала обновляем дочерние кости
        if (node.childs != null) { foreach (var child in node.childs) { if (child != null) { UpdateFinger(child, landmarks, isLeft, stableUp, fingerCorrection); } } }
        
        // Поворачиваем родительскую кость так, чтобы она смотрела на текущую
        if (node.parent != null && node.parent.transf != null)
        {
            Vector3 parentPos = GetScreenPosition(landmarks, node.parent.idx, isLeft);
            Vector3 currentPos = GetScreenPosition(landmarks, node.idx, isLeft);
            Vector3 direction = (currentPos - parentPos).normalized;
            
            if (direction == Vector3.zero) return;
            
            // Используем стабильный 'stableUp', полученный от предплечья
            Quaternion rotation = Quaternion.LookRotation(direction, stableUp);
            node.parent.transf.rotation = Quaternion.Slerp(node.parent.transf.rotation, rotation * fingerCorrection, smoothFactor);
        }
    }
    
    void UpdateFace(NormalizedLandmarkList landmarks)
    {
        if (landmarks.Landmark.Count < 468) return;

        if (blinkBlendshapeIndex != -1)
        {
            float rightEyeTop = landmarks.Landmark[159].Y;
            float rightEyeBottom = landmarks.Landmark[145].Y;
            float rightEyeDistance = Mathf.Abs(rightEyeTop - rightEyeBottom);
            float leftEyeTop = landmarks.Landmark[386].Y;
            float leftEyeBottom = landmarks.Landmark[374].Y;
            float leftEyeDistance = Mathf.Abs(leftEyeTop - leftEyeBottom);
            float avgBlink = (rightEyeDistance + leftEyeDistance) / 2.0f;
            float blinkValue = Mathf.InverseLerp(maxBlinkDistance, minBlinkDistance, avgBlink) * 100f;
            faceMeshRenderer.SetBlendShapeWeight(blinkBlendshapeIndex, blinkValue);
        }

        if (mouthOpenBlendshapeIndex != -1)
        {
            float upperLip = landmarks.Landmark[13].Y;
            float lowerLip = landmarks.Landmark[14].Y;
            float mouthDistance = Mathf.Abs(upperLip - lowerLip);
            float mouthOpenValue = Mathf.InverseLerp(minMouthOpenDistance, maxMouthOpenDistance, mouthDistance) * 100f;
            faceMeshRenderer.SetBlendShapeWeight(mouthOpenBlendshapeIndex, mouthOpenValue);
        }
    }
    #endregion

    #region Utility and Setup 
    private Vector3 GetWorldPosition(int index)
    {
        var lm = currentLandmarks[index];
        float x = lm.X * (mirrorX ? -1 : 1);
        float y = -lm.Y;
        float z = -lm.Z;
        return new Vector3(x, y, z);
    }
    
    private Vector3 GetScreenPosition(NormalizedLandmarkList landmarks, int index, bool isLeft)
    {
        var lm = landmarks.Landmark[index];
        bool mirrorHand = isLeft ? mirrorX : !mirrorX;
        float x = lm.X * (mirrorHand ? -1 : 1);
        float y = -lm.Y;
        float z = -lm.Z;
        return new Vector3(x, y, z);
    }
    
    private void BuildHandTrees()
    {
        if (l_wrist != null)
        {
            L_WristTree = new AvatarTree(l_wrist, 5, 0);
            if(l_thumb1) L_WristTree.childs[0] = new AvatarTree(l_thumb1, 1, 1, L_WristTree);
            if(l_thumb2 && L_WristTree.childs[0] != null) L_WristTree.childs[0].childs[0] = new AvatarTree(l_thumb2, 1, 2, L_WristTree.childs[0]);
            if(l_thumb3 && L_WristTree.childs[0]?.childs[0] != null) L_WristTree.childs[0].childs[0].childs[0] = new AvatarTree(l_thumb3, 1, 3, L_WristTree.childs[0].childs[0]);
            if(l_thumb4 && L_WristTree.childs[0]?.childs[0]?.childs[0] != null) L_WristTree.childs[0].childs[0].childs[0].childs[0] = new AvatarTree(l_thumb4, 0, 4, L_WristTree.childs[0].childs[0].childs[0]);
            if(l_index1) L_WristTree.childs[1] = new AvatarTree(l_index1, 1, 5, L_WristTree);
            if(l_index2 && L_WristTree.childs[1] != null) L_WristTree.childs[1].childs[0] = new AvatarTree(l_index2, 1, 6, L_WristTree.childs[1]);
            if(l_index3 && L_WristTree.childs[1]?.childs[0] != null) L_WristTree.childs[1].childs[0].childs[0] = new AvatarTree(l_index3, 1, 7, L_WristTree.childs[1].childs[0]);
            if(l_index4 && L_WristTree.childs[1]?.childs[0]?.childs[0] != null) L_WristTree.childs[1].childs[0].childs[0].childs[0] = new AvatarTree(l_index4, 0, 8, L_WristTree.childs[1].childs[0].childs[0]);
            if(l_middle1) L_WristTree.childs[2] = new AvatarTree(l_middle1, 1, 9, L_WristTree);
            if(l_middle2 && L_WristTree.childs[2] != null) L_WristTree.childs[2].childs[0] = new AvatarTree(l_middle2, 1, 10, L_WristTree.childs[2]);
            if(l_middle3 && L_WristTree.childs[2]?.childs[0] != null) L_WristTree.childs[2].childs[0].childs[0] = new AvatarTree(l_middle3, 1, 11, L_WristTree.childs[2].childs[0]);
            if(l_middle4 && L_WristTree.childs[2]?.childs[0]?.childs[0] != null) L_WristTree.childs[2].childs[0].childs[0].childs[0] = new AvatarTree(l_middle4, 0, 12, L_WristTree.childs[2].childs[0].childs[0]);
            if(l_ring1) L_WristTree.childs[3] = new AvatarTree(l_ring1, 1, 13, L_WristTree);
            if(l_ring2 && L_WristTree.childs[3] != null) L_WristTree.childs[3].childs[0] = new AvatarTree(l_ring2, 1, 14, L_WristTree.childs[3]);
            if(l_ring3 && L_WristTree.childs[3]?.childs[0] != null) L_WristTree.childs[3].childs[0].childs[0] = new AvatarTree(l_ring3, 1, 15, L_WristTree.childs[3].childs[0]);
            if(l_ring4 && L_WristTree.childs[3]?.childs[0]?.childs[0] != null) L_WristTree.childs[3].childs[0].childs[0].childs[0] = new AvatarTree(l_ring4, 0, 16, L_WristTree.childs[3].childs[0].childs[0]);
            if(l_pinky1) L_WristTree.childs[4] = new AvatarTree(l_pinky1, 1, 17, L_WristTree);
            if(l_pinky2 && L_WristTree.childs[4] != null) L_WristTree.childs[4].childs[0] = new AvatarTree(l_pinky2, 1, 18, L_WristTree.childs[4]);
            if(l_pinky3 && L_WristTree.childs[4]?.childs[0] != null) L_WristTree.childs[4].childs[0].childs[0] = new AvatarTree(l_pinky3, 1, 19, L_WristTree.childs[4].childs[0]);
            if(l_pinky4 && L_WristTree.childs[4]?.childs[0]?.childs[0] != null) L_WristTree.childs[4].childs[0].childs[0].childs[0] = new AvatarTree(l_pinky4, 0, 20, L_WristTree.childs[4].childs[0].childs[0]);
        }
        if (r_wrist != null)
        {
            R_WristTree = new AvatarTree(r_wrist, 5, 0);
            if(r_thumb1) R_WristTree.childs[0] = new AvatarTree(r_thumb1, 1, 1, R_WristTree);
            if(r_thumb2 && R_WristTree.childs[0] != null) R_WristTree.childs[0].childs[0] = new AvatarTree(r_thumb2, 1, 2, R_WristTree.childs[0]);
            if(r_thumb3 && R_WristTree.childs[0]?.childs[0] != null) R_WristTree.childs[0].childs[0].childs[0] = new AvatarTree(r_thumb3, 1, 3, R_WristTree.childs[0].childs[0]);
            if(r_thumb4 && R_WristTree.childs[0]?.childs[0]?.childs[0] != null) R_WristTree.childs[0].childs[0].childs[0].childs[0] = new AvatarTree(r_thumb4, 0, 4, R_WristTree.childs[0].childs[0].childs[0]);
            if(r_index1) R_WristTree.childs[1] = new AvatarTree(r_index1, 1, 5, R_WristTree);
            if(r_index2 && R_WristTree.childs[1] != null) R_WristTree.childs[1].childs[0] = new AvatarTree(r_index2, 1, 6, R_WristTree.childs[1]);
            if(r_index3 && R_WristTree.childs[1]?.childs[0] != null) R_WristTree.childs[1].childs[0].childs[0] = new AvatarTree(r_index3, 1, 7, R_WristTree.childs[1].childs[0]);
            if(r_index4 && R_WristTree.childs[1]?.childs[0]?.childs[0] != null) R_WristTree.childs[1].childs[0].childs[0].childs[0] = new AvatarTree(r_index4, 0, 8, R_WristTree.childs[1].childs[0].childs[0]);
            if(r_middle1) R_WristTree.childs[2] = new AvatarTree(r_middle1, 1, 9, R_WristTree);
            if(r_middle2 && R_WristTree.childs[2] != null) R_WristTree.childs[2].childs[0] = new AvatarTree(r_middle2, 1, 10, R_WristTree.childs[2]);
            if(r_middle3 && R_WristTree.childs[2]?.childs[0] != null) R_WristTree.childs[2].childs[0].childs[0] = new AvatarTree(r_middle3, 1, 11, R_WristTree.childs[2].childs[0]);
            if(r_middle4 && R_WristTree.childs[2]?.childs[0]?.childs[0] != null) R_WristTree.childs[2].childs[0].childs[0].childs[0] = new AvatarTree(r_middle4, 0, 12, R_WristTree.childs[2].childs[0].childs[0]);
            if(r_ring1) R_WristTree.childs[3] = new AvatarTree(r_ring1, 1, 13, R_WristTree);
            if(r_ring2 && R_WristTree.childs[3] != null) R_WristTree.childs[3].childs[0] = new AvatarTree(r_ring2, 1, 14, R_WristTree.childs[3]);
            if(r_ring3 && R_WristTree.childs[3]?.childs[0] != null) R_WristTree.childs[3].childs[0].childs[0] = new AvatarTree(r_ring3, 1, 15, R_WristTree.childs[3].childs[0]);
            if(r_ring4 && R_WristTree.childs[3]?.childs[0]?.childs[0] != null) R_WristTree.childs[3].childs[0].childs[0].childs[0] = new AvatarTree(r_ring4, 0, 16, R_WristTree.childs[3].childs[0].childs[0]);
            if(r_pinky1) R_WristTree.childs[4] = new AvatarTree(r_pinky1, 1, 17, R_WristTree);
            if(r_pinky2 && R_WristTree.childs[4] != null) R_WristTree.childs[4].childs[0] = new AvatarTree(r_pinky2, 1, 18, R_WristTree.childs[4]);
            if(r_pinky3 && R_WristTree.childs[4]?.childs[0] != null) R_WristTree.childs[4].childs[0].childs[0] = new AvatarTree(r_pinky3, 1, 19, R_WristTree.childs[4].childs[0]);
            if(r_pinky4 && R_WristTree.childs[4]?.childs[0]?.childs[0] != null) R_WristTree.childs[4].childs[0].childs[0].childs[0] = new AvatarTree(r_pinky4, 0, 20, R_WristTree.childs[4].childs[0].childs[0]);
        }
    }
    #endregion
}