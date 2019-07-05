using System;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class AnimatorTool : MonoBehaviour
{
    private static List<AnimatorState> stateList = new List<AnimatorState>();
    /// <summary>
    /// 菜单方法，遍历文件夹创建Animation Controller
    /// </summary>
    [MenuItem("Tools/CreateAnimator")]
    static void CreateAnimationAssets()
    {
        string rootFolder = "Assets/Resources/Fbx/";
        if (!Directory.Exists(rootFolder))
        {
            Directory.CreateDirectory(rootFolder);
            return;
        }
        // 遍历目录，查找生成controller文件
        var folders = Directory.GetDirectories(rootFolder);
        foreach (var folder in folders)
        {
            DirectoryInfo info = new DirectoryInfo(folder);
            string folderName = info.Name;
            // 创建animationController文件
            AnimatorController aController =
                AnimatorController.CreateAnimatorControllerAtPath(string.Format("{0}/animation.controller", folder));  //在对应目录生成AnimatorController文件

            //添加参数
            aController.AddParameter("run", AnimatorControllerParameterType.Bool);
            aController.AddParameter("attack01", AnimatorControllerParameterType.Bool);

            // 得到其layer
            var layer = aController.layers[0];//Base Layer
            // 绑定动画文件
            AddStateTranstion(string.Format("{0}/{1}_model.fbx", folder, folderName), layer);
            Debug.Log(string.Format("<color=yellow>{0}</color>", layer));
            // 创建预设
            GameObject go = LoadFbx(folderName);
            PrefabUtility.CreatePrefab(string.Format("{0}/{1}.prefab", folder, folderName), go);
            DestroyImmediate(go);
        }

    }

    /// <summary>
    /// 添加动画状态机状态
    /// </summary>
    /// <param name="path"></param>
    /// <param name="layer"></param>
    private static void AddStateTranstion(string path, AnimatorControllerLayer layer)
    {
        AnimatorStateMachine sm = layer.stateMachine;  //状态机
        // 根据动画文件读取它的AnimationClip对象
        var datas = AssetDatabase.LoadAllAssetsAtPath(path);
        if (datas.Length == 0)
        {
            Debug.Log(string.Format("Can't find clip in {0}", path));
            return;
        }
        /*
        //创建默认state  
        AnimatorState defaultState = sm.AddState("default", new Vector3(300, 0, 0));
        //defaultState.motion=  
        sm.defaultState = defaultState;
        AnimatorStateTransition defaultTransition = sm.AddAnyStateTransition(defaultState);
        defaultTransition.AddCondition(AnimatorConditionMode.If, 0, "default");
        */
        // 先添加一个默认的空状态
        var emptyState = sm.AddState("empty", new Vector3(500, 0, 0));
        sm.AddAnyStateTransition(emptyState);

        //遍历模型中包含的动画片段，将其加入状态机中
        foreach (var data in datas)
        {
            int index = 0;
            if (!(data is AnimationClip)) //如果不是动画文件则跳过
                continue;
            var newClip = data as AnimationClip; //如果是的话则转化

            if (newClip.name.StartsWith("__"))
                continue;
            // 取出动画名字，添加到state里面
            AnimatorState state = sm.AddState(newClip.name, new Vector3(500, sm.states.Length * 60, 0)); //将动画添加到动画控制器
            stateList.Add(state);
            if (state.name == "walk")
            {
                sm.defaultState = state;   //将walk设置为默认动画
            }
            Debug.Log(string.Format("<color=red>{0}</color>", state));
            index++;
            state.motion = newClip; //设置动画状态指定到自己的动画文件
            // 把State添加在Layer里面
            sm.AddAnyStateTransition(state); //将动画状态连线到AnyState
        }

        AddTransition(sm, "walk", "run", 1);
        AddTransition(sm, "run", "walk", 0);

        AddTransition(sm, "walk", "attack01", 1);
        AddTransition(sm, "attack01", "walk", 0);

        AddSuMechie(sm, 2, path, layer, "sub2Machine");
    }

    static void AddSuMechie(AnimatorStateMachine machine, int index1, string path, AnimatorControllerLayer layer, string sunStateMachine)
    {
        ////创建子状态机  
        //for (int k = 1; k < index1; k++)
        //{
        //    AnimatorStateMachine sub2Machine = machine.AddStateMachine("sub2Machine", new Vector3(100, 300, 0));
        //}
        AnimatorStateMachine sub2Machine = machine.AddStateMachine(sunStateMachine, new Vector3(100, 300, 0));

        // 根据动画文件读取它的AnimationClip对象
        var datas = AssetDatabase.LoadAllAssetsAtPath(path);
        if (datas.Length == 0)
        {
            Debug.Log(string.Format("Can't find clip in {0}", path));
            return;
        }
        foreach (var data in datas)
        {
            int index = 0;
            if (!(data is AnimationClip))
                continue;
            var newClip = data as AnimationClip;

            if (newClip.name.StartsWith("__"))
                continue;
            // 取出动画名字，添加到state里面
            AnimatorState state = sub2Machine.AddState(newClip.name, new Vector3(500, sub2Machine.states.Length * 60, 0));
            stateList.Add(state);
            if (state.name == "walk")
            {
                sub2Machine.defaultState = state;
            }
            Debug.Log(string.Format("<color=red>{0}</color>", state));
            index++;
            state.motion = newClip;
            // 把State添加在Layer里面
            sub2Machine.AddAnyStateTransition(state);
        }
    }

    /// <summary>
    /// 添加状态之间的连线
    /// </summary>
    /// <param name="stateM">状态</param>
    /// <param name="ani_name"></param>
    /// <param name="ani_des"></param>
    /// <param name="flag"></param>
    static void AddTransition(AnimatorStateMachine stateM, string ani_name, string ani_des, int flag)
    {
        foreach (var item in stateM.states)
        {
            if (item.state.name == ani_name)
            {
                foreach (var des in stateM.states)
                {
                    if (des.state.name == ani_des)
                    {
                        AnimatorStateTransition transition = item.state.AddTransition(des.state); //添加连线
                        transition.hasExitTime = true;
                        transition.exitTime = 0.8f;
                        if (flag == 1)
                            transition.AddCondition(AnimatorConditionMode.If, flag, ani_des); //添加连线状态
                        else
                        {
                            transition.AddCondition(AnimatorConditionMode.IfNot, flag, ani_name);
                        }
                    }
                }
            }
        }
        Resources.UnloadUnusedAssets(); //卸载资源
    }


    /// <summary>
    /// 生成带动画控制器的对象
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static GameObject LoadFbx(string name)
    {
        var obj = Instantiate(Resources.Load(string.Format("Fbx/{0}/{0}_model", name))) as GameObject;
        obj.GetComponent<Animator>().runtimeAnimatorController =
            Resources.Load<RuntimeAnimatorController>(string.Format("fbx/{0}/animation", name));
        return obj;
    }
}
