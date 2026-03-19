using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[XmlRoot("Data")]
public class DataManager : MonoBehaviour
{
    public static DataManager instance;

    public TMP_InputField fileNameInput;

    private string folderPath;

    XmlSerializer serializer = new(typeof(Data));
    Encoding encoding = Encoding.UTF8;

    void Awake()
    {
        instance = this;

        folderPath = Path.Combine(Application.dataPath, "Saves");

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
    }

    string GetPath()
    {
        string fileName = fileNameInput.text;

        if (string.IsNullOrEmpty(fileName))
            fileName = "Data";

        return Path.Combine(folderPath, fileName + ".xml");
    }

    public void Save(Data data)
    {
        string path = GetPath();

        using (StreamWriter streamWriter = new(path, false, encoding))
        {
            serializer.Serialize(streamWriter, data);
        }

        AssetDatabase.Refresh();
    }

    public Data LoadFromFile()
    {
        string path = EditorUtility.OpenFilePanel("Load Save", Application.dataPath + "/Saves", "xml");

        if (string.IsNullOrEmpty(path))
            return null;
        
        if (File.Exists(path))
        {
            using (FileStream fileStream = new(path, FileMode.Open))
            {
                return serializer.Deserialize(fileStream) as Data;
            }
        }

        return null;
    }
}

public class Data
{
    public int generation;
    public List<NeuralNetwork> nets;
}