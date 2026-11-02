using System.IO;
using UnityEditor;

// 대본(.ink)을 json으로 컴파일한다. 에디터를 GUI로 열어 두면 Ink 패키지가 알아서 하지만,
// 배치 모드에서는 자동 컴파일이 돌지 않아서 이걸 쓴다.
//   Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod InkBuild.CompileAll
public static class InkBuild
{
    const string Dir = "Assets/_Project/Dialogue";
    static readonly string[] Masters = { "Baekrojin" }; // INCLUDE 되지 않는 최상위 대본

    class Files : Ink.IFileHandler
    {
        public string ResolveInkFilename(string name) => Path.Combine(Dir, name);
        public string LoadInkFileContents(string path) => File.ReadAllText(path);
    }

    [MenuItem("GPA/대본 컴파일")]
    public static void CompileAll()
    {
        foreach (var name in Masters)
        {
            string ink = $"{Dir}/{name}.ink", json = $"{Dir}/{name}.json";
            var story = new Ink.Compiler(File.ReadAllText(ink), new Ink.Compiler.Options { sourceFilename = ink, fileHandler = new Files() }).Compile();
            File.WriteAllText(json, story.ToJson());
            AssetDatabase.ImportAsset(json);
        }
    }
}
