//TAScratch
//https://github.com/hayattgd/TAScratch/

using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net;

class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        List<string> keymap = new List<string>()
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
            "up arrow", "down arrow", "right arrow", "left arrow", "space", "enter"
        };

        Console.WriteLine("Welcome to TAScratch sb3 Patcher!");

        string selectedFilePath = string.Empty;
        if (args.Length < 1)
        {
            Console.WriteLine("Please select sb3.");
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "(*.sb3)|*.sb3";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFilePath = openFileDialog.FileName;
                }
                else
                {
                    Console.WriteLine("File select aborted.");
                    return -1;
                }
            }
        }
        else
        {
            selectedFilePath = args[0];
        }
        
        if (!File.Exists(selectedFilePath))
        {
            Console.WriteLine("Selected file doenst exists.");
            return -1;
        }

        Console.WriteLine("Processing file...");

        string json = string.Empty;

        using(ZipArchive archive = ZipFile.Open(selectedFilePath, ZipArchiveMode.Read))
        {
            ZipArchiveEntry? projectjson = archive.GetEntry("project.json");
            if (projectjson == null)
            {
                Console.WriteLine("project.json not found inside sb3.");
                return -1;
            }

            using(Stream str = projectjson.Open())
            {
                StreamReader reader = new StreamReader(str);
                json = reader.ReadToEnd();
                reader.Dispose();
            }
        }
        Console.WriteLine("project.json read successfully");

        Console.WriteLine("Finding block to modify...");
        JObject parsedJson = JObject.Parse(json);

        IEnumerable<JToken> blocks;

        try
        {
            blocks = parsedJson["targets"]?
                .SelectMany(target => target["blocks"]?
                .Children<JProperty>()
                .Select(prop => prop.Value)
                .OfType<JObject>() ?? Enumerable.Empty<JObject>())
                .Where(block => 
                block["opcode"]?.ToString() == "sensing_keypressed" ||
                block["opcode"]?.ToString() == "sensing_keyoptions" ||
                block["opcode"]?.ToString() == "sensing_mousedown" ||
                block["opcode"]?.ToString() == "sensing_mousex" ||
                block["opcode"]?.ToString() == "sensing_mousey" ||
                block["opcode"]?.ToString() == "sensing_touchingobjectmenu"
                )
                .ToList() ?? Enumerable.Empty<JObject>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error processing project.json: " + ex.Message);
            return -2;
        }

        if (blocks.Any())
        {
            Console.WriteLine("Listing key options...");
            Dictionary<string, string> keyoption = new();
            foreach (var block in blocks)
            {
                if (block["opcode"]?.ToString() == "sensing_keyoptions" && isNotTAS_WORKER(block))
                {
                    string parentKey = block["parent"]?.ToString() ?? string.Empty;
                    JToken keyOptionToken = block["fields"]?["KEY_OPTION"]?[0] ?? JToken.FromObject(new object());
                    if (!string.IsNullOrEmpty(parentKey) && keyOptionToken != null)
                    {
                        if (!keyoption.ContainsKey(parentKey))
                        {
                            keyoption.Add(parentKey, keyOptionToken.ToString());
                        }
                        RemoveTokenFromParent(block); 
                    }
                    else
                    {
                        Console.WriteLine("Skipping because key/value invailed. may cause error soon.");
                    }
                }
            }

            Console.WriteLine("Replacing inputs...");

            foreach (var block in blocks)
            {
                if (isNotTAS_WORKER(block))
                {
                    if (block["opcode"]?.ToString() == "sensing_keypressed")
                    {
                        string[] sstr = block.Path.Split('.');
                        if (keyoption.ContainsKey(sstr[sstr.Length - 1]))
                        {
                            if (keymap.Contains(keyoption[block.Path.Split('.')[2]]))
                            {
                                block["opcode"] = "data_itemoflist";
                                block["fields"] = new JObject { { "LIST", new JArray { "tas_input", "+I~~18V#hQrwh!DU=]%E" } } };
                                block["inputs"] = new JObject { { "INDEX", new JArray { 1, new JArray { 7, keymap.IndexOf(keyoption[sstr[sstr.Length - 1]]) + 4 } } } };
                            }
                        }
                    }
                    else if(block["opcode"]?.ToString() == "sensing_mousedown")
                    {
                        block["opcode"] = "data_itemoflist";
                        block["fields"] = new JObject { { "LIST", new JArray { "tas_input", "+I~~18V#hQrwh!DU=]%E" } } };
                        block["inputs"] = new JObject { { "INDEX", new JArray { 1, new JArray { 7, 3 } } } };
                    }
                    else if(block["opcode"]?.ToString() == "sensing_mousex")
                    {
                        block["opcode"] = "data_itemoflist";
                        block["fields"] = new JObject { { "LIST", new JArray { "tas_input", "+I~~18V#hQrwh!DU=]%E" } } };
                        block["inputs"] = new JObject { { "INDEX", new JArray { 1, new JArray { 7, 1 } } } };
                    }
                    else if(block["opcode"]?.ToString() == "sensing_mousey")
                    {
                        block["opcode"] = "data_itemoflist";
                        block["fields"] = new JObject { { "LIST", new JArray { "tas_input", "+I~~18V#hQrwh!DU=]%E" } } };
                        block["inputs"] = new JObject { { "INDEX", new JArray { 1, new JArray { 7, 2 } } } };
                    }
                    else if(block["opcode"]?.ToString() == "sensing_touchingobjectmenu")
                    {
    #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                        block["fields"] = new JObject { { "TOUCHINGOBJECTMENU", new JArray { "TAS_WORKER", null } } };
    #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("No input block is used.");
        }

        Console.WriteLine("Overwrites project.json with patched one...");

        using (var zip = ZipFile.Open(selectedFilePath, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("project.json");
            if (entry != null)
            {
                using (var stream = entry.Open())
                {
                    using (var streamWriter = new StreamWriter(stream))
                    {
                        streamWriter.Write(parsedJson.ToString(Formatting.None));
                    }
                }
            }
        }

        Console.WriteLine("All done!");

        return 0;
    }

    public static void RemoveTokenFromParent(JToken token)
    {
        if (token == null)
        {
            return; // Token is null, nothing to remove
        }

        JToken parent = token.Parent ?? JToken.FromObject(new());
        if (parent == null)
        {
            return; // Token has no parent, cannot remove
        }

        switch (parent.Type)
        {
            case JTokenType.Array:
                JArray array = (JArray)parent;
                array.Remove(token);
                break;
            case JTokenType.Object:
                JObject obj = (JObject)parent;
                if (token is JProperty property)
                {
                    property.Remove();
                }
                break;
            default:
                // Unsupported parent type
                break;
        }
    }

    public static bool isNotTAS_WORKER(JToken block)
    {
        return block?.Parent?.Parent?.Parent?.Parent?["name"]?.ToString() != "TAS_WORKER";
    }
}