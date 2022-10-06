using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OhMyDemoConsole.Library
{
    public static class YamlConfigurationExtensions
    {
        public static IConfigurationBuilder AddSecureJson(this IConfigurationBuilder builder, string path)
        {
            return builder.AddSecureJson(provider: null, path: path, optional: false, reloadOnChange: false);
        }

        public static IConfigurationBuilder AddSecureJson(this IConfigurationBuilder builder, string path, bool optional)
        {
            return builder.AddSecureJson(provider: null, path: path, optional: optional, reloadOnChange: false);
        }

        public static IConfigurationBuilder AddSecureJson(this IConfigurationBuilder builder, string path, bool optional, bool reloadOnChange)
        {
            return builder.AddSecureJson(provider: null, path: path, optional: optional, reloadOnChange: reloadOnChange);
        }

        public static IConfigurationBuilder AddSecureJson(this IConfigurationBuilder builder, IFileProvider provider, string path, bool optional, bool reloadOnChange)
        {
            var fullPath = path;
            if (provider == null && Path.IsPathRooted(path))
            {
                provider = new PhysicalFileProvider(Path.GetDirectoryName(path));
                fullPath = path;
                path = Path.GetFileName(path);
            }
            var source = new SecureJsonConfigurationSource
            {
                FileProvider = provider,
                Path = path,
                Optional = optional,
                ReloadOnChange = reloadOnChange,
                FullPath = fullPath,
            };
            builder.Add(source);
            return builder;
        }
    }

    public class SecureJsonConfigurationSource : FileConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            FileProvider = FileProvider ?? builder.GetFileProvider();
            return new SecureJsonConfigurationProvider(this);
        }

        public string FullPath { get; set; }
    }

    public class SecureJsonConfigurationProvider : FileConfigurationProvider
    {
        public SecureJsonConfigurationProvider(SecureJsonConfigurationSource source) : base(source) { }

        public override void Load(Stream stream)
        {
            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                var deserialized = (JObject)serializer.Deserialize(jsonTextReader)!;
                //ok I now have a nice file on disk
                var parser = new SecureJsonConfigurationFileParser(deserialized);
                Data = parser.Parse();

                if (parser.IsModified)
                {
                    var newValue = deserialized.ToString();
                    File.WriteAllText(((SecureJsonConfigurationSource)Source).FullPath, newValue);
                }
            }

        }
    }

    /// <summary>
    /// Taken by the code of .NET framework and customized to support security
    /// </summary>
    internal class SecureJsonConfigurationFileParser
    {
        private JObject _deserialized;
        private Stack<string> _paths;
        Dictionary<string, string> _data;

        public bool IsModified { get; private set; }

        public SecureJsonConfigurationFileParser(JObject deserialized)
        {
            _deserialized = deserialized;
        }

        internal IDictionary<string, string> Parse()
        {
            _data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _paths = new Stack<string>();
            IsModified = false;
            VisitObjectElement(_deserialized);
            return _data;
        }

        private void VisitObjectElement(JObject obj)
        {
            var isEmpty = true;

            foreach (var property in obj.Properties())
            {
                isEmpty = false;
                EnterContext(property.Name);
                VisitValue(property.Value);
                ExitContext();
            }

            SetNullIfElementIsEmpty(isEmpty);
        }

        private void VisitArrayElement(JArray element)
        {
            int index = 0;

            foreach (var arrayElement in element)
            {
                EnterContext(index.ToString());
                VisitValue(arrayElement);
                ExitContext();
                index++;
            }

            SetNullIfElementIsEmpty(isEmpty: index == 0);
        }

        private void SetNullIfElementIsEmpty(bool isEmpty)
        {
            if (isEmpty && _paths.Count > 0)
            {
                _data[_paths.Peek()] = null;
            }
        }

        private void VisitValue(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Object:
                    VisitObjectElement((JObject)value);
                    break;

                case JTokenType.Array:
                    VisitArrayElement((JArray)value);
                    break;

                case JTokenType.Integer:
                case JTokenType.Boolean:
                case JTokenType.String:
                case JTokenType.Float:
                case JTokenType.Null:
                    string key = _paths.Peek();
                    if (_data.ContainsKey(key))
                    {
                        throw new FormatException($"Key {key} is duplicated");
                    }
                    PopulateDataAndManageEncryption(value, key);
                    break;

                default:
                    throw new FormatException($"Unsupported json token {value.Type.ToString()}!");
            }
        }

        private void PopulateDataAndManageEncryption(JToken value, string key)
        {
            if (key.StartsWith("$") || key.Contains(":$"))
            {
                //config is encrypted.
                var stValue = value.Value<string>();
                if (stValue.StartsWith("$ENCRYPTED:"))
                {
                    //ok data was already encrypted
                    var realData = stValue.Substring("$ENCRYPTED:".Length);
                    var data = Convert.FromBase64String(realData);

                    var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
                    stValue = Encoding.UTF8.GetString(decrypted);
                }
                else
                {
                    //ok configuration is still unencrypted
                    _data[key] = stValue;
                    var dataByte = Encoding.UTF8.GetBytes(stValue);
                    var encrypted = ProtectedData.Protect(dataByte, null, DataProtectionScope.LocalMachine);
                    var encryptedString = $"$ENCRYPTED:{Convert.ToBase64String(encrypted)}";
                    value.Replace(JToken.FromObject(encryptedString));
                    IsModified = true;
                }
                if (key.StartsWith("$"))
                {
                    key = key.Substring(1);
                }
                key = key.Replace(":$", ":");
                _data[key] = stValue;
            }
            else
            {
                _data[key] = value.ToString();
            }
        }

        private void EnterContext(string context) =>
            _paths.Push(_paths.Count > 0 ?
                _paths.Peek() + ConfigurationPath.KeyDelimiter + context :
                context);

        private void ExitContext() => _paths.Pop();

    }
}

