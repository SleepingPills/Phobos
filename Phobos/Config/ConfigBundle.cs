using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace Phobos.Config;

public class ConfigBundle<T> where T : class
{
    private readonly string _filePath;

    public ConfigBundle(string path, T defaultValue)
    {
        _filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Config", path);

        if (!File.Exists(_filePath))
        {
            var parent = Directory.GetParent(_filePath);

            if (parent != null)
            {
                Directory.CreateDirectory(parent.FullName);
            }

            File.WriteAllText(_filePath, JsonConvert.SerializeObject(defaultValue, Formatting.Indented));
        }

        Value = JsonConvert.DeserializeObject<T>(File.ReadAllText(_filePath));
    }

    public T Value { get; private set; }

    public void Reload()
    {
        Value = JsonConvert.DeserializeObject<T>(File.ReadAllText(_filePath));
    }
}