using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DjmaxRandomSelectorV
{
    public interface IFileManager
    {
        T Import<T>(string path);
        void Export<T>(T instance, string path);
        Task<string> RequestAsync(string url);
        void Write(string content, string path);
    }

    public class FileManager : IFileManager
    {
        public T Import<T>(string path)
        {
            using var reader = new StreamReader(path);
            string json = reader.ReadToEnd();
            var option = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            T instance = JsonSerializer.Deserialize<T>(json, option);

            return instance ?? throw new NullReferenceException();
        }

        public void Export<T>(T instance, string path)
        {
            var option = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            string json = JsonSerializer.Serialize(instance, option);
            Write(json, path);
        }

        public async Task<string> RequestAsync(string url)
        {
            using var client = new HttpClient();
            string result = await client.GetStringAsync(url);
            return result;
        }

        public void Write(string content, string path)
        {
            using var writer = new StreamWriter(path);
            writer.Write(content);
        }
    }

    // 프로그램 폴더 아래의 지정한 폴더에서 JSON 파일 대화상자를 연다. 취소하면 null을 반환한다.
    public static class JsonFileDialog
    {
        public static string Save(string folder) => Show(new SaveFileDialog(), folder);

        public static string Open(string folder) => Show(new OpenFileDialog(), folder);

        private static string Show(FileDialog dialog, string folder)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
            Directory.CreateDirectory(path);
            dialog.InitialDirectory = path;
            dialog.DefaultExt = ".json";
            dialog.Filter = "JSON Files (*.json)|*.json";
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
