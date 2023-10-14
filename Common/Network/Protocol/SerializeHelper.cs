using UnityEngine;
public static class SerializeHelper
{
    public static byte[] Serialize<T>(T obj) where T : class
    {
        if (obj == null)
        {
            return null;
        }
        var json = JsonUtility.ToJson(obj);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return bytes;
    }

    public static T Deserialize<T>(byte[] bytes) where T : class
    {
        if (bytes == null)
        {
            return null;
        }
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var obj = JsonUtility.FromJson<T>(json);
        return obj;
    }
}
