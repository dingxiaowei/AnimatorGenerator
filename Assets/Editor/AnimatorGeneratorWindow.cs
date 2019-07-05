﻿using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using System;
using System.IO;
using System.Windows.Forms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimatorGeneratorWindow : OdinEditorWindow
{
    string fileDirectory;
    bool recursion = false;//是否是递归模式

    [UnityEditor.MenuItem("Tools/AnimatorGenerator")]
    private static void Open()
    {
        var window = GetWindow<AnimatorGeneratorWindow>();
        window.position = GUIHelper.GetEditorWindowRect().AlignCenter(450, 500);
    }
    [InlineEditor(InlineEditorModes.LargePreview)] //对材质和mesh有效，可以预览
    [LabelText("动画模板")]
    public AnimatorController AnimatorControllerTemplate;

    [Button("选择要生成的目录(递归遍历)", ButtonSizes.Medium), GUIColor(1, 1, 0)]
    [HorizontalGroup("ChooseMenu")]
    [EnableIf("AnimatorControllerTemplate", InfoMessageType.Error)]
    private void ChooseMenus()
    {
        Debug.Log("选择目录");
        FolderBrowserDialog fbd = new FolderBrowserDialog();
        fbd.RootFolder = Environment.SpecialFolder.MyComputer;
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            fileDirectory = fbd.SelectedPath;
            recursion = true;
        }
        Debug.Log("选择目录:" + fileDirectory);
    }

    [Button("选择要生成的目录(单文件目录)", ButtonSizes.Medium), GUIColor(1, 1, 0)]
    [HorizontalGroup("ChooseMenu")]
    [EnableIf("AnimatorControllerTemplate", InfoMessageType.Error)]
    private void ChooseMenu()
    {
        Debug.Log("选择目录");
        FolderBrowserDialog fbd = new FolderBrowserDialog();
        fbd.RootFolder = Environment.SpecialFolder.MyComputer;
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            fileDirectory = fbd.SelectedPath;
            recursion = false;
        }
        Debug.Log("选择目录:" + fileDirectory);
    }

    [Button("生成动画控制器", ButtonSizes.Medium), GUIColor(0, 1, 1)]
    [HorizontalGroup("Buttons")]
    [EnableIf("AnimatorControllerTemplate", InfoMessageType.Error)]
    private void GeneratorAnimatorButton()
    {
        Debug.Log("生成动画控制器");
        CreateAnimatorAssets();
    }


    private void CreateAnimatorAssets()
    {
        if (!Directory.Exists(fileDirectory))
        {
            throw new Exception("目录不存在或者路径不存在");
        }
        if (AnimatorControllerTemplate == null)
        {
            Debug.LogError("没有选择动画模板");
            return;
        }

        var animatorFilePath = AssetDatabase.GetAssetPath(AnimatorControllerTemplate);
        var dirArray = fileDirectory.Split('\\');
        var pathLastDirectoryName = dirArray[dirArray.Length - 1];
        var animatorExtension = Path.GetExtension(animatorFilePath);

        if (recursion)
        {
            var folders = Directory.GetDirectories(fileDirectory);
            foreach (var folder in folders)
            {
                SingleFolderDispose(folder, animatorFilePath);
            }
        }
        else
        {
            SingleFolderDispose(fileDirectory, animatorFilePath);
        }
    }


    private void SingleFolderDispose(string folder, string animatorFilePath)
    {
        DirectoryInfo info = new DirectoryInfo(folder);
        string folderName = info.Name;
        var newAnimatorFilePath = Path.Combine(folder, folderName + Path.GetExtension(animatorFilePath));
        File.Copy(animatorFilePath, newAnimatorFilePath, true);
        AssetDatabase.Refresh();
        AnalyzeAnimController(folder, newAnimatorFilePath);
        AssetDatabase.Refresh();
        var obj = LoadFbx(folder, newAnimatorFilePath);
        PrefabUtility.SaveAsPrefabAsset(obj, string.Format("{0}/{1}.prefab", folder, folderName));
        DestroyImmediate(obj);
    }

    private GameObject LoadFbx(string folder, string animatorFilePath)
    {
        //找到fbx,找到当前目录下名字不带@符号的fbx 进行实例化
        FileInfo tempFile = null;
        DirectoryInfo folderDirectoryInfo = new DirectoryInfo(folder);
        var files = folderDirectoryInfo.GetFiles();
        foreach (var fileInfo in files)
        {
            if (!fileInfo.Name.Contains("@") && fileInfo.Name.Contains(".fbx") && !fileInfo.Name.Contains(".meta"))   //TODO:也可以根据floder名去找对应的fbx
            {
                tempFile = fileInfo;
                break;
            }
        }
        if (tempFile == null)
        {
            throw new Exception(string.Format("目录：{0} 没有找到不带@的fbx", folder));
        }

        var obj = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GetAssetPath(tempFile.FullName))) as GameObject;
        //找到controller

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GetAssetPath(animatorFilePath));
        obj.GetComponent<Animator>().runtimeAnimatorController = controller;
        return obj;
    }

    private void AnalyzeAnimController(string floder, string controllerPath)
    {
        var assetPath = GetAssetPath(controllerPath);
        var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
        string animationFolder = Path.GetDirectoryName(assetPath) + "\\animations";
        animationFolder.Replace("\\", "/");
        //animatorController的Parameters不需要修改
        //遍历所有的layer
        for (int i = 0; i < animatorController.layers.Length; i++)
        {
            var layer = animatorController.layers[i];
            AnimatorStateMachine sm = layer.stateMachine;
            RecursionAnalyzeAnimatorStateMachine(sm, animationFolder);
        }
    }

    private string GetAssetPath(string fullPath)
    {
        var strs = fullPath.Split(new string[] { "Assets" }, StringSplitOptions.None);
        var assetPath = "Assets" + strs[strs.Length - 1];
        assetPath.Replace("\\", "/");
        return assetPath;
    }

    private void RecursionAnalyzeAnimatorStateMachine(AnimatorStateMachine stateMachine, string animationFlolder)
    {
        //遍历states
        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            var animatorState = stateMachine.states[i];
            var motion = animatorState.state.motion;
            if (motion != null)
            {
                if (motion is BlendTree)
                {
                    BlendTree bt = motion as BlendTree;

                    ChildMotion[] childMotions = new ChildMotion[bt.children.Length];


                    for (int j = 0; j < bt.children.Length; j++)
                    {
                        var childMotion = bt.children[j];
                        var motionClip = GetAnimationClip(childMotion.motion.name, animationFlolder);

                        if (motionClip == null)
                        {
                            Debug.LogError("没有找到" + motion.name + "的动画控制器");
                        }
                        else
                        {
                            Debug.Log(string.Format("Name:{0}  Motion:{1}", animatorState.state.name, childMotion.motion));   //根据名字找到对应的prefab 然后找出里面的动画文件加载
                            //childMotion.motion = (Motion)motionClip;

                            //var newChildMotion = new ChildMotion() { motion = motionClip, cycleOffset = childMotion.cycleOffset, mirror = childMotion.mirror, directBlendParameter = childMotion.directBlendParameter, position = childMotion.position, threshold = childMotion.threshold, timeScale = childMotion.timeScale };
                            //childMotion = newChildMotion;

                            childMotions[j] = new ChildMotion() { motion = (Motion)motionClip, cycleOffset = childMotion.cycleOffset, mirror = childMotion.mirror, directBlendParameter = childMotion.directBlendParameter, position = childMotion.position, threshold = childMotion.threshold, timeScale = childMotion.timeScale };
                        }
                    }
                    //bt.children = childMotions;
                    BlendTree newBt = new BlendTree()
                    {
                        blendParameter = bt.blendParameter,
                        blendParameterY = bt.blendParameterY,
                        blendType = bt.blendType,
                        hideFlags = bt.hideFlags,
                        maxThreshold = bt.maxThreshold,
                        minThreshold = bt.minThreshold,
                        name = bt.name,
                        useAutomaticThresholds = bt.useAutomaticThresholds,
                        children = childMotions,
                    };
                    animatorState.state.motion = newBt;
                }
                else
                {
                    animatorState.state.motion = null;
                    var motionClip = GetAnimationClip(motion.name, animationFlolder);
                    if (motionClip == null)
                    {
                        Debug.LogError("没有找到" + motion.name + "的动画控制器");
                    }
                    else
                    {
                        animatorState.state.motion = (Motion)motionClip;
                        Debug.Log(string.Format("Name:{0}  Motion:{1}", animatorState.state.name, motion));
                    }
                }
            }
        }
        //遍历substatemachine
        for (int j = 0; j < stateMachine.stateMachines.Length; j++)
        {
            var stateMachines = stateMachine.stateMachines[j];
            RecursionAnalyzeAnimatorStateMachine(stateMachines.stateMachine, animationFlolder);
        }
    }

    private AnimationClip GetAnimationClip(string motionName, string animationFolder)
    {
        var motionNameExt = motionName.Substring(motionName.IndexOf("_"));
        DirectoryInfo directoryInfo = new DirectoryInfo(animationFolder);
        FileInfo tempFileInfo = null;
        var files = directoryInfo.GetFiles("*.FBX", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Name.EndsWith(motionNameExt + ".FBX"))    //有可能是Robert01_gun_jump_start  对应的Robert01@Robert01_gun_jump
            {
                tempFileInfo = files[i];
                break;
            }
        }
        if (tempFileInfo != null)
        {
            var datas = AssetDatabase.LoadAllAssetsAtPath(GetAssetPath(tempFileInfo.FullName));
            if (datas.Length == 0)
            {
                Debug.Log(string.Format("Can't find clip in {0}", tempFileInfo.FullName));
                return null;
            }
            foreach (var data in datas)
            {
                if (!(data is AnimationClip))//如果不是动画文件则跳过
                    continue;
                var newClip = data as AnimationClip;
                return newClip;
            }
        }
        else
        {
            Debug.LogError("没有找到对应的动画FBX:" + motionName);
        }
        return null;
    }
}