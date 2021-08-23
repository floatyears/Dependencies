using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;

using Microsoft.Win32;
using Mono.Cecil;

using Dependencies.ClrPh;
using Dependencies;

namespace DependenciesCheck
{
    class Program
    {
        private static StreamWriter logFile;
        private static StringBuilder logContent = new StringBuilder();
        private static string workingDir;
        private static string dllDir;
        private static bool _DisplayWarning;
        private static int writeLogCnt = 10;

        static void Main(string[] args)
        {
            Phlib.InitializePhLib();

            BinaryCache.InitializeBinaryCache(false);

            dllDir = args.Length > 0 ? args[0] : ".\\";
            workingDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            dllDir = Path.GetFullPath((File.GetAttributes(dllDir) & FileAttributes.Directory) == FileAttributes.Directory ? dllDir : Path.GetDirectoryName(dllDir));
            if (!dllDir.EndsWith("Plugins"))
            {
                //如果不在Plugins目录下，那么默认在SGame.exe目录下
                foreach (var exe in Directory.GetFiles(dllDir, "*.exe"))
                {
                    var exeName = Path.GetFileNameWithoutExtension(exe);
                    //Application.dataPath
                    var dataPath = Path.Combine(Path.GetDirectoryName(exe), exeName + "_Data");
                    if (Directory.Exists(dataPath))
                    {
                        dllDir = Path.Combine(dataPath, "Plugins");
                        if (Directory.Exists(dllDir))
                        {
                            break;
                        }
                    }
                }
            }

            Dictionary<string, ImportContext> importContexts = new Dictionary<string, ImportContext>();
            Dictionary<string, List<string>> depMaps = new Dictionary<string, List<string>>();
            List<string> customSearchFolders = new List<string>();
            foreach (var dll in Directory.GetFiles(dllDir, "*.dll"))
            {
                var tempContext = new Dictionary<string, ImportContext>();
                var dllName = Path.GetFileName(dll).ToLower();
                var pe = LoadBinary(dll);
                ProcessPe(tempContext, pe, pe, SxsManifest.GetSxsEntries(pe), customSearchFolders, dllDir);
                foreach(var item in tempContext)
                {
                    List<string> depDlls;
                    var depName = item.Value.ModuleName.ToLower();
                    if (!depMaps.TryGetValue(depName, out depDlls))
                    {
                        depDlls = new List<string>();
                        depMaps.Add(depName, depDlls);
                    }
                    depDlls.Add(dllName);


                    if (!importContexts.ContainsKey(item.Key))
                    {
                        importContexts.Add(item.Key, item.Value);
                    }
                }
            }
            foreach (var item in importContexts)
            {
                if((item.Value.Flags & (ModuleFlag.ChildrenError | ModuleFlag.NotFound | ModuleFlag.MissingImports)) != ModuleFlag.NoFlag)
                {
                    WriteLog(string.Format("{0} load fail : {1}, path: {2}", item.Value.ModuleName, item.Value.Flags, item.Value.PeFilePath));
                    var name = item.Value.ModuleName.ToLower();
                    WriteDepErroRecursively(depMaps, name);
                }
            }

            WriteLog("Dll Check Finish", true);

            Console.ReadKey();
        }

        private static void WriteDepErroRecursively(Dictionary<string, List<string>> totalDeps, string moduleName, int depth = 5)
        {
            if (totalDeps.TryGetValue(moduleName, out var deps)){
                foreach (var d in deps)
                {
                    WriteLog(string.Format("----<<<{0}>>>'s load failure will cause <<<{1}>>>'s load failure", moduleName, d));
                    if (depth > 0)
                    {
                        WriteDepErroRecursively(totalDeps, d, depth - 1);
                    }
                }
            }
        }

        public static PE LoadBinary(string path)
        {

            if (!NativeFile.Exists(path))
            {
                WriteLog(String.Format("Loading PE file \"{0:s}\" failed : file not present on disk.", path));
                return null;
            }

            PE pe = BinaryCache.LoadPe(path);
            if (pe == null || !pe.LoadSuccessful)
            {
                WriteLog(String.Format("Loading module {0:s} failed.", path));
            }
            else
            {
                WriteLog(String.Format("Loading PE file \"{0:s}\" successful.", pe.Filepath));
            }

            return pe;
        }

