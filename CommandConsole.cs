using System.Text;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using Color = System.Windows.Media.Color;
using WpfAnimatedGif;

namespace globulator;

public class CommandConsole()
{
    private const string VERSION = "Poly Use Linked Storage and Text Editor version: b1.1.1";

    readonly static JObject json = JObject.Parse(File.ReadAllText("config.json"));

    private string Password = "";
    public string CurrentDirectory = json.Value<string>("defaultdir");
    public string[] AllFilesInCurrentPath = Directory.GetFiles(json.Value<string>("defaultdir"));

    public bool isMuted = json.Value<bool>("mute");

    public string CurrentGlobName = "";
    private string CurrentGlobPath = "";
    private List<FileWrapper> CurrentGlobContents = new();

    #region Compression
    // nicked a good deal of this
    public static void CopyTo(Stream src, Stream dest)
    {
        byte[] bytes = new byte[4096];

        int cnt;

        while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            dest.Write(bytes, 0, cnt);
    }

    public static byte[] Zip(byte[] bytes)
    {
        using var msi = new MemoryStream(bytes);
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(mso, CompressionMode.Compress))
            CopyTo(msi, gs);

        return mso.ToArray();
    }

    public static byte[] Unzip(byte[] bytes)
    {
        using var msi = new MemoryStream(bytes);
        using var mso = new MemoryStream();
        using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            CopyTo(gs, mso);

        return mso.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        MemoryStream output = new();
        using (DeflateStream dStream = new(output, CompressionLevel.SmallestSize))
            dStream.Write(data, 0, data.Length);
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        MemoryStream input = new(data);
        MemoryStream output = new();
        using (DeflateStream dStream = new(input, CompressionMode.Decompress))
            dStream.CopyTo(output);
        return output.ToArray();
    }
    #endregion

    public (string, Color) ProcessCommand(string command, (string, string) fileDetails)
    {
        string[] args = command.Split(' ');

        try {
            return args[0] switch
            {
                "echo" => Echo(args[1..]),
                "login" => Login(args[1]),
                "help" => HelpCommand(args[1..]),
                "cd" => ChangeDirectory(args[1..]),
                "ls" => ListFiles(args[1..]),
                "mk" => MakeFile(args[1], args[2]),
                "dl" => DeleteFile(args[1..]),
                "globulate" => MoveFileToGlob(args[1], args[2], args[3..]),
                "extract" => ExtractFromGlob(args[1], args[2], args[3..]),
                "peek" => ViewGlobContents(args[1..]),
                "def" => SetDefaultDirectory(),
                "open" => OpenFile(args[1]),
                "ver" => (VERSION, Color.FromRgb(255, 228, 225)),
                "sl" => SelectFile(args[1..]),
                "commit" => WriteToSelectedFile(fileDetails),
                "togglemute" => ToggleMute(),
                _ => CommandNotFoundError(args[0])
            };
        } catch (IndexOutOfRangeException) {
            return ("There was an error parsing command arguments.", Color.FromRgb(205, 0, 26));
        }
    }

    // since the usual parsing for commands works by splitting on a space, we have to pass in an array of the rest of the arguments
    private static (string, Color) Echo(string[] message)
    {
        string output = string.Join(' ', message);
        return (output, Color.FromRgb(255, 228, 225));
    }

    private (string, Color) ToggleMute()
    {
        isMuted ^= true;

        Dictionary<string, dynamic> newConfig = new()
        {
            { "defaultdir", CurrentDirectory },
            { "mute", isMuted }
        };

        string json = JsonConvert.SerializeObject(newConfig, Formatting.Indented);
        File.WriteAllText("config.json", json);

        return ($"Mute is now set to '{isMuted}'.", Color.FromRgb(255, 228, 225));
    }

    // the password is used as an encryption seed for each .glob
    private (string, Color) Login(string password)
    {
        Password = password;
        return ("Login successful.", Color.FromRgb(0, 155, 119));
    }

    // since the usual parsing for commands works by splitting on a space, we have to pass in an array of the rest of the arguments
    private static (string, Color) HelpCommand(string[] command)
    {
        Dictionary<string, string> commands = new()
        {
            { "echo", " <message> - Repeats <message> in terminal." },
            { "login", " <password> - Sets current glob encryption key to <password>." },
            { "help", " <command = *> - Displays help on <command>, by default will display all commands." },
            { "cd", " <directory> - Changes current directory scope to <directory>. <directory = '^'> retrieves parent directory." },
            { "ls", " <type> - Lists all files / directories in current directory depending on the value of <type>. <type = -f | -d | -g> where -f is all files, -d is all directories and -g is all globs. Use F11 and F12 to scroll through each file." },
            { "mk", " <type> <name> - Creates file of <type> with name <name>. <type = -f | -g> where -f is for a file and -g is for a glob." },
            { "dl", " <file> - Deletes file at <file>." },
            { "globulate", " <file> <copy? = -m | -c> <glob> - Moves <file> to <glob>. <copy = -c> indicates to copy the file, and if <glob> is left blank, the currently selected glob is assumed." },
            { "extract", " <file ?= *> <copy? = -m | -c> <glob> - Extracts <file> from <glob>. <copy = -c> indicates to copy the file, and if <glob> is left blank, the currently selected glob is assumed. <file = *> indicates all files should be extracted." },
            { "peek", " <glob> - Views contents of <glob>, and sets it to the currently selected glob. If <glob> is left blank, the currently selected glob is assumed."},
            { "def", " - Sets current directory to the default." },
            { "open", " <file> - Opens <file> in default application." },
            { "ver", " - Displays current software version." },
            { "sl", " <file> - Selects <file> for viewing / editing on the side. Use F11 and F12 to scroll through each file." },
            { "commit", " - Writes changes from selected file." },
            { "togglemute", " - Toggles mute." }
        };

        // checks if user is requesting help for specific command
        string response = "";
        if (command.Length == 0)    // the length being zero indicates the user has inputted no argument, and should therefore display everything
        {
            foreach (KeyValuePair<string, string> kvPair in commands)
                response += $"\n> {kvPair.Key}{kvPair.Value}";

            return (response + '\n', Color.FromRgb(255, 228, 225));
        }

        try {
            response = commands[command[0]];
            return (command[0] + response, Color.FromRgb(255, 228, 225));
        } catch (KeyNotFoundException) {
            return CommandNotFoundError(command[0]);
        }
    }

    #region Glob
    // since the usual parsing for commands works by splitting on a space, we have to pass in an array of the rest of the arguments
    private (string, Color) ViewGlobContents(string[] glob)
    {
        string stringGlob;
        if (glob.Length > 0)    // length greater than zero indicates user has inputted a path for the .glob
        {
            stringGlob = glob[0];
            CurrentGlobPath = ParseDirectoryRequest(stringGlob);
        }
        else
            stringGlob = CurrentGlobName;

        if (File.Exists(CurrentGlobPath))
        {
            // read all the byte data of a file [File.ReadAllBytes()]
            // decompress the encrypted data [Unzip()]
            // decrypt [ByteCipher.XOR()]
            // and finally read the json [Encoding.ASCII.GetString()]
            // it's worth noting that each file's byte data is also compressed separately, but we don't need to do that here since we're only reading-
            // - the file name and size
            CurrentGlobName = stringGlob;
            string json = Encoding.ASCII.GetString(ByteCipher.XOR(Unzip(File.ReadAllBytes(CurrentGlobPath)), Password));

            // get all files in selected .glob file and read file name and size
            try {
                CurrentGlobContents = JsonConvert.DeserializeObject<List<FileWrapper>>(json);
            } catch (JsonReaderException) {
                return ("There was an error deserializing glob contents. This very likely means the password provided was incorrect.", Color.FromRgb(205, 0, 26));
            }

            string contents = "\n";
            foreach (FileWrapper f in CurrentGlobContents)
                contents += "> " + f.FileName + $" ({f.Bytes.Length} B)" + '\r';
            contents += '\n';

            return (contents, Color.FromRgb(255, 228, 225));
        }
        
        return ($"Error! Glob '{stringGlob}' does not exist!", Color.FromRgb(205, 0, 26));
    }
    
    

    private void ExtractSingleFile(FileWrapper file, string isCopy, string globPath)
    {
        // create a new file containing the decompressed byte data of file.Bytes and write
        File.WriteAllBytes(CurrentDirectory + '\\' + file.FileName, Decompress(file.Bytes));

        if (isCopy == "-m")
        {
            CurrentGlobContents.Remove(file);
            // get the byte data of the json serialized object [JsonConvert.SerializeObject()]
            // encrypt it [ByteCipher.XOR()]
            // compress it [Zip()]
            byte[] jsonBytes = Zip(ByteCipher.XOR(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(CurrentGlobContents, Formatting.Indented)), Password));
            File.WriteAllBytes(globPath, jsonBytes);
        }    
    }

    // since the usual parsing for commands works by splitting on a space, we have to pass in an array of the rest of the arguments
    private (string, Color) ExtractFromGlob(string fileName, string isCopy, string[] glob)
    {
        if (!(isCopy == "-c" || isCopy == "-m"))
            return ($"Argument '{isCopy}' is invalid!", Color.FromRgb(205, 0, 26));

        string globPath;
        string globName;
        if (glob.Length > 0) // length greater than zero indicates user has inputted a path for the .glob
        {
            globPath = ParseDirectoryRequest(glob[0]);
            globName = glob[0];
        }   
        else
        {
            globPath = CurrentGlobPath;
            globName = CurrentGlobName;
        }
            
        if (File.Exists(globPath))
        {
            // refer to above method for reading file json
            string json = Encoding.ASCII.GetString(ByteCipher.XOR(Unzip(File.ReadAllBytes(CurrentGlobPath)), Password));

            try {
                // get all files in selected .glob file
                CurrentGlobContents = JsonConvert.DeserializeObject<List<FileWrapper>>(json);
            } catch (JsonReaderException) {
                return ("There was an error deserializing glob contents. This very likely means the password provided was incorrect.", Color.FromRgb(205, 0, 26));
            }

            if (fileName != "*")
            {
                // acquire file by file name
                FileWrapper file = CurrentGlobContents.FirstOrDefault(file => file.FileName == fileName);

                if (file is null)
                    return ($"File '{fileName}' does not exist in glob '{glob}'!", Color.FromRgb(205, 0, 26));

                ExtractSingleFile(file, isCopy, globPath);
            }
            
            else
            {               
                if (isCopy == "-m")
                {
                    // we have to define an amount here and do a manual loop because CurrentGlobContents.Count may change as files are extracted
                    int count = CurrentGlobContents.Count;
                    for (int _ = 0; _ < count; _++)
                        ExtractSingleFile(CurrentGlobContents[0], isCopy, globPath);
                }
                                    
                else
                    foreach (FileWrapper file in CurrentGlobContents)
                        ExtractSingleFile(file, isCopy, globPath);
            }
                
            return ($"Extracted '{fileName}' from glob '{globName}'.", Color.FromRgb(0, 155, 119));
        }
        
        return ($"Error! Glob '{globName}' does not exist!", Color.FromRgb(205, 0, 26));
    }

    // since the usual parsing for commands works by splitting on a space, we have to pass in an array of the rest of the arguments
    private (string, Color) MoveFileToGlob(string fileName, string isCopy, string[] glob)
    {
        if (!(isCopy == "-c" || isCopy == "-m"))
            return ($"Argument '{isCopy}' is invalid!", Color.FromRgb(205, 0, 26));

        string? filePath = ParseDirectoryRequest(fileName);

        string? globPath;
        string globName;
        if (glob.Length > 0) // length greater than zero indicates user has inputted a path for the .glob
        {
            globPath = ParseDirectoryRequest(glob[0]);
            globName = glob[0];
        }        
        else
        {
            globPath = CurrentGlobPath;
            globName = CurrentGlobName;
        }

        if (File.Exists(filePath) && File.Exists(globPath))
        {
            // refer to previous
            string json = Encoding.ASCII.GetString(ByteCipher.XOR(Unzip(File.ReadAllBytes(CurrentGlobPath)), Password));

            List<FileWrapper> files;
            try {
                // get all files in selected .glob file
                files = JsonConvert.DeserializeObject<List<FileWrapper>>(json);
            } catch (JsonReaderException) {
                return ("There was an error deserializing glob contents. This very likely means the password provided was incorrect.", Color.FromRgb(205, 0, 26));
            }

            // create a new instance of the file with compressed byte data
            FileWrapper file = new(fileName, Compress(File.ReadAllBytes(filePath)));

            files.Add(file);

            // refer to previous
            byte[] jsonBytes = Zip(ByteCipher.XOR(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(files, Formatting.Indented)), Password));
            File.WriteAllBytes(globPath, jsonBytes);

            if (isCopy == "-m")
                File.Delete(filePath);

            return ($"Moved file '{fileName}' to glob '{globName}'.", Color.FromRgb(0, 155, 119));
        }
        else
            return ($"Error! Either file '{fileName}' or glob '{globName}' does not exist!", Color.FromRgb(205, 0, 26));
    }
    #endregion

    #region Files
    private (string, Color) OpenFile(string path)
    {
        string file = ParseDirectoryRequest(path);

        using Process p = new();

        p.StartInfo.FileName = "explorer";
        p.StartInfo.Arguments = "\"" + file + "\"";
        p.Start();

        return ($"Opened file '{file}'.", Color.FromRgb(0, 155, 119));
    }

    private (string, Color) SetDefaultDirectory()
    {
        Dictionary<string, dynamic> newConfig = new()
        {
            { "defaultdir", CurrentDirectory },
            { "mute", isMuted }
        };

        string json = JsonConvert.SerializeObject(newConfig, Formatting.Indented);

        File.WriteAllText("config.json", json);
        return ("Set current directory to default.", Color.FromRgb(0, 155, 119));
    }

    // -f refers to files
    // -d refers to directories
    // -g refers to globs
    private (string, Color) MakeFile(string type, string name)
    {
        string filePath = CurrentDirectory + '\\' + name;

        try {
            if (type == "-f")
                File.Create(filePath).Dispose();
            else if (type == "-d")
                Directory.CreateDirectory(filePath);
            else if (type == "-g")
                File.WriteAllBytes(filePath + ".glob", Zip(ByteCipher.XOR(Encoding.ASCII.GetBytes("[]"), Password)));
                            
        } catch (Exception ex) {
            return ($"An error occurred whilst attempting to create file '{name}'. '{ex}'", Color.FromRgb(205, 0, 26));
        }

        return ($"Created file '{name}'.", Color.FromRgb(0, 155, 119));
    }

    private (string, Color) ListFiles(string[] type)
    {
        string stringAllFiles = "\n";
        DirectoryInfo dInfo = new(CurrentDirectory);

        if (type.Length == 0)
        {
            stringAllFiles += "Files:\r";

            FileInfo[] allFiles = dInfo.GetFiles();

            string allGlobs = "";

            foreach (FileInfo file in allFiles)
            {
                if (file.Name.EndsWith("glob"))
                    allGlobs += "> " + file.Name + $" ({file.Length} B)" + '\r';
                else
                    stringAllFiles += "> " + file.Name + $" ({file.Length} B)" + '\r';
            }
                
            stringAllFiles += "\rDirectories:\r";

            DirectoryInfo[] allDirs = dInfo.GetDirectories();

            foreach (DirectoryInfo dir in allDirs)
                stringAllFiles += "> " + dir.Name + '\r';

            stringAllFiles += "\rGlobs:\r" + allGlobs;
            return (stringAllFiles, Color.FromRgb(255, 228, 225));
        }

        switch(type[0])
        {
            case "-d":
                DirectoryInfo[] allDirs = dInfo.GetDirectories();

                foreach (DirectoryInfo dir in allDirs)
                    stringAllFiles += "> "+ dir.Name + '\r';
                break;
            case "-f":
                FileInfo[] allFiles = dInfo.GetFiles();

                foreach (FileInfo file in allFiles)
                    stringAllFiles += "> " + file.Name + $" ({file.Length} B)" + '\r';
                break;
            case "-g":
                allFiles = dInfo.GetFiles();

                string allGlobs = "";

                foreach (FileInfo file in allFiles)
                    if (file.Name.EndsWith("glob"))
                        allGlobs += "> " + file.Name + $" ({file.Length} B)" + '\r';

                stringAllFiles += allGlobs;
                break;
            default:
                return ($"Invalid argument '{type[0]}'.", Color.FromRgb(205, 0, 26));
        };

        return (stringAllFiles + '\n', Color.FromRgb(255, 228, 225));
    }

    private (string, Color) SelectFile(string[] path)
    {
        string? stringPath = ParseDirectoryRequest(string.Join(' ', path));

        if (stringPath is null)
            return ($"File '{string.Join(' ', path)}' does not exist!", Color.FromRgb(205, 0, 26));


        Window parentWindow = Application.Current.MainWindow;
        RichTextBox viewingBox = (RichTextBox)parentWindow.FindName("viewingBox");
        Image viewingImage = (Image)parentWindow.FindName("pictureBox");
        Label pathLabel = (Label)parentWindow.FindName("viewedFilePath");

        pathLabel.Content = stringPath;

        if (stringPath.EndsWith("png") || stringPath.EndsWith("jpg") || stringPath.EndsWith("jpeg") || stringPath.EndsWith("bmp") || stringPath.EndsWith("gif"))
        {
            viewingImage.Visibility = Visibility.Visible;
            viewingBox.Visibility = Visibility.Hidden;
                   
            BitmapImage im = new();
            im.BeginInit();
            im.UriSource = new Uri(stringPath);
            im.EndInit();

            ImageBehavior.SetAnimatedSource(viewingImage, im);
        }
            
        else
        {
            viewingBox.Visibility = Visibility.Visible;
            viewingImage.Visibility = Visibility.Hidden;
            
            FlowDocument textDoc = new();

            Run textRun = new(File.ReadAllText(stringPath));

            Paragraph text = new();
            text.Inlines.Add(textRun);

            textDoc.Blocks.Add(text);

            viewingBox.Document = textDoc;
        }

        return ($"Currently viewing '{stringPath}'.", Color.FromRgb(255, 228, 225));
    }

    private static (string, Color) WriteToSelectedFile((string, string) fileDetails)
    {
        if (fileDetails.Item2.EndsWith("png") || fileDetails.Item2.EndsWith("jpg") || fileDetails.Item2.EndsWith("jpeg") || fileDetails.Item2.EndsWith("bmp") || fileDetails.Item2.EndsWith("gif") || fileDetails.Item2.EndsWith("glob"))
            return ("Selected file is not a text file!", Color.FromRgb(205, 0, 26));

        File.WriteAllText(fileDetails.Item2, fileDetails.Item1);
        return ($"Wrote to file '{fileDetails.Item2}'.", Color.FromRgb(0, 155, 119));
    }

    private (string, Color) DeleteFile(string[] path)
    {
        string? stringPath = ParseDirectoryRequest(string.Join(' ', path));

        if (stringPath is null)
            return ($"File '{string.Join(' ', path)}' does not exist.", Color.FromRgb(205, 0, 26));

        File.Delete(stringPath);

        return ($"Deleted file '{stringPath}'.", Color.FromRgb(0, 155, 119));
    }

    private string? ParseDirectoryRequest(string requestedPath)
    {
        if (requestedPath == "^")
            return Directory.GetParent(CurrentDirectory).FullName;

        string potentialLocalDirectoryPath = CurrentDirectory + '\\' + requestedPath;

        return FileWrapper.FileOrDirectoryExists(potentialLocalDirectoryPath) ? potentialLocalDirectoryPath :
               FileWrapper.FileOrDirectoryExists(requestedPath) ? requestedPath : null;
    }

    private (string, Color) ChangeDirectory(string[] path)
    {
        string stringPath = string.Join(' ', path);

        string? nextDirectory = ParseDirectoryRequest(stringPath);
        if (nextDirectory is null)
            return ($"Directory '{stringPath}' does not exist!", Color.FromRgb(205, 0, 26));
       
        CurrentDirectory = nextDirectory;
        return ($"Changed current directory scope to '{stringPath}'.", Color.FromRgb(0, 155, 119));   
    }
    #endregion

    private static (string, Color) CommandNotFoundError(string command)
    {    
        return ($"Error! Command '{command}' not found!", Color.FromRgb(205, 0, 26));
    }
}
