using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using MagicFire.Mmorpg.Huanhuo;

namespace MagicFire.SceneManagement
{
    using UnityEngine;
    using System.Collections;
    using System.Xml;
    using System.IO;
    using MagicFire.Common.Plugin;
    using MagicFire.Common;
    using MagicFire.Mmorpg.UI;

    public class XmlSceneManager : MagicFire.MonoSingleton<XmlSceneManager>
    {
        [System.SerializableAttribute]
        public enum LoadModeEnum
        {
            Database,
            Assetbundle
        }

        [System.SerializableAttribute]
        public enum ControlModeEnum
        {
            PcControl,
            MobileControl
        }

        [SerializeField]
        private LoadModeEnum _loadMode;
        [SerializeField]
        private ControlModeEnum _controlMode;

        private static System.Action _action;
        private static GameObject _self;
        private static List<GameObject> _originalSceneObjects = new List<GameObject>();
        public static string Message1 { get; set; }
        private static string _xmlPath;
        private static string _loadingSceneName;
        private static XmlDocument _loadingXmlDocument;
        private static AssetBundle _loadingAssetBundle;

        public LoadModeEnum LoadMode
        {
            get { return _loadMode; }
        }

        public ControlModeEnum ControlMode
        {
            get { return _controlMode; }
        }

        private void Start()
        {
            _self = gameObject;
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            LoadScene(sceneName);
            
            if (Application.platform == RuntimePlatform.WindowsEditor)
                if (Instance._loadMode == LoadModeEnum.Database)
                    return;
            BundleTool.Instance.LoadAllBundle();
        }

        void OnGUI()
        {
            //GUI.Label(
            //    new Rect(
            //        new Vector2(400, 0),
            //        new Vector2(400, 2000)),
            //    "Debug: \n" + Message1);
        }

        /// 通过解析xml文件创建场景，如果是在Editor模式下资源会从AssetDatabase加载，如果是发布模式，资源会从AssetBundle加载。
        public static void LoadScene(string sceneName, System.Action createSceneCallBackAction)
        {
            _action = createSceneCallBackAction;
            LoadScene(sceneName);
        }

        /// 通过解析xml文件创建场景，如果是在Editor模式下资源会从AssetDatabase加载，如果是发布模式，资源会从AssetBundle加载。
        public static void LoadScene(string sceneName)
        {
            DestroyOriginalSceneObjects();
            _loadingSceneName = sceneName;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                if (Instance._loadMode == LoadModeEnum.Database)
                {
                    LoadSceneFromDatabase(sceneName);
                    if (_action != null)
                    {
                        _action.Invoke();
                        _action = null;
                    }
                    return;
                }
            }
            LoadSceneFromAssetBundle(sceneName);
        }

        /// 优化版
        static void LoadSceneFromDatabase(string sceneName)
        {
            var filepath = Application.streamingAssetsPath + "/SceneDescription/" + sceneName + ".xml";
            if (File.Exists(filepath)) //如果文件存在话开始解析。
            {
                var xmlPath = filepath;
                var xmlDocument = new XmlDocument();
                xmlDocument.Load(xmlPath);

                var xmlNodeList = xmlDocument.SelectNodes("//gameObject"); // 使用 XPATH 获取所有 gameObject 节点
                if (xmlNodeList != null)
                    foreach (XmlNode xmlNode in xmlNodeList)
                    {
                        if (xmlNode.Attributes != null)
                        {
                            var prefabName = xmlNode.Attributes["objectAsset"].Value;
                            var gameObjectName = xmlNode.Attributes["objectName"].Value;

                            var prefabObject =
                                AssetTool.LoadAsset_Database_Or_Bundle(
                                    "Assets/_Scenes/" + sceneName + "/" + prefabName + ".prefab",
                                    "SceneAssets",
                                    sceneName.ToLower(),
                                    prefabName);

                            var gameObj = (GameObject) Instantiate(prefabObject);
                            gameObj.name = gameObjectName;
                            _originalSceneObjects.Add(gameObj);
                            ParseAttributes(xmlNode, gameObj); // 使用 XPATH 获取 位置、旋转、缩放数据
                        }
                    }
            }
        }

