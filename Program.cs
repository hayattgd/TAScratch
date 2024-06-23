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

        Console.WriteLine("Downloading TAS.sprite3...");

        byte[] tasSpriteData;

        using (HttpClient client = new HttpClient())
        {
            string tasSpriteUrl = "https://github.com/hayattgd/TAScratch/raw/master/tas.sprite3";
            Console.WriteLine("Downloading TAS.sprite3...");
            HttpResponseMessage response = client.GetAsync(tasSpriteUrl).Result;
            if (response.IsSuccessStatusCode)
            {
                tasSpriteData = response.Content.ReadAsByteArrayAsync().Result;
                Console.WriteLine("TAS.sprite3 downloaded successfully.");
            }
            else
            {
                Console.WriteLine("Failed to download TAS.sprite3.");
                return -3;
            }
        }

        byte[] cursorclick;
        byte[] cursornormal;
        byte[] dataSpriteJson;

        using (MemoryStream zms = new MemoryStream(tasSpriteData))
        {
            using (ZipArchive zip = new ZipArchive(zms, ZipArchiveMode.Read))
            {
                Console.WriteLine("TAS.sprite3 extracted successfully.");
                ZipArchiveEntry? entry43a48fb4ddc2f35f2099287a26dbfe60 = zip.GetEntry("43a48fb4ddc2f35f2099287a26dbfe60.svg");
                ZipArchiveEntry? entry65c6a23380f20dd8e445540b2c5bd849 = zip.GetEntry("65c6a23380f20dd8e445540b2c5bd849.svg");
                ZipArchiveEntry? entrySpriteJson = zip.GetEntry("sprite.json");

                if (entry43a48fb4ddc2f35f2099287a26dbfe60 != null && entry65c6a23380f20dd8e445540b2c5bd849 != null && entrySpriteJson != null)
                {
                    using (MemoryStream fms = new MemoryStream())
                    {
                        entry43a48fb4ddc2f35f2099287a26dbfe60.Open().CopyTo(fms);
                        cursorclick = fms.ToArray();
                    }

                    using (MemoryStream fms = new MemoryStream())
                    {
                        entry65c6a23380f20dd8e445540b2c5bd849.Open().CopyTo(fms);
                        cursornormal = fms.ToArray();
                    }

                    using (MemoryStream fms = new MemoryStream())
                    {
                        entrySpriteJson.Open().CopyTo(fms);
                        dataSpriteJson = fms.ToArray();
                    }

                    Console.WriteLine("TAS.sprite3 loaded successfully.");
                }
                else
                {
                    Console.WriteLine("One or more required files didnt found.");
                    return -4;
                }
            }
        }

        string json = string.Empty;

        Console.WriteLine("Adding TAS_WORKER costume...");

        using(ZipArchive archive = ZipFile.Open(selectedFilePath, ZipArchiveMode.Read))
        {
            
            ZipArchiveEntry cursorClickEntry = archive.CreateEntry("43a48fb4ddc2f35f2099287a26dbfe60.svg");
            using (Stream cursorClickStream = cursorClickEntry.Open())
            {
                cursorClickStream.Write(cursorclick, 0, cursorclick.Length);
            }

            ZipArchiveEntry cursorNormalEntry = archive.CreateEntry("65c6a23380f20dd8e445540b2c5bd849.svg");
            using (Stream cursorNormalStream = cursorNormalEntry.Open())
            {
                cursorNormalStream.Write(cursornormal, 0, cursornormal.Length);
            }

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
                if (block["opcode"]?.ToString() == "sensing_keyoptions")
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
        else
        {
            Console.WriteLine("No input block is used.");
        }

        Console.WriteLine("Adding TAS_WORKER sprite...");

        if (parsedJson["targets"] != null && parsedJson["targets"] is JArray targets)
        {
            targets.Add(dataSpriteJson);
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
}