using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public class CameraPlayer : MonoBehaviour
    {
        private GeoTilesCesiumCameraController CameraController;
        private bool isEditMode;
        private List<CameraKeyFrame> keyFrames = new List<CameraKeyFrame>();
        private string animationFileName = "CameraAnimation.json";
        private string keyBindingConfigFileName = "CameraAnimationEditorKeyBindings.json";

        private double duration = 1;
        private double delay = 1;
        //private string durationInput = "1";
        //private string delayInput = "1";
        private int currentFrameNum = 0;

        //const int buttonHeight = 50;
        //const int buttonSpace = buttonHeight;

        //private Dictionary<KeyCode, Action> keyActionsEditMode = new Dictionary<KeyCode, Action>();
        private Dictionary<KeyCode, Action> keyActionsPlayMode = new Dictionary<KeyCode, Action>();

        //private Dictionary<KeyCode, GUIContent> buttonContentsEditMode = new Dictionary<KeyCode, GUIContent>();
        //private Dictionary<KeyCode, GUIContent> buttonContentsPlayMode = new Dictionary<KeyCode, GUIContent>();

        KeyBindingConfig keyBindingConfig = new KeyBindingConfig();

        public void UpdateDuration(double dur)
        {
            duration = dur;
        }
        public void UpdateDelay(double del)
        {
            delay = del;
        }

        public int GetCurrentFrameNum()
        {
            return currentFrameNum;
        }

        void Start()
        {
            if (CameraController == null)
            {
                CameraController = GetComponent<GeoTilesCesiumCameraController>();
            }

            LoadFile();

            //if (!LoadKeyBindings())
            //{
            //    LoadDefaultKeyBindings();
            //    SaveKeyBindings();
            //}
            //ParseKeyBindings(keyBindingConfig);

            LoadDefaultKeyBindings();
            ParseKeyBindings(keyBindingConfig);
        }

        //void OnGUI()
        //{
        //if (CameraController == null)
        //{
        //    return;
        //}

        //GUILayout.BeginVertical();

        //if (isEditMode)
        //{
        //    foreach (var keyAction in keyActionsEditMode)
        //    {
        //        if (buttonContentsEditMode.ContainsKey(keyAction.Key))
        //        {
        //            if (GUILayout.Button(buttonContentsEditMode[keyAction.Key], GUILayout.Height(buttonHeight)))
        //            {
        //                keyAction.Value.Invoke();
        //            }
        //        }
        //    }

        //    GUILayout.Label("动画跳转时间:", GUILayout.Width(100));
        //    durationInput = GUILayout.TextField(durationInput, 20);

        //    GUILayout.Space(buttonSpace);

        //    GUILayout.Label("等待时间:", GUILayout.Width(100));
        //    delayInput = GUILayout.TextField(delayInput, 20);

        //    GUILayout.Space(buttonSpace);

        //    GUILayout.Label("当前帧:" + currentFrameNum, GUILayout.Width(100));
        //    GUILayout.Label(GetCurrentFrameString(), GUILayout.Width(100));
        //}
        //else
        //{
        //    foreach (var keyAction in keyActionsPlayMode)
        //    {
        //        if (buttonContentsPlayMode.ContainsKey(keyAction.Key))
        //        {
        //            if (GUILayout.Button(buttonContentsPlayMode[keyAction.Key], GUILayout.Height(buttonHeight)))
        //            {
        //                keyAction.Value.Invoke();
        //            }
        //        }
        //    }
        //}

        //GUILayout.EndVertical();
        //}

        void Update()
        {
            if (CameraController == null)
            {
                return;
            }
            foreach (var keyAction in keyActionsPlayMode)
            {
                if (Input.GetKeyDown(keyAction.Key))
                {
                    keyAction.Value.Invoke();
                }
            }
            //if (isEditMode)
            //{
            //    foreach (var keyAction in keyActionsEditMode)
            //    {
            //        if (Input.GetKeyDown(keyAction.Key))
            //        {
            //            keyAction.Value.Invoke();
            //        }
            //    }
            //    double inputValue;
            //    if (double.TryParse(durationInput, out inputValue))
            //    {
            //        duration = inputValue;
            //    }
            //    if (double.TryParse(delayInput, out inputValue))
            //    {
            //        delay = inputValue;
            //    }
            //}
            //else
            //{
            //    foreach (var keyAction in keyActionsPlayMode)
            //    {
            //        if (Input.GetKeyDown(keyAction.Key))
            //        {
            //            keyAction.Value.Invoke();
            //        }
            //    }
            //}
        }

        /// <summary>
        /// 切换编辑模式
        /// </summary>
        public void ToggleEditMode()
        {
            isEditMode = !isEditMode;
        }

        /// <summary>
        /// 往后插一帧
        /// </summary>
        public void PushCurrentView()
        {
            // Add current camera state to keyframes
            if (currentFrameNum >= keyFrames.Count - 1)
            {
                keyFrames.Add(GetCurrentView());
                currentFrameNum = keyFrames.Count - 1;
            }
            else
            {
                keyFrames.Insert(++currentFrameNum, GetCurrentView());
            }
        }

        /// <summary>
        /// 插入当前视野到末尾
        /// </summary>
        public void PushbackCurrentView()
        {
            keyFrames.Add(GetCurrentView());
            currentFrameNum = keyFrames.Count - 1;
        }

        /// <summary>
        /// 往前插一帧
        /// </summary>
        public void InsertCurrentView()
        {
            if (keyFrames.Count == 0)
            {
                return;
            }
            keyFrames.Insert(currentFrameNum, GetCurrentView());
        }

        /// <summary>
        /// 移除当前帧
        /// </summary>
        public void RemoveCurrentFrame()
        {
            if (keyFrames.Count == 0)
            {
                return;
            }
            keyFrames.RemoveAt(currentFrameNum);
            currentFrameNum = Mathf.Clamp(currentFrameNum, 0, keyFrames.Count - 1);
        }

        /// <summary>
        /// 替换当前帧
        /// </summary>
        public void ReplaceCurrentFrame()
        {
            if (keyFrames.Count == 0)
            {
                return;
            }
            keyFrames[currentFrameNum] = GetCurrentView();
        }

        /// <summary>
        /// 清空所有帧
        /// </summary>
        public void CleanAllFrame()
        {
            keyFrames.Clear();
            currentFrameNum = 0;
        }

        /// <summary>
        /// 上一帧
        /// </summary>
        public void PreviousFrame()
        {
            if (keyFrames.Count == 0)
            {
                return;
            }
            currentFrameNum = Mathf.Clamp(currentFrameNum - 1, 0, keyFrames.Count - 1);
            PlayFrame(keyFrames[currentFrameNum]);
        }

        /// <summary>
        /// 下一帧
        /// </summary>
        public void NextFrame()
        {
            if (keyFrames.Count == 0)
            {
                return;
            }
            currentFrameNum = Mathf.Clamp(currentFrameNum + 1, 0, keyFrames.Count - 1);
            PlayFrame(keyFrames[currentFrameNum]);
        }

        CameraKeyFrame GetCurrentView()
        {
            return new CameraKeyFrame()
            {
                Action = CameraKeyFrame.AnimationAction.FlyTo,
                X = CameraController.LookCenterPoint.x,
                Y = CameraController.LookCenterPoint.y,
                Z = CameraController.LookCenterPoint.z,
                Distance = CameraController.CameraToLookCenterDistance,
                RotateAngle = CameraController.RotationAngle,
                SkewAngle = CameraController.SkewAngle,
                Duration = duration
            };
        }

        /// <summary>
        /// 插入等待间隔
        /// </summary>
        public void InsertDelay()
        {
            // Insert delay keyframe
            keyFrames.Add(new CameraKeyFrame() { Action = CameraKeyFrame.AnimationAction.Delay, Duration = delay });
            currentFrameNum = keyFrames.Count - 1;
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        public void SaveFile()
        {
            // Convert keyframes to JSON and write to file
            string json = JsonConvert.SerializeObject(new CameraAnimation() { KeyFrames = keyFrames }, Formatting.Indented);
            var path = Application.dataPath + "/" + animationFileName;
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 加载文件
        /// </summary>
        public void LoadFile()
        {
            var path = Application.dataPath + "/" + animationFileName;
            if (File.Exists(path))
            {
                // Read animation data from file and play it
                string json = File.ReadAllText(path);
                CameraAnimation animation = JsonConvert.DeserializeObject<CameraAnimation>(json);

                if (animation != null && animation.KeyFrames != null)
                {
                    keyFrames = animation.KeyFrames;
                    currentFrameNum = 0;
                }
            }
        }

        /// <summary>
        /// 重置到第0帧
        /// </summary>
        public void ResetToZeroFrame()
        {
            currentFrameNum = 0;
            PlayFrame(keyFrames[currentFrameNum]);
        }

        /// <summary>
        /// 播放动画
        /// </summary>
        public void PlayAnimations()
        {
            StartCoroutine(PlayAnimationsCoroutine());
        }

        IEnumerator PlayAnimationsCoroutine()
        {
            if (keyFrames.Count == 0)
            {
                LoadFile();
            }

            foreach (var keyframe in keyFrames)
            {
                PlayFrame(keyframe);
                yield return new WaitForSeconds((float)keyframe.Duration);
            }
        }

        void PlayFrame(CameraKeyFrame keyframe)
        {
            if (keyframe == null)
            {
                return;
            }
            if (keyframe.Action == CameraKeyFrame.AnimationAction.FlyTo)
            {
                CameraController.FlyTo(new double3(keyframe.X, keyframe.Y, keyframe.Z), keyframe.Distance, keyframe.RotateAngle, keyframe.SkewAngle, keyframe.Duration);
            }
        }

        public string GetCurrentFrameString()
        {
            if (keyFrames.Count == 0)
            {
                return "";
            }

            return JsonConvert.SerializeObject(keyFrames[currentFrameNum], Formatting.Indented);
        }


        public bool LoadKeyBindings()
        {
            string path = Application.dataPath + "/" + keyBindingConfigFileName;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loadConfig = JsonConvert.DeserializeObject<KeyBindingConfig>(json);
                if (loadConfig != null && loadConfig.Bindings != null)
                {
                    if (keyBindingConfig == null)
                    {
                        keyBindingConfig = new KeyBindingConfig();
                        keyBindingConfig.Bindings = loadConfig.Bindings;
                        return true;
                    }
                    if (keyBindingConfig.Bindings == null)
                    {
                        keyBindingConfig.Bindings = loadConfig.Bindings;
                        return true;
                    }
                    keyBindingConfig.Bindings.AddRange(loadConfig.Bindings);
                    keyBindingConfig.Bindings = keyBindingConfig.Bindings
                        .GroupBy(b => new { b.Key, b.Mode })
                        .Select(g => g.First())
                        .ToList();
                    return true;
                }
            }
            return false;
        }

        public void SaveKeyBindings()
        {
            string path = Application.dataPath + "/" + keyBindingConfigFileName;
            if (keyBindingConfig == null)
            {
                return;
            }
            string json = JsonConvert.SerializeObject(keyBindingConfig, Formatting.Indented);
            File.WriteAllText(path, json);

        }

        //public void ParseKeyBindings(KeyBindingConfig keyBindingConfig)
        //{
        //    if (keyBindingConfig != null)
        //    {
        //        foreach (var binding in keyBindingConfig.Bindings)
        //        {
        //            string buttonText = $"{binding.Text}({binding.Key})";
        //            KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), binding.Key);

        //            if (binding.Mode == KeyBindingConfig.InputMode.Edit)
        //            {
        //                if (keyActionsEditMode.ContainsKey(key))
        //                {
        //                    keyActionsEditMode[key] = ResolveAction(binding.Action);
        //                }
        //                else
        //                {
        //                    keyActionsEditMode.Add(key, ResolveAction(binding.Action));
        //                }
        //                if (binding.ShowButton)
        //                {
        //                    if (buttonContentsEditMode.ContainsKey(key))
        //                    {
        //                        buttonContentsEditMode[key] = new GUIContent(buttonText);
        //                    }
        //                    else
        //                    {
        //                        buttonContentsEditMode.Add(key, new GUIContent(buttonText));
        //                    }
        //                }
        //            }
        //            else if (binding.Mode == KeyBindingConfig.InputMode.Play)
        //            {
        //                if (keyActionsPlayMode.ContainsKey(key))
        //                {
        //                    keyActionsPlayMode[key] = ResolveAction(binding.Action);
        //                }
        //                else
        //                {
        //                    keyActionsPlayMode.Add(key, ResolveAction(binding.Action));
        //                }
        //                if (binding.ShowButton)
        //                {
        //                    if (buttonContentsPlayMode.ContainsKey(key))
        //                    {
        //                        buttonContentsPlayMode[key] = new GUIContent(buttonText);
        //                    }
        //                    else
        //                    {
        //                        buttonContentsPlayMode.Add(key, new GUIContent(buttonText));
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        //public void LoadDefaultKeyBindings()
        //{
        //    keyBindingConfig = new KeyBindingConfig();
        //    keyBindingConfig.Bindings = new List<KeyBinding>();

        //    // 添加编辑模式下的按键绑定
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F1.ToString(),
        //        Action = "ToggleEditMode",
        //        Text = "切换编辑模式",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F2.ToString(),
        //        Action = "PushCurrentView",
        //        Text = "往后插一帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F3.ToString(),
        //        Action = "InsertDelay",
        //        Text = "插入等待间隔",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F4.ToString(),
        //        Action = "InsertCurrentView",
        //        Text = "往前插一帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F5.ToString(),
        //        Action = "RemoveCurrentFrame",
        //        Text = "移除当前帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F6.ToString(),
        //        Action = "ReplaceCurrentFrame",
        //        Text = "替换当前帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F7.ToString(),
        //        Action = "PushbackCurrentView",
        //        Text = "插入当前视野到末尾",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F9.ToString(),
        //        Action = "CleanAllFrame",
        //        Text = "清空所有帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F10.ToString(),
        //        Action = "SaveFile",
        //        Text = "保存文件",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F11.ToString(),
        //        Action = "PlayAnimations",
        //        Text = "播放动画",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F12.ToString(),
        //        Action = "LoadFile",
        //        Text = "加载文件",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.LeftArrow.ToString(),
        //        Action = "PreviousFrame",
        //        Text = "上一帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.RightArrow.ToString(),
        //        Action = "NextFrame",
        //        Text = "下一帧",
        //        ShowButton = true,
        //        Mode = KeyBindingConfig.InputMode.Edit
        //    });

        //    // 添加播放模式下的按键绑定
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F1.ToString(),
        //        Action = "ToggleEditMode",
        //        Text = "切换编辑模式",
        //        ShowButton = false,
        //        Mode = KeyBindingConfig.InputMode.Play
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F11.ToString(),
        //        Action = "PlayAnimations",
        //        Text = "播放动画",
        //        ShowButton = false,
        //        Mode = KeyBindingConfig.InputMode.Play
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.F12.ToString(),
        //        Action = "LoadFile",
        //        Text = "加载文件",
        //        ShowButton = false,
        //        Mode = KeyBindingConfig.InputMode.Play
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.LeftArrow.ToString(),
        //        Action = "PreviousFrame",
        //        Text = "上一帧",
        //        ShowButton = false,
        //        Mode = KeyBindingConfig.InputMode.Play
        //    });
        //    keyBindingConfig.Bindings.Add(new KeyBinding()
        //    {
        //        Key = KeyCode.RightArrow.ToString(),
        //        Action = "NextFrame",
        //        Text = "下一帧",
        //        ShowButton = false,
        //        Mode = KeyBindingConfig.InputMode.Play
        //    });

        //}

        //public System.Action ResolveAction(string actionName)
        //{
        //    var method = typeof(CameraAnimationEditor).GetMethod(actionName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        //    if (method != null)
        //    {
        //        return (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), this, method);
        //    }
        //    else
        //    {
        //        Debug.LogError($"Action '{actionName}' not found in CameraAnimationEditor class.");
        //        return null;
        //    }
        //} 

        public void ParseKeyBindings(KeyBindingConfig keyBindingConfig)
        {
            if (keyBindingConfig != null)
            {
                foreach (var binding in keyBindingConfig.Bindings)
                {
                    string buttonText = $"{binding.Text}({binding.Key})";
                    KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), binding.Key);

                    if (keyActionsPlayMode.ContainsKey(key))
                    {
                        keyActionsPlayMode[key] = ResolveAction(binding.Action);
                    }
                    else
                    {
                        keyActionsPlayMode.Add(key, ResolveAction(binding.Action));
                    }

                }

            }
        }

        public void LoadDefaultKeyBindings()
        {
            keyBindingConfig = new KeyBindingConfig();
            keyBindingConfig.Bindings = new List<KeyBinding>
            {
                new KeyBinding()
                {
                    Key = KeyCode.F1.ToString(),
                    Action = "InsertCurrentView",
                    Text = "往前插一帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F2.ToString(),
                    Action = "PushCurrentView",
                    Text = "往后插一帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F3.ToString(),
                    Action = "InsertDelay",
                    Text = "插入等待间隔",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                 new KeyBinding()
                {
                    Key = KeyCode.F4.ToString(),
                    Action = "PushbackCurrentView",
                    Text = "插入当前视野到末尾",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F5.ToString(),
                    Action = "RemoveCurrentFrame",
                    Text = "移除当前帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F6.ToString(),
                    Action = "ReplaceCurrentFrame",
                    Text = "替换当前帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F7.ToString(),
                    Action = "CleanAllFrame",
                    Text = "清空所有帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F8.ToString(),
                    Action = "SaveFile",
                    Text = "保存文件",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                 new KeyBinding()
                {
                    Key = KeyCode.F9.ToString(),
                    Action = "LoadFile",
                    Text = "加载文件",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F11.ToString(),
                    Action = "PlayAnimations",
                    Text = "播放动画",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.F12.ToString(),
                    Action = "ResetToZeroFrame",
                    Text = "重置到第0帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.LeftArrow.ToString(),
                    Action = "PreviousFrame",
                    Text = "上一帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },
                new KeyBinding()
                {
                    Key = KeyCode.RightArrow.ToString(),
                    Action = "NextFrame",
                    Text = "下一帧",
                    ShowButton = true,
                    Mode = KeyBindingConfig.InputMode.Edit
                },


            };

        }

        public Action ResolveAction(string actionName)
        {
            var method = typeof(CameraPlayer).GetMethod(actionName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (method != null)
            {
                return (Action)Delegate.CreateDelegate(typeof(Action), this, method);
            }
            else
            {
                Debug.LogError($"Action '{actionName}' not found in CameraPlayer class.");
                return null;
            }
        }
    }

    [System.Serializable]
    public class CameraKeyFrame
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum AnimationAction
        {
            FlyTo,
            Delay
        }
        public AnimationAction Action;
        public double X;
        public double Y;
        public double Z;
        public double Distance;
        public double RotateAngle;
        public double SkewAngle;
        public double Duration;
    }

    [System.Serializable]
    public class CameraAnimation
    {
        public List<CameraKeyFrame> KeyFrames;
    }

    [System.Serializable]
    public class KeyBinding
    {
        public string Key;
        public string Action;
        public string Text;
        public bool ShowButton;
        public KeyBindingConfig.InputMode Mode;
    }

    [System.Serializable]
    public class KeyBindingConfig
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum InputMode
        {
            Edit,
            Play
        }
        public List<KeyBinding> Bindings;
    }

}