using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UnityEngine.Profiling;
using OfficeOpenXml;
public class ResourceCheckTool: EditorWindow
{

    static Dictionary<string, List<string>> refDic = new Dictionary<string, List<string>>();

    private static EditorApplication.CallbackFunction _updateDelegate;

    public delegate Dictionary<string, List<string>> ThreadRun(ThreadPars par);

    private const int ThreadCount = 4;

    public class ThreadPars
    {
        public List<string> CheckAssetList = new List<string>();
        public List<string> CheckCSList = new List<string>();
        public List<string> AssetGuidList = new List<string>();
        public List<string> AssetNameList = new List<string>();
        public List<string> CheckWihteList = new List<string>();
    }

    private static Dictionary<string, List<string>> ThreadFind(ThreadPars par)
    {
        Dictionary<string, List<string>> ret = new Dictionary<string, List<string>>();
        if (par != null)
        {
            foreach (var file in par.CheckAssetList)
            {
                string fileContent = File.ReadAllText(file);
                foreach (var aimGuid in par.AssetGuidList)
                {
                    if (Regex.IsMatch(fileContent, aimGuid))
                    {
                        if (ret.ContainsKey(aimGuid))
                        {
                            ret[aimGuid].Add(file);
                        }
                        else
                        {
                            List<string> list = new List<string>();
                            list.Add(file);
                            ret.Add(aimGuid, list);
                        }
                    }
                    else if (!ret.ContainsKey(aimGuid))
                    {
                        List<string> list = new List<string>();
                        ret.Add(aimGuid, list);
                    }
                }
            }

            foreach (var file in par.CheckCSList)
            {
                string fileContent = File.ReadAllText(file);
                foreach (var aimName in par.AssetNameList)
                {
                    var checkName = Path.GetFileName(aimName);
                    if (Regex.IsMatch(fileContent, checkName))
                    {
                        if (ret.ContainsKey(aimName))
                        {
                            ret[aimName].Add(file);
                        }
                        else
                        {
                            List<string> list = new List<string>();
                            list.Add(file);
                            ret.Add(aimName, list);
                        }
                    }
                    else if (!ret.ContainsKey(aimName))
                    {
                        List<string> list = new List<string>();
                        ret.Add(aimName, list);
                    }
                }
            }

            foreach (var file in par.CheckWihteList)
            {
                string fileContent = file;
                foreach (var aimName in par.AssetNameList)
                {
                    var checkName = Path.GetFileName(aimName);
                    if (Regex.IsMatch(checkName, fileContent))
                    {
                        if (ret.ContainsKey(aimName))
                        {
                            ret[aimName].Add(file);
                        }
                        else
                        {
                            List<string> list = new List<string>();
                            list.Add(file);
                            ret.Add(aimName, list);
                        }
                    }
                }
            }
            
        }

        return ret;
    }

    static public void ResourceAnalysis()
    {
        FindThread("Assets/AssetsPackage", true);

    }