        static void WriteLog(string content, bool flush = false)
        {
            logContent.AppendLine(content);
            Console.WriteLine(content);
            writeLogCnt--;
            if(writeLogCnt < 0)
            {
                flush = true;
                writeLogCnt = 10;
            }
            if (flush)
            {
                if(logFile == null)
                {
                    logFile = File.CreateText(Path.Combine(workingDir, "dll_check.log"));
                }
                logFile.Write(logContent.ToString());
                logFile.Flush();
                logContent.Clear();
            }
        }

        //private static void ConstructDependencyTree(PE CurrentPE, int RecursionLevel = 0)
        //{
        //    // "Closured" variables (it 's a scope hack really).
        //    Dictionary<string, ImportContext> NewTreeContexts = new Dictionary<string, ImportContext>();

        //    BackgroundWorker bw = new BackgroundWorker();
        //    bw.WorkerReportsProgress = true; // useless here for now


        //    bw.DoWork += (sender, e) => {

        //        ProcessPe(NewTreeContexts, CurrentPE);
        //    };


        //    bw.RunWorkerCompleted += (sender, e) =>
        //    {
        //        TreeBuildingBehaviour.DependencyTreeBehaviour SettingTreeBehaviour = Dependencies.TreeBuildingBehaviour.GetGlobalBehaviour();
        //        List<ModuleTreeViewItem> PeWithDummyEntries = new List<ModuleTreeViewItem>();
        //        List<BacklogImport> PEProcessingBacklog = new List<BacklogImport>();

        //        // Important !
        //        // 
        //        // This handler is executed in the STA (Single Thread Application)
        //        // which is authorized to manipulate UI elements. The BackgroundWorker is not.
        //        //

        //        foreach (ImportContext NewTreeContext in NewTreeContexts.Values)
        //        {

        //            ModuleCacheKey ModuleKey = new ModuleCacheKey(NewTreeContext);

        //            // Newly seen modules
        //            if (!this.ProcessedModulesCache.ContainsKey(ModuleKey))
        //            {
        //                // Missing module "found"
        //                if ((NewTreeContext.PeFilePath == null) || !NativeFile.Exists(NewTreeContext.PeFilePath))
        //                {
        //                    if (NewTreeContext.IsApiSet)
        //                    {
        //                        this.ProcessedModulesCache[ModuleKey] = new ApiSetNotFoundModuleInfo(ModuleName, NewTreeContext.ApiSetModuleName);
        //                    }
        //                    else
        //                    {
        //                        this.ProcessedModulesCache[ModuleKey] = new NotFoundModuleInfo(ModuleName);
        //                    }

        //                }
        //                else
        //                {


        //                    if (NewTreeContext.IsApiSet)
        //                    {
        //                        var ApiSetContractModule = new DisplayModuleInfo(NewTreeContext.ApiSetModuleName, NewTreeContext.PeProperties, NewTreeContext.ModuleLocation, NewTreeContext.Flags);
        //                        var NewModule = new ApiSetModuleInfo(NewTreeContext.ModuleName, ref ApiSetContractModule);

        //                        this.ProcessedModulesCache[ModuleKey] = NewModule;

        //                        if (SettingTreeBehaviour == TreeBuildingBehaviour.DependencyTreeBehaviour.Recursive)
        //                        {
        //                            PEProcessingBacklog.Add(new BacklogImport(childTreeNode, ApiSetContractModule.ModuleName));
        //                        }
        //                    }
        //                    else
        //                    {
        //                        var NewModule = new DisplayModuleInfo(NewTreeContext.ModuleName, NewTreeContext.PeProperties, NewTreeContext.ModuleLocation, NewTreeContext.Flags);
        //                        this.ProcessedModulesCache[ModuleKey] = NewModule;

