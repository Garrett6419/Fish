using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

public class JsonDataService : IDataService
{

    public bool SaveData<T>(string RelativePath, T Data, bool Encrypted)
    {
        string path = Application.persistentDataPath + RelativePath;

        try
        {
            if (File.Exists(path))
            {
                Debug.Log("Data exists. Deleting old file and writing a new one!");
                File.Delete(path);
            }
            Debug.Log("Creating a new file");
            using FileStream stream = File.Create(path);
            stream.Close();
            File.WriteAllText(path, JsonConvert.SerializeObject(Data));
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError($"Unable to save data due to: {e.Message} {e.StackTrace}");
            return false;
        }
    }

    public T LoadData<T>(string RelativePath, bool Encrypted)
    {
        string path = Application.persistentDataPath + RelativePath;

        if(!File.Exists(path))
        {
            Debug.LogError($"Cannot load file at {path}. File does not exist");
            throw new FileNotFoundException("No Path");
        }

        try
        {
            T data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Unable to get data due to: {e.Message} {e.StackTrace}");
            throw e;
        }
    }
}
