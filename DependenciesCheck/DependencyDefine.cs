using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dependencies;
using Dependencies.ClrPh;

namespace DependenciesCheck
{
    /// <summary>
    /// ImportContext : Describe an import module parsed from a PE.
    /// Only used during the dependency tree building phase
    /// </summary>
    public struct ImportContext
    {
        // Import "identifier" 
        public string ModuleName;

        // Return how the module was found (NOT_FOUND otherwise)
        public ModuleSearchStrategy ModuleLocation;

        // If found, set the filepath and parsed PE, otherwise it's null
        public string PeFilePath;
        public PE PeProperties;

        // Some imports are from api sets
        public bool IsApiSet;
        public string ApiSetModuleName;

        // module flag attributes
        public ModuleFlag Flags;
    }

    [Flags]
    public enum PeTypes
    {
        None = 0,
        IMAGE_FILE_EXECUTABLE_IMAGE = 0x02,
        IMAGE_FILE_DLL = 0x2000,
    }

    [Flags]
    public enum ModuleFlag
    {
        NoFlag = 0x00,

        DelayLoad = 0x01,
        ClrReference = 0x02,
        ApiSet = 0x04,
        ApiSetExt = 0x08,
        NotFound = 0x10,
        MissingImports = 0x20,
        ChildrenError = 0x40,
    }
}