        //                        switch (SettingTreeBehaviour)
        //                        {
        //                            case TreeBuildingBehaviour.DependencyTreeBehaviour.RecursiveOnlyOnDirectImports:
        //                                if ((NewTreeContext.Flags & ModuleFlag.DelayLoad) == 0)
        //                                {
        //                                    PEProcessingBacklog.Add(new BacklogImport(childTreeNode, NewModule.ModuleName));
        //                                }
        //                                break;

        //                            case TreeBuildingBehaviour.DependencyTreeBehaviour.Recursive:
        //                                PEProcessingBacklog.Add(new BacklogImport(childTreeNode, NewModule.ModuleName));
        //                                break;
        //                        }
        //                    }
        //                }

        //                // add it to the module list
        //                this.ModulesList.AddModule(this.ProcessedModulesCache[ModuleKey]);
        //            }

        //            // Since we uniquely process PE, for thoses who have already been "seen",
        //            // we set a dummy entry in order to set the "[+]" icon next to the node.
        //            // The dll dependencies are actually resolved on user double-click action
        //            // We can't do the resolution in the same time as the tree construction since
        //            // it's asynchronous (we would have to wait for all the background to finish and
        //            // use another Async worker to resolve).

        //            if ((NewTreeContext.PeProperties != null) && (NewTreeContext.PeProperties.GetImports().Count > 0))
        //            {
        //                ModuleTreeViewItem DummyEntry = new ModuleTreeViewItem();
        //                DependencyNodeContext DummyContext = new DependencyNodeContext()
        //                {
        //                    ModuleInfo = new WeakReference(new NotFoundModuleInfo("Dummy")),
        //                    IsDummy = true
        //                };

        //                DummyEntry.DataContext = DummyContext;
        //                DummyEntry.Header = "@Dummy : if you see this header, it's a bug.";
        //                DummyEntry.IsExpanded = false;

        //                childTreeNode.Items.Add(DummyEntry);
        //                childTreeNode.Expanded += ResolveDummyEntries;
        //            }

        //            // Add to tree view
        //            childTreeNodeContext.ModuleInfo = new WeakReference(this.ProcessedModulesCache[ModuleKey]);
        //            childTreeNode.DataContext = childTreeNodeContext;
        //            childTreeNode.Header = childTreeNode.GetTreeNodeHeaderName(Dependencies.Properties.Settings.Default.FullPath);
        //            RootNode.Items.Add(childTreeNode);
        //        }


        //        // Process next batch of dll imports
        //        if (SettingTreeBehaviour != TreeBuildingBehaviour.DependencyTreeBehaviour.ChildOnly)
        //        {
        //            foreach (var ImportNode in PEProcessingBacklog)
        //            {
        //                ConstructDependencyTree(ImportNode.Item1, ImportNode.Item2, RecursionLevel + 1); // warning : recursive call
        //            }
        //        }


        //    };

        //    bw.RunWorkerAsync();
        //}

        /// <summary>
        /// Background processing of a single PE file.
        /// It can be lengthy since there are disk access (and misses).
        /// </summary>
        /// <param name="NewTreeContexts"> This variable is passed as reference to be updated since this function is run in a separate thread. </param>
        /// <param name="newPe"> Current PE file analyzed </param>
        private static void ProcessPe(Dictionary<string, ImportContext> NewTreeContexts, PE pe, PE newPe, SxsEntries SxsEntriesCache, List<string> CustomSearchFolders, string WorkingDirectory)
        {
            List<PeImportDll> PeImports = newPe.GetImports();

            foreach (PeImportDll DllImport in PeImports)
            {
                // Ignore already processed imports
                if (NewTreeContexts.ContainsKey(DllImport.Name))
                {
                    continue;
                }

                // Find Dll in "paths"
                ImportContext ImportModule = ResolveImport(DllImport, newPe, SxsEntriesCache, CustomSearchFolders, WorkingDirectory);

                // add warning for appv isv applications 
                TriggerWarningOnAppvIsvImports(DllImport.Name);


                NewTreeContexts.Add(DllImport.Name, ImportModule);


                // AppInitDlls are triggered by user32.dll, so if the binary does not import user32.dll they are not loaded.
                ProcessAppInitDlls(NewTreeContexts, pe, newPe, ImportModule, SxsEntriesCache, CustomSearchFolders, WorkingDirectory);


                // if mscoree.dll is imported, it means the module is a C# assembly, and we can use Mono.Cecil to enumerate its references
                ProcessClrImports(NewTreeContexts, pe, newPe, ImportModule, SxsEntriesCache, CustomSearchFolders, WorkingDirectory);
            }
        }