        // 优化版
        static void LoadSceneFromAssetBundle(string sceneName)
        {
            string xmlPath = null;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    xmlPath = "file://" + Application.streamingAssetsPath + "/SceneDescription/" + sceneName + ".xml";
                    break;
                case RuntimePlatform.WindowsPlayer:
                    xmlPath = "file://" + Application.streamingAssetsPath + "/SceneDescription/" + sceneName + ".xml";
                    break;
                case RuntimePlatform.Android:
                    xmlPath = Application.streamingAssetsPath + "/SceneDescription/" + sceneName + ".xml";
                    break;
            }
            Instance.StartCoroutine("LoadXml", xmlPath);
        }

        /// 使用 XPATH 获取 位置、旋转、缩放数据
        private static void ParseAttributes(XmlNode xmlNode, GameObject gameObj)
        {
            var positionXmlNode = xmlNode.SelectSingleNode("descendant::position");
            var rotationXmlNode = xmlNode.SelectSingleNode("descendant::rotation");
            var scaleXmlNode = xmlNode.SelectSingleNode("descendant::scale");
            if (positionXmlNode != null && rotationXmlNode != null && scaleXmlNode != null)
            {
                if (positionXmlNode.Attributes != null)
                {
                    gameObj.transform.position =
                        new Vector3(
                            float.Parse(positionXmlNode.Attributes["x"].Value),
                            float.Parse(positionXmlNode.Attributes["y"].Value),
                            float.Parse(positionXmlNode.Attributes["z"].Value));
                    if (rotationXmlNode.Attributes != null)
                    {
                        gameObj.transform.rotation =
                            Quaternion.Euler(
                                new Vector3(
                                    float.Parse(rotationXmlNode.Attributes["x"].Value),
                                    float.Parse(rotationXmlNode.Attributes["y"].Value),
                                    float.Parse(rotationXmlNode.Attributes["z"].Value)));
                        if (scaleXmlNode.Attributes != null)
                            gameObj.transform.localScale =
                                new Vector3(
                                    float.Parse(scaleXmlNode.Attributes["x"].Value),
                                    float.Parse(scaleXmlNode.Attributes["y"].Value),
                                    float.Parse(scaleXmlNode.Attributes["z"].Value));
                    }
                }
            }
        }

        public IEnumerator LoadXml(string xmlPath)
        {
            var www = new WWW(xmlPath);
            while (true)
            {
                if (www.isDone)
                {
                    Message1 += "xml isDone\n";
                    break;
                }
                yield return null;
            }
            _loadingXmlDocument = new XmlDocument();
            try
            {
                _loadingXmlDocument.LoadXml(www.text);
            }
            catch (XmlException e)
            {
                Message1 += e + "\n";
                throw;
            }
            Instance.StartCoroutine("LoadAssetBundle");
        }

        public IEnumerator LoadAssetBundle()
        {
            WWW www = null;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    www = new WWW("file://" + Application.streamingAssetsPath + "/SceneAssets/Windows/" + _loadingSceneName.ToLower());
                    break;
                case RuntimePlatform.WindowsPlayer:
                    www = new WWW("file://" + Application.streamingAssetsPath + "/SceneAssets/Windows/" + _loadingSceneName.ToLower());
                    break;
                case RuntimePlatform.Android:
                    www = new WWW(Application.streamingAssetsPath + "/SceneAssets/Android/" + _loadingSceneName.ToLower());
                    break;
            }

            while (true)
            {
                if (www.isDone)
                {
                    Message1 += "bundle isDone\n";
                    break;
                }
                yield return null;
            }
            _loadingAssetBundle = www.assetBundle;
            LoadSceneByXmlAndAssetBundle();
        }

        private static void LoadSceneByXmlAndAssetBundle()
        {
            var assetBundle = _loadingAssetBundle;
            var xmlNodeList = _loadingXmlDocument.SelectNodes("//gameObject"); // 使用 XPATH 获取所有 gameObject 节点
            if (xmlNodeList != null)
            {
                foreach (XmlNode xmlNode in xmlNodeList)
                {
                    if (xmlNode.Attributes != null)
                    {
                        var prefabName = xmlNode.Attributes["objectAsset"].Value;
                        var gameObjectName = xmlNode.Attributes["objectName"].Value;

                        //Message1 += gameObjectName + "\n";

                        if (assetBundle != null)
                        {
                            var assetObject = assetBundle.LoadAsset(prefabName);
                            if (assetObject != null)
                            {
                                var gameObj = (GameObject)Instantiate(assetObject);
                                gameObj.name = gameObjectName;
                                _originalSceneObjects.Add(gameObj);
                                ParseAttributes(xmlNode, gameObj);// 使用 XPATH 获取 位置、旋转、缩放数据
                            }
                        }
                        else
                        {
                            Debug.LogError(_loadingSceneName + " bundle is null!\n");
                            Message1 += _loadingSceneName + " bundle is null!\n";
                        }
                    }
                }
            }
            if (assetBundle != null)
                assetBundle.Unload(false);// 卸载引用的加载资源，释放内存
            if (_action != null)
            {
                _action.Invoke();
                _action = null;
            }
        }

        /// 销毁当前场景
        static void DestroyCurrentScene()
        {
            var rootGameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            //UnityEngine.SceneManagement.SceneManager.GetSceneByName("DontDestroyOnLoad");
            foreach (var item in rootGameObjects)
            {
                if (item == _self)
                    continue;
                if (item.tag == "DontDestroy")
                    continue;
                Destroy(item);
            }
        }

        public static void DestroyOriginalSceneObjects()
        {
            foreach (var item in _originalSceneObjects)
            {
                if (item == null)
                    continue;
                if (item == _self)
                    continue;
                if (item.tag == "DontDestroy")
                    continue;
                Destroy(item);
            }
            _originalSceneObjects.Clear();
        }
    } 
}