    //[MenuItem("Assets/Find References Thread",false,10)]
    public static void FindThread(string path, bool outExcel = false)
    {
        refDic.Clear();
        EditorSettings.serializationMode = SerializationMode.ForceText;
        AssetDatabase.Refresh();
        //string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!string.IsNullOrEmpty(path))
        {

            List<string> _AssetGuidList = new List<string>();
            List<string> _AssetNameList = new List<string>();
            if (Directory.Exists(path))
            {
                string[] assetPathsFromFolder = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);

                foreach (var tempPath in assetPathsFromFolder)
                {
                    if (!tempPath.EndsWith(".meta"))
                    {
                        var assetFindPath = GetRelativeAssetsPath(tempPath);
                        var guid = AssetDatabase.AssetPathToGUID(assetFindPath);
                        if (!string.IsNullOrEmpty(guid))
                        {
                            _AssetGuidList.Add(guid);
                            _AssetNameList.Add(assetFindPath);
                        }
                        else
                        {
                            Debug.LogError(tempPath);
                        }
                    }
                }
            }
            else
            {
                var assetFindPath = GetRelativeAssetsPath(path);
                var guid = AssetDatabase.AssetPathToGUID(assetFindPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    _AssetGuidList.Add(guid);
                    _AssetNameList.Add(assetFindPath);
                }
                else
                {
                    Debug.LogError(path);
                }
            }

            ThreadPars[] threadParses = new ThreadPars[ThreadCount];
            for (int i = 0; i < ThreadCount; i++)//添加查找的udid
            {
                threadParses[i] = new ThreadPars();
                threadParses[i].AssetGuidList = _AssetGuidList;
                threadParses[i].AssetNameList = _AssetNameList;
            }


            List<string> withoutExtensions = new List<string>()
            {
                ".prefab",
                ".unity",
                ".mat",
                ".asset",
                ".playable"
            };
            List<string> dirs = new List<string>();
            var dir = new DirectoryInfo("Packages/../Modules");
            var directories = dir.GetDirectories();
            for (int i = 0; i < directories.Length; i++)
            {
                string temp = $"Packages/{directories[i].Name}/AssetsPackage";
                if (!Directory.Exists(temp)) continue;
                dirs.Add(temp);
            }
            
            dir = new DirectoryInfo("Packages");
            directories = dir.GetDirectories();
            for (int i = 0; i < directories.Length; i++)
            {
                string temp = $"Packages/{directories[i].Name}/AssetsPackage";
                if (!Directory.Exists(temp)) continue;
                dirs.Add(temp);
            }

            dirs.Add("Assets/AssetsPackage");
            List<string> files = new List<string>();
            for (int i = 0; i < dirs.Count; i++)
            {
                var fs = Directory.GetFiles(dirs[i], "*.*",
                            SearchOption.AllDirectories)
                        .Where(s => withoutExtensions.Contains(Path.GetExtension(s).ToLower()));
                files.AddRange(fs);
            }

            for (int i = 0; i < files.Count; i++) //添加要查找的资源文件
            {
                int index = i % ThreadCount;
                threadParses[index].CheckAssetList.Add(files[i]);
            }


            string[] csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
            .Where(s => s.IndexOf("/editor/", System.StringComparison.OrdinalIgnoreCase) <0).ToArray();
            for (int i = 0; i < csFiles.Length; i++)//添加要查找的CS文件
            {
                int index = i % ThreadCount;
                threadParses[index].CheckCSList.Add(csFiles[i]);
            }

            ThreadRun[] tRun = new ThreadRun[ThreadCount];
            int finishedState = ThreadCount;

            IAsyncResult[] results = new IAsyncResult[ThreadCount];

            _updateDelegate = delegate
            {
                var finishedCount = 0;
                for (int i = 0; i < ThreadCount; i++)
                {
                    if (results[i].IsCompleted) ++finishedCount;
                }

                EditorUtility.DisplayProgressBar("匹配资源中", string.Format("进度：{0}", finishedCount), (float)finishedCount/ ThreadCount);

                if (finishedCount >= finishedState)
                {

                    for (int i = 0; i < ThreadCount; i++)
                    {
                        Dictionary<string, List<string>> temRunThreadData = tRun[i].EndInvoke(results[i]);
                        foreach (var keyValue in temRunThreadData)
                        {
                            var key = keyValue.Key;
                            if (key.Contains("/") || key.Contains("\\"))
                            {
                                key = AssetDatabase.AssetPathToGUID(key);
                            }

                            if (refDic.ContainsKey(key))
                            {
                                refDic[key].AddRange(keyValue.Value);
                            }
                            else
                            {
                                refDic.Add(key, keyValue.Value);
                            }

                        }
                    }
                    
                    EditorUtility.ClearProgressBar();
                    EditorApplication.update -= _updateDelegate;

                    if (outExcel)
                    {
                        OutputToExcel();
                    }
                    else
                    {
                        ResourceCheckTool window =
                                (ResourceCheckTool) EditorWindow.GetWindow(typeof (ResourceCheckTool));
                        window.Show();
                    }
                }
            };

            for (int i = 0; i < ThreadCount; i++)
            {
                tRun[i] = ThreadFind;
                results[i] = tRun[i].BeginInvoke(threadParses[i], null, null);
            }

            EditorApplication.update += _updateDelegate;
        }

    }

    private static string GetRelativeAssetsPath(string path)
    {
        if (path.Contains("Modules"))
        {
            return path.Replace("Modules", "Packages");
        }

        if (path.Contains("Packages"))
        {
            return path;
        }

        return "Assets" + Path.GetFullPath(path).Replace(Path.GetFullPath(Application.dataPath), "")
                .Replace('\\', '/');
    }




    bool showNoUser = true;
    bool showUser = true;
    System.Collections.Generic.List<bool> showList = new System.Collections.Generic.List<bool>();
    Vector2 scrollPos;
    void OnGUI()
    {

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(position.width),
            GUILayout.Height(position.height));

        showNoUser = EditorGUILayout.Toggle("无引用的资源：", showNoUser);
        if (GUILayout.Button("删除所有无引用的资源"))
        {
            AssetDatabase.StartAssetEditing();
            var keys = refDic.Keys.ToArray();
            foreach (var item in keys)
            {
                //Debug.Log(item.Value.Count + "=" + item.Key);
                if (refDic[item].Count == 0)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(item);
                    AssetDatabase.DeleteAsset(assetPath);
                    refDic.Remove(item);
                }
            }

            AssetDatabase.StopAssetEditing();
        }

        if (showNoUser)
        {
            Rect r = EditorGUILayout.BeginVertical("Button");
            foreach (var item in refDic)
            {
                //Debug.Log(item.Value.Count + "=" + item.Key);
                if (item.Value.Count == 0)
                {

                    EditorGUILayout.BeginHorizontal();
                    var assetPath = AssetDatabase.GUIDToAssetPath(item.Key);
                    Object assetObj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                    GUILayout.Label(assetPath, GUILayout.Width(350));
                    EditorGUILayout.ObjectField("", assetObj, typeof(Object), true, GUILayout.Width(120));
                    GUILayout.Label("内存占用：" + EditorUtility.FormatBytes(Profiler.GetRuntimeMemorySizeLong(assetObj)), GUILayout.Width(150));

                    if (FindReferences.Rule01(assetPath))
                    {
                        GUIStyle fontStyle = new GUIStyle();
                        fontStyle.normal.textColor = new Color(1, 0, 0);   //设置字体颜色  
                        fontStyle.fixedWidth = 100;
                        GUILayout.Label("bigImage", fontStyle);
                    }

                    EditorGUILayout.EndHorizontal();

                }
            }
            EditorGUILayout.EndVertical();
        }


        showUser = EditorGUILayout.Toggle("有引用的资源：", showUser);
        if (showUser)
        {
            var index = 0;
            foreach (var item in refDic)
            {
                if (item.Value.Count > 0)
                {
                    if (showList.Count <= index)
                    {
                        showList.Add(true);
                    }

                    //有引用的资源
                    EditorGUILayout.BeginHorizontal();
                    var assetPath = AssetDatabase.GUIDToAssetPath(item.Key);
                    Object assetObj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                    GUILayout.Label(assetPath, GUILayout.Width(350));
                    EditorGUILayout.ObjectField("", assetObj, typeof(Object), true, GUILayout.Width(100));
                    showList[index] = EditorGUILayout.Toggle("", showList[index], GUILayout.Width(20));

                    GUILayout.Label("内存占用：" + EditorUtility.FormatBytes(Profiler.GetRuntimeMemorySizeLong(assetObj)), GUILayout.Width(150));

                    if (FindReferences.Rule01(assetPath))
                    {
                        GUIStyle fontStyle = new GUIStyle();
                        fontStyle.normal.textColor = new Color(1, 0, 0);   //设置字体颜色  
                        fontStyle.fixedWidth = 100;
                        GUILayout.Label("bigImage", fontStyle);
                    }

                    //GUILayout.Label("被下面" + item.Value.Count + "个资源引用:");
                    EditorGUILayout.EndHorizontal();

                    if (showList[index])
                    {
                        Rect r = EditorGUILayout.BeginVertical("Button");
                        foreach (var fileName in item.Value)
                        {
                            assetPath = GetRelativeAssetsPath(fileName);
                            assetObj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label(assetPath, GUILayout.Width(350));
                            EditorGUILayout.ObjectField("", assetObj, typeof(Object), true, GUILayout.Width(120));
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndVertical();
                    }
                    index++;
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    static private void OutputToExcel()
    {
        //输出excel


        //指定默认路径
        //string path = Application.dataPath + "/" + "UIPrefabExcel.xlsx";
        //自己选择一个路径
        System.DateTime time = System.DateTime.Now;
        string timeStr = time.Year.ToString("D4") + "_" + time.Month.ToString("D2") + time.Day.ToString("D2") + "_" + time.Hour.ToString("D2") + time.Minute.ToString("D2");
        string path = EditorUtility.SaveFilePanel("Save Excel File", "", timeStr + ".xlsx", "xlsx");
        FileInfo newFile = new FileInfo(path);
        if (newFile.Exists)
        {
            newFile.Delete();
            newFile = new FileInfo(path);
        }

        //通过ExcelPackage打开文件
        using (ExcelPackage package = new ExcelPackage(newFile))
        {
            //添加sheet
            ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("sheet1");
            //添加列名
            worksheet.Cells[1, 1].Value = "资源名字";
            worksheet.Cells[1, 2].Value = "被引用数量";
            worksheet.Cells[1, 3].Value = "内存占用";
            worksheet.Cells[1, 4].Value = "Atlas超过256*256";

            var index = 2;

            foreach (var item in refDic)
            {
                //if (item.Value.Count == 0)
                //{
                var assetPath = AssetDatabase.GUIDToAssetPath(item.Key);
                worksheet.Cells["A" + index].Value = assetPath;
                worksheet.Cells["B" + index].Value = item.Value.Count;
                Object assetObj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                worksheet.Cells["C" + index].Value = EditorUtility.FormatBytes(Profiler.GetRuntimeMemorySizeLong(assetObj));
                worksheet.Cells["D" + index].Value = FindReferences.Rule01(assetPath) ? "是" : "否";
                index++;
                //}
            }

            //foreach (var item in refDic)
            //{
            //    if (item.Value.Count > 0)
            //    {
            //        var assetPath = AssetDatabase.GUIDToAssetPath(item.Key);
            //        worksheet.Cells["A"+ index].Value = assetPath;
            //        worksheet.Cells["B"+ index].Value = item.Value.Count;
            //        Object assetObj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
            //        worksheet.Cells["C" + index].Value = EditorUtility.FormatBytes(Profiler.GetRuntimeMemorySizeLong(assetObj));
            //        index++;
            //    }
            //}

            package.Save();
        }
    }

}