        private static ImportContext ResolveImport(PeImportDll DllImport, PE Pe, SxsEntries SxsEntriesCache, List<string> CustomSearchFolders, string WorkingDirectory)
        {
            ImportContext ImportModule = new ImportContext();

            ImportModule.PeFilePath = null;
            ImportModule.PeProperties = null;
            ImportModule.ModuleName = DllImport.Name;
            ImportModule.ApiSetModuleName = null;
            ImportModule.Flags = 0;
            if (DllImport.IsDelayLoad())
            {
                ImportModule.Flags |= ModuleFlag.DelayLoad;
            }

            Tuple<ModuleSearchStrategy, PE> ResolvedModule = BinaryCache.ResolveModule(
                    Pe,
                    DllImport.Name,
                    SxsEntriesCache,
                    CustomSearchFolders,
                    WorkingDirectory
                );

            ImportModule.ModuleLocation = ResolvedModule.Item1;
            if (ImportModule.ModuleLocation != ModuleSearchStrategy.NOT_FOUND)
            {
                ImportModule.PeProperties = ResolvedModule.Item2;

                if (ResolvedModule.Item2 != null)
                {
                    ImportModule.PeFilePath = ResolvedModule.Item2.Filepath;
                    foreach (var Import in BinaryCache.LookupImports(DllImport, ImportModule.PeFilePath))
                    {
                        if (!Import.Item2)
                        {
                            ImportModule.Flags |= ModuleFlag.MissingImports;
                            break;
                        }

                    }
                }
            }
            else
            {
                ImportModule.Flags |= ModuleFlag.NotFound;
            }

            // special case for apiset schema
            ImportModule.IsApiSet = (ImportModule.ModuleLocation == ModuleSearchStrategy.ApiSetSchema);
            if (ImportModule.IsApiSet)
            {
                ImportModule.Flags |= ModuleFlag.ApiSet;
                ImportModule.ApiSetModuleName = BinaryCache.LookupApiSetLibrary(DllImport.Name);

                if (DllImport.Name.StartsWith("ext-"))
                {
                    ImportModule.Flags |= ModuleFlag.ApiSetExt;
                }
            }

            return ImportModule;
        }

