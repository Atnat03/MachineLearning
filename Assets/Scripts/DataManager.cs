using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Text;

[XmlRoot("Data")]
public class DataManager : MonoBehaviour
{
    public static DataManager instance;
    public        string      path;

    XmlSerializer serializer = new(typeof(Data));
    Encoding      encoding   = Encoding.UTF8;

    void Awake()
    {
        instance = this;
        SetPath();
    }

    public void Save(Data data)
    {
        StreamWriter streamWriter = new(path, false, encoding);
        serializer.Serialize(streamWriter, data);
        streamWriter.Close();
    }

    public Data Load()
    {
        if (File.Exists(path))
        {
            FileStream fileStream = new(path, FileMode.Open);

            Data data = serializer.Deserialize(fileStream) as Data;
            fileStream.Close();

            return data;
        }

        return null;
    }

    void SetPath()
    {
        path = Path.Combine(Application.persistentDataPath, "Data.xml");
    }
}

public class Data
{
    public int                 generation;
    public List<NeuralNetwork> nets;
}