        private static void TriggerWarningOnAppvIsvImports(string DllImportName)
        {
            if (String.Compare(DllImportName, "AppvIsvSubsystems32.dll", StringComparison.OrdinalIgnoreCase) == 0 ||
                    String.Compare(DllImportName, "AppvIsvSubsystems64.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                if (!_DisplayWarning)
                {
                    WriteLog("App-V ISV disclaimer: This binary use the App-V containerization technology which fiddle with search directories and PATH env in ways Dependencies can't handle.\n\nFollowing results are probably not quite exact.");

                    _DisplayWarning = true; // prevent the same warning window to popup several times
                }

            }
        }

        private static void ProcessAppInitDlls(Dictionary<string, ImportContext> NewTreeContexts, PE Pe, PE AnalyzedPe, ImportContext ImportModule, SxsEntries SxsEntriesCache, List<string> CustomSearchFolders, string WorkingDirectory)
        {
            List<PeImportDll> PeImports = AnalyzedPe.GetImports();

            // only user32 triggers appinit dlls
            string User32Filepath = Path.Combine(FindPe.GetSystemPath(Pe), "user32.dll");
            if (ImportModule.PeFilePath != User32Filepath)
            {
                return;
            }

            string AppInitRegistryKey =
                (Pe.IsArm32Dll()) ?
                "SOFTWARE\\WowAA32Node\\Microsoft\\Windows NT\\CurrentVersion\\Windows" :
                (Pe.IsWow64Dll()) ?
                "SOFTWARE\\Wow6432Node\\Microsoft\\Windows NT\\CurrentVersion\\Windows" :
                "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Windows";

            // Opening registry values
            RegistryKey localKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
            localKey = localKey.OpenSubKey(AppInitRegistryKey);
            int LoadAppInitDlls = (int)localKey.GetValue("LoadAppInit_DLLs", 0);
            string AppInitDlls = (string)localKey.GetValue("AppInit_DLLs", "");
            if (LoadAppInitDlls == 0 || String.IsNullOrEmpty(AppInitDlls))
            {
                return;
            }

            // Extremely crude parser. TODO : Add support for quotes wrapped paths with spaces
            foreach (var AppInitDll in AppInitDlls.Split(' '))
            {
                WriteLog("AppInit loading " + AppInitDll);

                // Do not process twice the same imported module
                if (null != PeImports.Find(module => module.Name == AppInitDll))
                {
                    continue;
                }

                if (NewTreeContexts.ContainsKey(AppInitDll))
                {
                    continue;
                }

                ImportContext AppInitImportModule = new ImportContext();
                AppInitImportModule.PeFilePath = null;
                AppInitImportModule.PeProperties = null;
                AppInitImportModule.ModuleName = AppInitDll;
                AppInitImportModule.ApiSetModuleName = null;
                AppInitImportModule.Flags = 0;
                AppInitImportModule.ModuleLocation = ModuleSearchStrategy.AppInitDLL;



                Tuple<ModuleSearchStrategy, PE> ResolvedAppInitModule = BinaryCache.ResolveModule(
                    Pe,
                    AppInitDll,
                    SxsEntriesCache,
                    CustomSearchFolders,
                    WorkingDirectory
                );
                if (ResolvedAppInitModule.Item1 != ModuleSearchStrategy.NOT_FOUND)
                {
                    AppInitImportModule.PeProperties = ResolvedAppInitModule.Item2;
                    AppInitImportModule.PeFilePath = ResolvedAppInitModule.Item2.Filepath;
                }
                else
                {
                    AppInitImportModule.Flags |= ModuleFlag.NotFound;
                }

                NewTreeContexts.Add(AppInitDll, AppInitImportModule);
            }
        }

        private static void ProcessClrImports(Dictionary<string, ImportContext> NewTreeContexts, PE pe, PE AnalyzedPe, ImportContext ImportModule, SxsEntries SxsEntriesCache, List<string> CustomSearchFolders, string WorkingDirectory)
        {
            List<PeImportDll> PeImports = AnalyzedPe.GetImports();

            // only mscorre triggers clr parsing
            string User32Filepath = Path.Combine(FindPe.GetSystemPath(pe), "mscoree.dll");
            if (ImportModule.PeFilePath != User32Filepath)
            {
                return;
            }

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(dllDir);

            // Parse it via cecil
            AssemblyDefinition PeAssembly = null;
            try
            {
                PeAssembly = AssemblyDefinition.ReadAssembly(AnalyzedPe.Filepath);
            }
            catch (BadImageFormatException)
            {
                WriteLog(
                        String.Format("CLR parsing fail: Cecil could not correctly parse {0:s}, which can happens on .NET Core executables. CLR imports will be not shown", AnalyzedPe.Filepath)
                );

                return;
            }

            foreach (var module in PeAssembly.Modules)
            {
                // Process CLR referenced assemblies
                foreach (var assembly in module.AssemblyReferences)
                {
                    AssemblyDefinition definition;
                    try
                    {
                        definition = resolver.Resolve(assembly);
                    }
                    catch (AssemblyResolutionException)
                    {
                        ImportContext AppInitImportModule = new ImportContext();
                        AppInitImportModule.PeFilePath = null;
                        AppInitImportModule.PeProperties = null;
                        AppInitImportModule.ModuleName = Path.GetFileName(assembly.Name);
                        AppInitImportModule.ApiSetModuleName = null;
                        AppInitImportModule.Flags = ModuleFlag.ClrReference;
                        AppInitImportModule.ModuleLocation = ModuleSearchStrategy.ClrAssembly;
                        AppInitImportModule.Flags |= ModuleFlag.NotFound;

                        if (!NewTreeContexts.ContainsKey(AppInitImportModule.ModuleName))
                        {
                            NewTreeContexts.Add(AppInitImportModule.ModuleName, AppInitImportModule);
                        }

                        continue;
                    }

                    foreach (var AssemblyModule in definition.Modules)
                    {
                        WriteLog("Referenced Assembling loading " + AssemblyModule.Name + " : " + AssemblyModule.FileName);

                        // Do not process twice the same imported module
                        if (null != PeImports.Find(mod => mod.Name == Path.GetFileName(AssemblyModule.FileName)))
                        {
                            continue;
                        }

                        ImportContext AppInitImportModule = new ImportContext();
                        AppInitImportModule.PeFilePath = null;
                        AppInitImportModule.PeProperties = null;
                        AppInitImportModule.ModuleName = Path.GetFileName(AssemblyModule.FileName);
                        AppInitImportModule.ApiSetModuleName = null;
                        AppInitImportModule.Flags = ModuleFlag.ClrReference;
                        AppInitImportModule.ModuleLocation = ModuleSearchStrategy.ClrAssembly;

                        Tuple<ModuleSearchStrategy, PE> ResolvedAppInitModule = BinaryCache.ResolveModule(
                            pe,
                            AssemblyModule.FileName,
                            SxsEntriesCache,
                            CustomSearchFolders,
                            WorkingDirectory
                        );
                        if (ResolvedAppInitModule.Item1 != ModuleSearchStrategy.NOT_FOUND)
                        {
                            AppInitImportModule.PeProperties = ResolvedAppInitModule.Item2;
                            AppInitImportModule.PeFilePath = ResolvedAppInitModule.Item2.Filepath;
                        }
                        else
                        {
                            AppInitImportModule.Flags |= ModuleFlag.NotFound;
                        }

                        if (!NewTreeContexts.ContainsKey(AppInitImportModule.ModuleName))
                        {
                            NewTreeContexts.Add(AppInitImportModule.ModuleName, AppInitImportModule);
                        }
                    }

                }

                // Process unmanaged dlls for native calls
                foreach (var UnmanagedModule in module.ModuleReferences)
                {
                    // some clr dll have a reference to an "empty" dll
                    if (UnmanagedModule.Name.Length == 0)
                    {
                        continue;
                    }

                    WriteLog("Referenced module loading " + UnmanagedModule.Name);

                    // Do not process twice the same imported module
                    if (null != PeImports.Find(m => m.Name == UnmanagedModule.Name))
                    {
                        continue;
                    }



                    ImportContext AppInitImportModule = new ImportContext();
                    AppInitImportModule.PeFilePath = null;
                    AppInitImportModule.PeProperties = null;
                    AppInitImportModule.ModuleName = UnmanagedModule.Name;
                    AppInitImportModule.ApiSetModuleName = null;
                    AppInitImportModule.Flags = ModuleFlag.ClrReference;
                    AppInitImportModule.ModuleLocation = ModuleSearchStrategy.ClrAssembly;

                    Tuple<ModuleSearchStrategy, PE> ResolvedAppInitModule = BinaryCache.ResolveModule(
                        pe,
                        UnmanagedModule.Name,
                        SxsEntriesCache,
                        CustomSearchFolders,
                        WorkingDirectory
                    );
                    if (ResolvedAppInitModule.Item1 != ModuleSearchStrategy.NOT_FOUND)
                    {
                        AppInitImportModule.PeProperties = ResolvedAppInitModule.Item2;
                        AppInitImportModule.PeFilePath = ResolvedAppInitModule.Item2.Filepath;
                    }

                    if (!NewTreeContexts.ContainsKey(AppInitImportModule.ModuleName))
                    {
                        NewTreeContexts.Add(AppInitImportModule.ModuleName, AppInitImportModule);
                    }
                }
            }
        }
    }
}
